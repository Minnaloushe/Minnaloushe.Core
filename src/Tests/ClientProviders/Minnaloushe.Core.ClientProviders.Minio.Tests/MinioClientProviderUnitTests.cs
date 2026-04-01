using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minio;
using Minnaloushe.Core.ClientProviders.Abstractions.VaultExtensions;
using Minnaloushe.Core.ClientProviders.Minio.Extensions;
using Minnaloushe.Core.ClientProviders.Minio.Options;
using Minnaloushe.Core.ClientProviders.Minio.Tests.Helpers;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Extensions;
using Minnaloushe.Core.Toolbox.TestHelpers;
using Minnaloushe.Core.VaultOptions.Vault;
using Moq;

namespace Minnaloushe.Core.ClientProviders.Minio.Tests;

public class MinioClientProviderUnitTests
{
    #region Fixture members

    #region Constants

    private const string StorageKey1 = "s3_1";
    private const string StorageKey2 = "s3_2";
    private const string ServiceUrl1 = "https://minio.local:9000";
    private const string ServiceUrl2 = "https://minio.other:9000";
    private const string AccessKey1 = "AKIA111111111";
    private const string AccessKey2 = "AKIA222222222";
    private const string SecretKey1 = "secret1";
    private const string SecretKey2 = "secret2";
    private const string Bucket1 = "bucket1";
    private const string Bucket2 = "bucket2";

    #endregion

    #region Fields

    private TestHost _sut = null!;
    private Mock<IMinioClient> _mockClient1 = null!;
    private Mock<IMinioClient> _mockClient2 = null!;
    private Mock<Factories.IMinioClientFactory> _mockFactory = null!;
    private Mock<IVaultOptionsLoader<S3StorageOptions>> _mockVaultLoader = null!;

    #endregion

    #region Properties

    private static object AppSettings =>
        new
        {
            S3 = new
            {
                s3_1 = new
                {
                    ServiceUrl = ServiceUrl1,
                    AccessKey = AccessKey1,
                    SecretKey = SecretKey1,
                    BucketName = Bucket1
                },
                s3_2 = new
                {
                    ServiceUrl = ServiceUrl2,
                    AccessKey = AccessKey2,
                    SecretKey = SecretKey2,
                    BucketName = Bucket2
                }
            },
            AsyncInitializer = new
            {
                Enabled = true,
                Timeout = TimeSpan.FromMinutes(1)
            }
        };

    #endregion

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _mockClient1 = new Mock<IMinioClient>();
        _mockClient2 = new Mock<IMinioClient>();

        _mockFactory = new Mock<Factories.IMinioClientFactory>();
        _mockFactory
            .Setup(x => x.Create(It.Is<S3StorageOptions>(o => o.AccessKey == AccessKey1 && o.ServiceUrl == ServiceUrl1)))
            .Returns(_mockClient1.Object);
        _mockFactory
            .Setup(x => x.Create(It.Is<S3StorageOptions>(o => o.AccessKey == AccessKey2 && o.ServiceUrl == ServiceUrl2)))
            .Returns(_mockClient2.Object);

        _mockVaultLoader = new Mock<IVaultOptionsLoader<S3StorageOptions>>();
        _mockVaultLoader
            .Setup(x => x.LoadAsync(It.IsAny<S3StorageOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((S3StorageOptions opts, CancellationToken _) => opts);

        _sut = await TestHost.Build(
            configureConfiguration: cfg => cfg.AddConfiguration(AppSettings),
            configureServices: (services, configuration) =>
            {
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Debug);
                });

                services.AddSingleton(configuration);

                services.ConfigureAsyncInitializers();

                // Register Minio client providers
                services.AddKeyedMinioClientProviders(configuration)
                    .WithDependency<IMinioWrapper, MinioWrapper>(
                        (svc, key) => svc.AddKeyedAsyncInitializer<MinioWrapper>(key)
                        );

                services.AddSingleton(_mockFactory.Object);
                services.AddSingleton(_mockVaultLoader.Object);
            },
            beforeStart: async host =>
            {
                await host.InvokeAsyncInitializers();
            },
            startHost: false);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _sut.DisposeAsync();
    }

    #endregion

    [Test]
    public void WhenKeyedDependencyResolvedThenShouldResolveWithProperClientProvider()
    {
        // Arrange & Act
        var wrapper1 = _sut.Services.GetRequiredKeyedService<IMinioWrapper>(StorageKey1);
        var provider1 = _sut.Services.GetRequiredKeyedService<IMinioClientProvider>(StorageKey1);

        var wrapper2 = _sut.Services.GetRequiredKeyedService<IMinioWrapper>(StorageKey2);
        var provider2 = _sut.Services.GetRequiredKeyedService<IMinioClientProvider>(StorageKey2);

        // Assert
        wrapper1.ClientProvider.Should().Be(provider1, "Resolved wrapper should reference the correct client provider");
        wrapper1.InitializationCompleted.Should().BeTrue("Wrapper should have completed initialization");

        wrapper2.ClientProvider.Should().Be(provider2, "Resolved wrapper should reference the correct client provider");
        wrapper2.InitializationCompleted.Should().BeTrue("Wrapper should have completed initialization");
    }

    [Test]
    public void WhenResolvingFirstProviderByKeyThenProvider1ShouldNotBeNull()
    {
        var provider1 = _sut.Services.GetKeyedService<IMinioClientProvider>(StorageKey1);
        provider1.Should().NotBeNull("First Minio client provider should be registered");
    }

    [Test]
    public void WhenResolvingSecondProviderByKeyThenProvider2ShouldNotBeNull()
    {
        var provider2 = _sut.Services.GetKeyedService<IMinioClientProvider>(StorageKey2);
        provider2.Should().NotBeNull("Second Minio client provider should be registered");
    }

    [Test]
    public void WhenResolvingClientProvidersThenShouldNotReturnSameInstanceForFirstAndSecond()
    {
        var provider1 = _sut.Services.GetKeyedService<IMinioClientProvider>(StorageKey1);
        var provider2 = _sut.Services.GetKeyedService<IMinioClientProvider>(StorageKey2);

        provider1.Should().NotBe(provider2, "Resolved providers should not reference same instance");
    }

    [Test]
    public void WhenResolvingClientProvidersThenProvidersShouldReferenceDifferentClients()
    {
        var provider1 = _sut.Services.GetRequiredKeyedService<IMinioClientProvider>(StorageKey1);
        var provider2 = _sut.Services.GetRequiredKeyedService<IMinioClientProvider>(StorageKey2);

        var client1 = provider1.Client;
        var client2 = provider2.Client;

        client1.Should().NotBe(client2, "Providers should reference different clients");
        client1.Should().Be(_mockClient1.Object, "First provider should return mock client 1");
        client2.Should().Be(_mockClient2.Object, "Second provider should return mock client 2");
    }

    [Test]
    public void WhenAccessingClient1ThenShouldReturnCorrectClient()
    {
        var provider1 = _sut.Services.GetRequiredKeyedService<IMinioClientProvider>(StorageKey1);
        var client1 = provider1.Client;

        client1.Should().NotBeNull("Client 1 should be accessible");
        client1.Should().Be(_mockClient1.Object, "Should return the first mocked Minio client");
    }

    [Test]
    public void WhenAccessingClient2ThenShouldReturnCorrectClient()
    {
        var provider2 = _sut.Services.GetRequiredKeyedService<IMinioClientProvider>(StorageKey2);
        var client2 = provider2.Client;

        client2.Should().NotBeNull("Client 2 should be accessible");
        client2.Should().Be(_mockClient2.Object, "Should return the second mocked Minio client");
    }

    [Test]
    public void WhenResolvingSameProviderMultipleTimesThenShouldReturnSameInstance()
    {
        var provider1A = _sut.Services.GetRequiredKeyedService<IMinioClientProvider>(StorageKey1);
        var provider1B = _sut.Services.GetRequiredKeyedService<IMinioClientProvider>(StorageKey1);

        provider1A.Should().Be(provider1B, "Should return same provider instance for same key");
    }

    [Test]
    public void WhenFactoryCalledThenShouldBeCalledWithCorrectOptions()
    {
        // Accessing clients should have triggered factory calls during initialization
        _ = _sut.Services.GetRequiredKeyedService<IMinioClientProvider>(StorageKey1).Client;
        _ = _sut.Services.GetRequiredKeyedService<IMinioClientProvider>(StorageKey2).Client;

        _mockFactory.Verify(x => x.Create(It.Is<S3StorageOptions>(o => o.AccessKey == AccessKey1 && o.ServiceUrl == ServiceUrl1)), Times.Once(), "Factory should be called once with options for storage 1");
        _mockFactory.Verify(x => x.Create(It.Is<S3StorageOptions>(o => o.AccessKey == AccessKey2 && o.ServiceUrl == ServiceUrl2)), Times.Once(), "Factory should be called once with options for storage 2");
    }
}

