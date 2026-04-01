using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Minnaloushe.Core.Toolbox.PollingFolderWatcher.Tests;

[TestFixture]
public class FolderChangeDetectorTests
{
    #region Constants

    private const string TestDirectoryPath = "/test/path";
    private const string TestFileRegex = @"\.txt$";

    #endregion

    #region Fields

    private Mock<IFolderWatcherHandler> _handlerMock = null!;
    private Mock<IFileSystemAccessor> _fileSystemAccessorMock = null!;
    private readonly Mock<IServiceScopeFactory> _serviceScopeFactoryMock = new();
    private readonly Mock<IServiceProvider> _serviceProviderMock = new();

    private readonly Mock<IServiceScope> _serviceScopeMock = new();

    private readonly ILogger<FolderChangeDetector> _nullLogger = NullLogger<FolderChangeDetector>.Instance;
    private FolderWatcherOptions _options = null!;
    private FolderChangeDetector _sut = null!;
    private CancellationTokenSource _cts = null!;

    #endregion

    #region Setup and Teardown

    [SetUp]
    public void Setup()
    {
        _handlerMock = new Mock<IFolderWatcherHandler>();
        _fileSystemAccessorMock = new Mock<IFileSystemAccessor>();
        _cts = new CancellationTokenSource();

        _options = new FolderWatcherOptions
        {
            Path = TestDirectoryPath,
            Interval = TimeSpan.FromMilliseconds(100),
            MaskRegex = TestFileRegex,
            EnumerationOptions = new EnumerationOptions(),
            WriteCompletionCheckAttempts = 3,
            WriteCompletionCheckWaitDelay = TimeSpan.FromMilliseconds(10)
        };

        _serviceScopeFactoryMock.Setup(x => x.CreateScope())
            .Returns(_serviceScopeMock.Object);

        _serviceScopeMock.Setup(x => x.ServiceProvider)
            .Returns(_serviceProviderMock.Object);

        _serviceProviderMock.Setup(x => x.GetService(typeof(IFolderWatcherHandler)))
            .Returns(_handlerMock.Object);

        _sut = new FolderChangeDetector(
            Options.Create(_options),
            _fileSystemAccessorMock.Object,
            _serviceScopeFactoryMock.Object,
            _nullLogger);
    }

    [TearDown]
    public void TearDown()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    #endregion

    #region Helper Methods

    private static FileInfoSnapshot CreateFileSnapshot(string name, long length = 100, DateTimeOffset? modifiedAt = null)
    {
        var now = modifiedAt ?? DateTimeOffset.UtcNow;
        return new FileInfoSnapshot
        {
            Name = name,
            FullName = $"{TestDirectoryPath}/{name}",
            Length = length,
            CreatedAt = now,
            ModifiedAt = now
        };
    }

    private void SetupGetFilesResponse(params FileInfoSnapshot[] files)
    {
        _fileSystemAccessorMock
            .Setup(x => x.GetFiles(TestDirectoryPath, It.IsAny<EnumerationOptions>()))
            .ReturnsAsync(files);
    }

    private void SetupFileStability(bool isStable)
    {
        _fileSystemAccessorMock
            .Setup(x => x.CheckForFileWriteCompletionAsync(
                It.IsAny<FileInfoSnapshot>(),
                It.IsAny<int>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(isStable);
    }

    #endregion

    #region Test Methods

    [Test]
    public async Task PollAsync_WhenNewFileAppears_ThenHandlerInvokedWithNewEvent()
    {
        // Arrange
        var file = CreateFileSnapshot("file1.txt");
        SetupGetFilesResponse(file);
        SetupFileStability(true);

        FileChangedEventArgs? capturedArgs = null;
        _handlerMock
            .Setup(x => x.HandleFileChange(It.IsAny<FileChangedEventArgs>(), It.IsAny<CancellationToken>()))
            .Callback<FileChangedEventArgs, CancellationToken>((args, _) => capturedArgs = args)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.PollAsync(_cts.Token);

        // Assert
        _handlerMock.Verify(
            x => x.HandleFileChange(It.IsAny<FileChangedEventArgs>(), It.IsAny<CancellationToken>()),
            Times.Once);

        capturedArgs.Should().NotBeNull();
        capturedArgs!.EventType.Should().Be(ChangeEventType.New);
        capturedArgs.FileInfo.FullName.Should().Be(file.FullName);
    }

    [Test]
    public async Task PollAsync_WhenFileIsModified_ThenHandlerInvokedWithModifiedEvent()
    {
        // Arrange
        var originalFile = CreateFileSnapshot("file1.txt", length: 100, modifiedAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        var modifiedFile = CreateFileSnapshot("file1.txt", length: 200, modifiedAt: DateTimeOffset.UtcNow);

        SetupFileStability(true);

        FileChangedEventArgs? capturedArgs = null;

        _handlerMock
            .Setup(x => x.HandleFileChange(It.IsAny<FileChangedEventArgs>(), It.IsAny<CancellationToken>()))
            .Callback<FileChangedEventArgs, CancellationToken>((args, _) => capturedArgs = args)
            .Returns(Task.CompletedTask);

        // Act
        SetupGetFilesResponse(originalFile);
        await _sut.PollAsync(_cts.Token);

        SetupGetFilesResponse(modifiedFile);
        await _sut.PollAsync(_cts.Token);

        // Assert
        _handlerMock.Verify(
            x => x.HandleFileChange(
                It.Is<FileChangedEventArgs>(args => args.EventType == ChangeEventType.Modified),
                It.IsAny<CancellationToken>()),
            Times.Once);

        capturedArgs.Should().NotBeNull();
        capturedArgs!.EventType.Should().Be(ChangeEventType.Modified);
    }

    [Test]
    public async Task PollAsync_WhenFileIsDeleted_ThenHandlerInvokedWithDeletedEvent()
    {
        // Arrange
        var file = CreateFileSnapshot("file1.txt");

        SetupFileStability(true);

        FileChangedEventArgs? capturedArgs = null;

        _handlerMock
            .Setup(x => x.HandleFileChange(It.IsAny<FileChangedEventArgs>(), It.IsAny<CancellationToken>()))
            .Callback<FileChangedEventArgs, CancellationToken>((args, _) => capturedArgs = args)
            .Returns(Task.CompletedTask);

        // Act
        SetupGetFilesResponse(file);
        await _sut.PollAsync(_cts.Token);

        SetupGetFilesResponse();
        await _sut.PollAsync(_cts.Token);

        // Assert
        _handlerMock.Verify(
            x => x.HandleFileChange(
                It.Is<FileChangedEventArgs>(args => args.EventType == ChangeEventType.Deleted),
                It.IsAny<CancellationToken>()),
            Times.Once);

        capturedArgs.Should().NotBeNull();
        capturedArgs!.EventType.Should().Be(ChangeEventType.Deleted);
        capturedArgs.FileInfo.FullName.Should().Be(file.FullName);
    }

    [Test]
    public async Task PollAsync_WhenFileDoesNotMatchRegex_ThenHandlerNotInvoked()
    {
        // Arrange
        var file = CreateFileSnapshot("file1.pdf");
        SetupGetFilesResponse(file);
        SetupFileStability(true);

        // Act
        await _sut.PollAsync(_cts.Token);

        // Assert
        _handlerMock.Verify(
            x => x.HandleFileChange(It.IsAny<FileChangedEventArgs>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task PollAsync_WhenFileIsNotStable_ThenHandlerNotInvokedAndFileRetriedNextPoll()
    {
        // Arrange
        var file = CreateFileSnapshot("file1.txt");
        SetupGetFilesResponse(file);

        var callCount = 0;
        _fileSystemAccessorMock
            .Setup(x => x.CheckForFileWriteCompletionAsync(
                It.IsAny<FileInfoSnapshot>(),
                It.IsAny<int>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => callCount++ > 0);

        // Act
        await _sut.PollAsync(_cts.Token);

        // Assert
        _handlerMock.Verify(
            x => x.HandleFileChange(It.IsAny<FileChangedEventArgs>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Act
        await _sut.PollAsync(_cts.Token);

        // Assert
        _handlerMock.Verify(
            x => x.HandleFileChange(
                It.Is<FileChangedEventArgs>(args => args.EventType == ChangeEventType.New),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task PollAsync_WhenHandlerThrowsException_ThenExceptionLoggedAndProcessingContinues()
    {
        // Arrange
        var file1 = CreateFileSnapshot("file1.txt");
        var file2 = CreateFileSnapshot("file2.txt");
        SetupGetFilesResponse(file1, file2);
        SetupFileStability(true);

        var exception = new InvalidOperationException("Handler error");
        var callCount = 0;

        _handlerMock
            .Setup(x => x.HandleFileChange(It.IsAny<FileChangedEventArgs>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                return callCount++ == 0 ? throw exception : Task.CompletedTask;
            });

        // Act
        await _sut.PollAsync(_cts.Token);

        // Assert
        _handlerMock.Verify(
            x => x.HandleFileChange(It.IsAny<FileChangedEventArgs>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Test]
    public async Task PollAsync_WhenMultipleFilesChange_ThenHandlerInvokedForEachChange()
    {
        // Arrange
        var file1 = CreateFileSnapshot("file1.txt");
        var file2 = CreateFileSnapshot("file2.txt");
        var file3 = CreateFileSnapshot("file3.txt");

        SetupGetFilesResponse(file1, file2, file3);
        SetupFileStability(true);

        var capturedEvents = new List<FileChangedEventArgs>();
        _handlerMock
            .Setup(x => x.HandleFileChange(It.IsAny<FileChangedEventArgs>(), It.IsAny<CancellationToken>()))
            .Callback<FileChangedEventArgs, CancellationToken>((args, _) => capturedEvents.Add(args))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.PollAsync(_cts.Token);

        // Assert
        _handlerMock.Verify(
            x => x.HandleFileChange(It.IsAny<FileChangedEventArgs>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));

        capturedEvents.Should().HaveCount(3);
        capturedEvents.Should().AllSatisfy(e => e.EventType.Should().Be(ChangeEventType.New));
    }

    [Test]
    public async Task PollAsync_WhenFileUnchanged_ThenHandlerNotInvokedOnSubsequentPolls()
    {
        // Arrange
        var file = CreateFileSnapshot("file1.txt");
        SetupGetFilesResponse(file);
        SetupFileStability(true);

        // Act
        await _sut.PollAsync(_cts.Token);
        await _sut.PollAsync(_cts.Token);
        await _sut.PollAsync(_cts.Token);

        // Assert
        _handlerMock.Verify(
            x => x.HandleFileChange(
                It.Is<FileChangedEventArgs>(args => args.EventType == ChangeEventType.New),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}
