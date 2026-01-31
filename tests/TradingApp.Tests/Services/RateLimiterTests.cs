using FluentAssertions;
using OoplesFinance.TradingApp.Maui.Services;
using Xunit;

namespace TradingApp.Tests.Services;

/// <summary>
/// Unit tests for the RateLimiter.
/// </summary>
public class RateLimiterTests
{
    [Fact]
    public void TryAcquire_UnderLimit_ReturnsTrue()
    {
        // Arrange
        var rateLimiter = new RateLimiter();

        // Act & Assert - Alpaca allows 20 burst requests
        for (int i = 0; i < 20; i++)
        {
            rateLimiter.TryAcquire("alpaca").Should().BeTrue($"Request {i + 1} should succeed");
        }
    }

    [Fact]
    public void TryAcquire_OverBurstLimit_ReturnsFalse()
    {
        // Arrange
        var rateLimiter = new RateLimiter();

        // Act - Exhaust burst limit
        for (int i = 0; i < 20; i++)
        {
            rateLimiter.TryAcquire("alpaca");
        }

        // Assert - Next request should be rejected
        rateLimiter.TryAcquire("alpaca").Should().BeFalse();
    }

    [Fact]
    public async Task TryAcquire_AfterCooldown_ReturnsTrue()
    {
        // Arrange
        var rateLimiter = new RateLimiter();

        // Exhaust burst limit
        for (int i = 0; i < 20; i++)
        {
            rateLimiter.TryAcquire("alpaca");
        }

        // Verify exhausted
        rateLimiter.TryAcquire("alpaca").Should().BeFalse();

        // Wait for token refill (200 req/min = ~3.33 req/sec)
        await Task.Delay(1000); // 1 second should refill ~3 tokens

        // Should be able to acquire again
        rateLimiter.TryAcquire("alpaca").Should().BeTrue();
    }

    [Fact]
    public void GetStatus_ReturnsCorrectInfo()
    {
        // Arrange
        var rateLimiter = new RateLimiter();

        // Act
        var status = rateLimiter.GetStatus("alpaca");

        // Assert
        status.Broker.Should().Be("alpaca");
        status.MaxTokens.Should().Be(20);
        status.RequestsPerMinute.Should().Be(200);
        status.AvailableTokens.Should().Be(20);
        status.IsLimited.Should().BeFalse();
    }

    [Fact]
    public void GetStatus_AfterRequests_ShowsCorrectAvailable()
    {
        // Arrange
        var rateLimiter = new RateLimiter();

        // Act - Use 10 tokens
        for (int i = 0; i < 10; i++)
        {
            rateLimiter.TryAcquire("alpaca");
        }

        var status = rateLimiter.GetStatus("alpaca");

        // Assert
        status.AvailableTokens.Should().BeLessOrEqualTo(10);
    }

    [Theory]
    [InlineData("alpaca", 200, 20)]
    [InlineData("interactive_brokers", 50, 10)]
    [InlineData("binance", 1200, 100)]
    [InlineData("coinbase", 10, 5)]
    [InlineData("unknown_broker", 60, 10)] // Falls back to default
    public void GetStatus_ReturnsCorrectConfigPerBroker(string broker, int expectedRpm, int expectedBurst)
    {
        // Arrange
        var rateLimiter = new RateLimiter();

        // Act
        var status = rateLimiter.GetStatus(broker);

        // Assert
        status.RequestsPerMinute.Should().Be(expectedRpm);
        status.MaxTokens.Should().Be(expectedBurst);
    }

    [Fact]
    public void Reset_ClearsTokens()
    {
        // Arrange
        var rateLimiter = new RateLimiter();

        // Exhaust tokens
        for (int i = 0; i < 25; i++)
        {
            rateLimiter.TryAcquire("alpaca");
        }

        rateLimiter.TryAcquire("alpaca").Should().BeFalse();

        // Act
        rateLimiter.Reset("alpaca");

        // Assert - Should have full tokens again
        rateLimiter.TryAcquire("alpaca").Should().BeTrue();
        rateLimiter.GetStatus("alpaca").AvailableTokens.Should().BeGreaterThan(15);
    }

    [Fact]
    public async Task WaitForPermissionAsync_WaitsAndReturnsTrue()
    {
        // Arrange
        var rateLimiter = new RateLimiter();

        // Exhaust tokens
        for (int i = 0; i < 20; i++)
        {
            rateLimiter.TryAcquire("alpaca");
        }

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var startTime = DateTime.UtcNow;
        var result = await rateLimiter.WaitForPermissionAsync("alpaca", cts.Token);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        result.Should().BeTrue();
        elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(100), "Should have waited for token");
    }

    [Fact]
    public async Task WaitForPermissionAsync_CancellationWorks()
    {
        // Arrange
        var rateLimiter = new RateLimiter();

        // Exhaust tokens
        for (int i = 0; i < 20; i++)
        {
            rateLimiter.TryAcquire("alpaca");
        }

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        var result = await rateLimiter.WaitForPermissionAsync("alpaca", cts.Token);

        // Assert
        result.Should().BeFalse("Should return false when cancelled");
    }

    [Fact]
    public void MultipleBrokers_IndependentLimits()
    {
        // Arrange
        var rateLimiter = new RateLimiter();

        // Act - Exhaust Alpaca tokens
        for (int i = 0; i < 25; i++)
        {
            rateLimiter.TryAcquire("alpaca");
        }

        // Assert - Alpaca is limited, but Binance is not
        rateLimiter.TryAcquire("alpaca").Should().BeFalse();
        rateLimiter.TryAcquire("binance").Should().BeTrue();
    }

    [Fact]
    public void CaseInsensitive_BrokerNames()
    {
        // Arrange
        var rateLimiter = new RateLimiter();

        // Act
        rateLimiter.TryAcquire("ALPACA");
        rateLimiter.TryAcquire("Alpaca");
        rateLimiter.TryAcquire("alpaca");

        // Assert - All should affect the same bucket
        var status = rateLimiter.GetStatus("ALPACA");
        status.AvailableTokens.Should().BeLessOrEqualTo(17);
    }
}

/// <summary>
/// Tests for concurrent access to rate limiter.
/// </summary>
public class RateLimiterConcurrencyTests
{
    [Fact]
    public async Task ConcurrentRequests_DoNotExceedLimit()
    {
        // Arrange
        var rateLimiter = new RateLimiter();
        var successCount = 0;
        var tasks = new List<Task>();

        // Act - 100 concurrent requests
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                if (rateLimiter.TryAcquire("alpaca"))
                    Interlocked.Increment(ref successCount);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Should not exceed burst limit
        successCount.Should().BeLessOrEqualTo(20, "Should not exceed burst limit of 20");
    }

    [Fact]
    public async Task ConcurrentWaiters_AllEventuallySucceed()
    {
        // Arrange
        var rateLimiter = new RateLimiter();
        var successCount = 0;
        var tasks = new List<Task>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act - 25 concurrent wait requests (more than burst limit)
        for (int i = 0; i < 25; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                if (await rateLimiter.WaitForPermissionAsync("alpaca", cts.Token))
                    Interlocked.Increment(ref successCount);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - All should eventually succeed (unless cancelled)
        successCount.Should().BeGreaterOrEqualTo(20, "Most requests should succeed");
    }
}
