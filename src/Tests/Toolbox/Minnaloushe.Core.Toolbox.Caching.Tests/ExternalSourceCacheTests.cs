using AutoFixture;
using Microsoft.Extensions.Caching.Distributed;
using Minnaloushe.Core.Toolbox.Caching.ExternalSourceCache;
using Moq;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Minnaloushe.Core.Toolbox.Caching.Tests;

public class ExternalSourceCacheTests
{
    private readonly Mock<IDistributedCache> _cache = new();
    private readonly Dictionary<string, byte[]> _cacheStorage = [];
    private readonly Mock<IDictionary<string, string>> _externalSourceMock = new();
    private readonly Fixture _fixture = new();
    private string _key;

    private Mock<ILogger> _loggerMock;
    private IExternalSourceCache<string, string> _sut;
    private string? _value;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger>();
        _sut = new ExternalCacheSutTest(_cache.Object, _loggerMock.Object, _externalSourceMock.Object);

        _key = _fixture.Create<string>();
        _value = _fixture.Create<string>();

        _externalSourceMock.Setup(x => x.TryGetValue(It.Is<string>(i => i == _key), out _value))
            .Returns(true);


        _cache.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) => _cacheStorage.GetValueOrDefault(key));
        _cache.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback((string key, byte[] val, DistributedCacheEntryOptions _, CancellationToken _) =>
                _cacheStorage[key] = val);
    }

    [Test]
    public async Task GetValue_WhenAccessingSameKey_ShouldCallForExternalSourceOnlyOnce()
    {
        var val = await _sut.TryGetAsync(_key, CancellationToken.None);

        Assert.That(val, Is.EqualTo(_value));

        string? stub;

        _externalSourceMock.Verify(x => x.TryGetValue(It.IsAny<string>(), out stub), Times.Once());

        var val2 = await _sut.TryGetAsync(_key, CancellationToken.None);

        Assert.That(val2, Is.EqualTo(_value));

        _externalSourceMock.Verify(x => x.TryGetValue(It.IsAny<string>(), out stub), Times.Once());
    }
}