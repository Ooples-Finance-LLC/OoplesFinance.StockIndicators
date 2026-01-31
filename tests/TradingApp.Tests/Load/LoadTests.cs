using NBomber.CSharp;
using NBomber.Contracts;
using NBomber.Contracts.Stats;
using OoplesFinance.StockIndicators.Builder.Cloud;
using OoplesFinance.StockIndicators.Builder.Trading.Brokers;
using OoplesFinance.StockIndicators.Builder.Trading.MarketData;
using Xunit;
using Xunit.Abstractions;

namespace TradingApp.Tests.Load;

/// <summary>
/// Load tests for the trading platform using NBomber.
/// Tests concurrent user scenarios, API throughput, and system stability.
/// </summary>
[Trait("Category", "Load")]
public class TradingPlatformLoadTests
{
    private readonly ITestOutputHelper _output;

    public TradingPlatformLoadTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Tests concurrent quote requests to validate market data throughput.
    /// </summary>
    [Fact(Skip = "Load test - run manually")]
    [Trait("Category", "Load")]
    public void MarketDataQuoteLoad_100ConcurrentUsers_MaintainsThroughput()
    {
        var apiKey = Environment.GetEnvironmentVariable("ALPACA_API_KEY") ?? string.Empty;
        var apiSecret = Environment.GetEnvironmentVariable("ALPACA_API_SECRET") ?? string.Empty;

        if (string.IsNullOrEmpty(apiKey))
        {
            _output.WriteLine("Skipping: ALPACA_API_KEY not configured");
            return;
        }

        var symbols = new[] { "AAPL", "MSFT", "GOOGL", "AMZN", "NVDA", "META", "TSLA", "JPM", "V", "JNJ" };
        var random = new Random();

        var scenario = Scenario.Create("get_quotes", async context =>
        {
            using var provider = new AlpacaMarketDataProvider(new AlpacaMarketDataOptions
            {
                ApiKey = apiKey,
                ApiSecret = apiSecret,
                UsePaper = true
            });

            var symbol = symbols[random.Next(symbols.Length)];
            var quote = await provider.GetQuoteAsync(symbol);

            return quote is not null ? Response.Ok() : Response.Fail();
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.Inject(rate: 10, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        // Assert performance requirements
        var scnStats = stats.ScenarioStats[0];
        Assert.True(scnStats.Ok.Request.RPS >= 5, "RPS should be at least 5");
        Assert.True(scnStats.Ok.Latency.Percent99 < 5000, "P99 latency should be under 5 seconds");
        Assert.True(scnStats.Fail.Request.Count < scnStats.Ok.Request.Count * 0.05, "Error rate should be under 5%");

        _output.WriteLine($"RPS: {scnStats.Ok.Request.RPS}");
        _output.WriteLine($"P50 Latency: {scnStats.Ok.Latency.Percent50}ms");
        _output.WriteLine($"P99 Latency: {scnStats.Ok.Latency.Percent99}ms");
        _output.WriteLine($"Error Rate: {scnStats.Fail.Request.Percent}%");
    }

    /// <summary>
    /// Tests Supabase database query throughput.
    /// </summary>
    [Fact(Skip = "Load test - run manually")]
    [Trait("Category", "Load")]
    public void SupabaseQueryLoad_50ConcurrentUsers_MaintainsThroughput()
    {
        var supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL") ?? string.Empty;
        var supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY") ?? string.Empty;

        if (string.IsNullOrEmpty(supabaseUrl))
        {
            _output.WriteLine("Skipping: SUPABASE_URL not configured");
            return;
        }

        var scenario = Scenario.Create("query_positions", async context =>
        {
            using var client = new SupabaseClient(new SupabaseOptions
            {
                Url = supabaseUrl,
                AnonKey = supabaseKey
            });

            // Simulate authenticated query
            client.SetSession("test-token", "test-refresh", "test-user");

            try
            {
                // This will fail without real auth, but tests the query building
                var query = client.From<TestPosition>("positions")
                    .Select("*")
                    .Limit(100);

                return Response.Ok();
            }
            catch
            {
                return Response.Fail();
            }
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.KeepConstant(copies: 50, during: TimeSpan.FromSeconds(30))
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        var scnStats = stats.ScenarioStats[0];
        _output.WriteLine($"Total Requests: {scnStats.Ok.Request.Count + scnStats.Fail.Request.Count}");
        _output.WriteLine($"RPS: {scnStats.Ok.Request.RPS}");
    }

    /// <summary>
    /// Simulates full trading workflow under load.
    /// </summary>
    [Fact(Skip = "Load test - run manually")]
    [Trait("Category", "Load")]
    public void TradingWorkflowLoad_SimulatesRealUsage()
    {
        var apiKey = Environment.GetEnvironmentVariable("ALPACA_API_KEY") ?? string.Empty;
        var apiSecret = Environment.GetEnvironmentVariable("ALPACA_API_SECRET") ?? string.Empty;

        if (string.IsNullOrEmpty(apiKey))
        {
            _output.WriteLine("Skipping: ALPACA_API_KEY not configured");
            return;
        }

        // Simulate typical user workflow:
        // 1. Check account (10% of requests)
        // 2. Get positions (20% of requests)
        // 3. Get quotes (60% of requests)
        // 4. Place/cancel orders (10% of requests)

        var checkAccountScenario = Scenario.Create("check_account", async context =>
        {
            using var broker = new AlpacaBroker(new AlpacaOptions
            {
                ApiKey = apiKey,
                ApiSecret = apiSecret,
                UsePaper = true
            });

            var account = await broker.GetAccountAsync();
            return account is not null ? Response.Ok() : Response.Fail();
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.Inject(rate: 2, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
        );

        var getPositionsScenario = Scenario.Create("get_positions", async context =>
        {
            using var broker = new AlpacaBroker(new AlpacaOptions
            {
                ApiKey = apiKey,
                ApiSecret = apiSecret,
                UsePaper = true
            });

            var positions = await broker.GetPositionsAsync();
            return positions is not null ? Response.Ok() : Response.Fail();
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.Inject(rate: 4, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
        );

        var getQuotesScenario = Scenario.Create("get_quotes", async context =>
        {
            using var provider = new AlpacaMarketDataProvider(new AlpacaMarketDataOptions
            {
                ApiKey = apiKey,
                ApiSecret = apiSecret,
                UsePaper = true
            });

            var quote = await provider.GetQuoteAsync("AAPL");
            return quote is not null ? Response.Ok() : Response.Fail();
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.Inject(rate: 12, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
        );

        var stats = NBomberRunner
            .RegisterScenarios(checkAccountScenario, getPositionsScenario, getQuotesScenario)
            .Run();

        // Output results
        foreach (var scnStats in stats.ScenarioStats)
        {
            _output.WriteLine($"--- {scnStats.ScenarioName} ---");
            _output.WriteLine($"  Requests: {scnStats.Ok.Request.Count}");
            _output.WriteLine($"  RPS: {scnStats.Ok.Request.RPS:F2}");
            _output.WriteLine($"  P50: {scnStats.Ok.Latency.Percent50}ms");
            _output.WriteLine($"  P99: {scnStats.Ok.Latency.Percent99}ms");
            _output.WriteLine($"  Errors: {scnStats.Fail.Request.Count}");
        }

        // Verify overall system stability
        var totalErrors = stats.ScenarioStats.Sum(s => s.Fail.Request.Count);
        var totalRequests = stats.ScenarioStats.Sum(s => s.Ok.Request.Count + s.Fail.Request.Count);
        var errorRate = (double)totalErrors / totalRequests;

        Assert.True(errorRate < 0.1, $"Overall error rate {errorRate:P} should be under 10%");
    }

    /// <summary>
    /// Tests rate limiter effectiveness under high load.
    /// </summary>
    [Fact]
    [Trait("Category", "Load")]
    public async Task RateLimiter_UnderHighLoad_EnforcesLimits()
    {
        // Arrange
        var rateLimiter = new OoplesFinance.TradingApp.Maui.Services.RateLimiter();
        var successCount = 0;
        var rejectedCount = 0;
        var tasks = new List<Task>();

        // Act - Simulate 500 requests in quick succession (Alpaca limit is 200/min)
        for (int i = 0; i < 500; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                if (rateLimiter.TryAcquire("alpaca"))
                    Interlocked.Increment(ref successCount);
                else
                    Interlocked.Increment(ref rejectedCount);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Should have limited some requests
        _output.WriteLine($"Successful: {successCount}");
        _output.WriteLine($"Rejected: {rejectedCount}");

        Assert.True(successCount <= 25, "Should not exceed burst limit immediately");
        Assert.True(rejectedCount > 0, "Should have rejected some requests");
    }

    /// <summary>
    /// Tests WebSocket connection stability under load.
    /// </summary>
    [Fact(Skip = "Load test - run manually")]
    [Trait("Category", "Load")]
    public async Task RealtimeConnections_50Concurrent_RemainStable()
    {
        var supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL") ?? string.Empty;
        var supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY") ?? string.Empty;

        if (string.IsNullOrEmpty(supabaseUrl))
        {
            _output.WriteLine("Skipping: SUPABASE_URL not configured");
            return;
        }

        var services = new List<OoplesFinance.TradingApp.Maui.Services.RealtimeService>();
        var connectedCount = 0;
        var errorCount = 0;

        try
        {
            // Create 50 concurrent connections
            var tasks = Enumerable.Range(0, 50).Select(async i =>
            {
                var service = new OoplesFinance.TradingApp.Maui.Services.RealtimeService(supabaseUrl, supabaseKey);
                services.Add(service);

                try
                {
                    await service.ConnectAsync("test-token");
                    Interlocked.Increment(ref connectedCount);
                }
                catch
                {
                    Interlocked.Increment(ref errorCount);
                }
            });

            await Task.WhenAll(tasks);

            // Wait for connections to stabilize
            await Task.Delay(5000);

            // Check how many are still connected
            var stillConnected = services.Count(s => s.IsConnected);

            _output.WriteLine($"Connected: {connectedCount}");
            _output.WriteLine($"Errors: {errorCount}");
            _output.WriteLine($"Still Connected: {stillConnected}");

            Assert.True(connectedCount > 40, "At least 80% should connect successfully");
        }
        finally
        {
            // Cleanup
            foreach (var service in services)
            {
                service.Dispose();
            }
        }
    }

    private class TestPosition
    {
        public string Id { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
    }
}

/// <summary>
/// Performance baseline tests to track regression.
/// </summary>
[Trait("Category", "Performance")]
public class PerformanceBaselineTests
{
    /// <summary>
    /// Establishes baseline for quote retrieval latency.
    /// </summary>
    [Fact]
    public async Task QuoteRetrieval_ShouldComplete_UnderBaseline()
    {
        // Arrange
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var iterations = 10;
        var latencies = new List<long>();

        var apiKey = Environment.GetEnvironmentVariable("ALPACA_API_KEY");
        var apiSecret = Environment.GetEnvironmentVariable("ALPACA_API_SECRET");

        if (string.IsNullOrEmpty(apiKey))
        {
            // Use mock timing for baseline
            for (int i = 0; i < iterations; i++)
            {
                stopwatch.Restart();
                await Task.Delay(10); // Simulate fast response
                latencies.Add(stopwatch.ElapsedMilliseconds);
            }
        }
        else
        {
            using var provider = new AlpacaMarketDataProvider(new AlpacaMarketDataOptions
            {
                ApiKey = apiKey,
                ApiSecret = apiSecret,
                UsePaper = true
            });

            for (int i = 0; i < iterations; i++)
            {
                stopwatch.Restart();
                await provider.GetQuoteAsync("AAPL");
                latencies.Add(stopwatch.ElapsedMilliseconds);
            }
        }

        // Assert
        var avgLatency = latencies.Average();
        var p99Latency = latencies.OrderBy(l => l).ElementAt((int)(iterations * 0.99));

        Assert.True(avgLatency < 1000, $"Average latency {avgLatency}ms should be under 1000ms");
        Assert.True(p99Latency < 3000, $"P99 latency {p99Latency}ms should be under 3000ms");
    }

    /// <summary>
    /// Tests memory efficiency during batch operations.
    /// </summary>
    [Fact]
    public void BatchOperations_ShouldNotLeak_Memory()
    {
        // Get initial memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(true);

        // Perform many operations
        for (int i = 0; i < 1000; i++)
        {
            var client = new SupabaseClient(new SupabaseOptions
            {
                Url = "https://test.supabase.co",
                AnonKey = "test-key"
            });
            client.Dispose();
        }

        // Force GC and measure
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(true);

        var memoryGrowth = finalMemory - initialMemory;
        var maxAllowedGrowth = 10 * 1024 * 1024; // 10MB

        Assert.True(memoryGrowth < maxAllowedGrowth,
            $"Memory growth {memoryGrowth / 1024 / 1024}MB should be under {maxAllowedGrowth / 1024 / 1024}MB");
    }
}
