using AutoFixture;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.Toolbox.Caching.Options;
using Minnaloushe.Core.Toolbox.Caching.Proxy;
using Testcontainers.Redis;

namespace Minnaloushe.Core.Toolbox.Caching.Tests;

[TestFixture]
[Category("Integration")]
public class DistributedCacheProxyTests
{
    private RedisContainer _redisContainer;
    private DistributedCacheProxy _cacheProxy;
    private Fixture _fixture;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Start Redis container
        _redisContainer = new RedisBuilder("registry.minnaloushe.net/redis:8.4.0").Build();

        await _redisContainer.StartAsync();

        // Configure services
        var services = new ServiceCollection();
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = _redisContainer.GetConnectionString();
        });

        services.AddSingleton<IOptions<DistributedCacheOptions>>(provider =>
            Microsoft.Extensions.Options.Options.Create(new DistributedCacheOptions
            {
                SyncLockCount = 10,
                CacheEntityOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                }
            }));

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<DistributedCacheOptions>>();
        var distributedCache = serviceProvider.GetRequiredService<IDistributedCache>();

        // Initialize DistributedCacheProxy
        _cacheProxy = new DistributedCacheProxy(options, distributedCache);
        _fixture = new Fixture();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _redisContainer.StopAsync();
        await _redisContainer.DisposeAsync();
    }

    [Test]
    public async Task GetValueAsync_NoCachedValue_ResponseProducerCalledAndValueStoredInRedis()
    {
        // Arrange
        var request = _fixture.Create<string>();
        var expectedResponse = _fixture.Create<string>();
        var callCount = 0;

        string ResponseProducer(string req, CancellationToken ct)
        {
            callCount++;
            return expectedResponse;
        }

        // Act
        var result = await _cacheProxy.GetValueAsync(request, ResponseProducer, CancellationToken.None);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(expectedResponse), "The result should match the expected response.");
            Assert.That(callCount, Is.EqualTo(1), "The response producer should be called exactly once.");

            // Verify value is stored in Redis
            result = await _cacheProxy.GetValueAsync(request, ResponseProducer, CancellationToken.None);


            Assert.That(result, Is.EqualTo(expectedResponse), "The result should match the expected response.");
            Assert.That(callCount, Is.EqualTo(1), "The response producer should not be called again.");
        }
    }

    [Test]
    public async Task GetValueAsync_CachedValueExists_ResponseProducerNotCalledAndValueReturnedFromRedis()
    {
        // Arrange
        var request = _fixture.Create<string>();
        var expectedResponse = _fixture.Create<string>();

        // Prepopulate Redis with the cached value using DistributedCacheProxy
        await _cacheProxy.GetValueAsync(request, (_, _) => expectedResponse, CancellationToken.None);

        // Act
        var result = await _cacheProxy.GetValueAsync(request, ResponseProducer, CancellationToken.None);

        // Assert
        Assert.That(result, Is.EqualTo(expectedResponse), "The result should match the expected response from the cache.");
        return;

        static string ResponseProducer(string req, CancellationToken ct)
        {
            Assert.Fail("Response producer should not be called when cached value exists.");
            return string.Empty;
        }
    }
}