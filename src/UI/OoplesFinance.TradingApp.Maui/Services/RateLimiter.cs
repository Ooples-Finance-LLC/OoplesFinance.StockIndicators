using System.Collections.Concurrent;

namespace OoplesFinance.TradingApp.Maui.Services;

/// <summary>
/// Rate limiter for API calls to prevent hitting rate limits.
/// Implements a sliding window with token bucket algorithm.
/// </summary>
public sealed class RateLimiter : IDisposable
{
    private readonly ConcurrentDictionary<string, RateLimitBucket> _buckets = new();
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    /// <summary>
    /// Default rate limit configurations for common APIs.
    /// </summary>
    private static readonly Dictionary<string, RateLimitConfig> DefaultConfigs = new()
    {
        ["alpaca"] = new(200, TimeSpan.FromMinutes(1), 25),       // 200/min with burst of 25
        ["alpaca_data"] = new(200, TimeSpan.FromMinutes(1), 50),  // Market data endpoint
        ["binance"] = new(1200, TimeSpan.FromMinutes(1), 100),    // 1200/min with burst of 100
        ["supabase"] = new(500, TimeSpan.FromSeconds(1), 50),     // 500/sec with burst of 50
        ["default"] = new(100, TimeSpan.FromMinutes(1), 10)       // Conservative default
    };

    public RateLimiter()
    {
        // Cleanup old entries every 5 minutes
        _cleanupTimer = new Timer(
            CleanupOldEntries,
            null,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Attempts to acquire a permit for the specified API.
    /// Returns true if the request can proceed, false if rate limited.
    /// </summary>
    public bool TryAcquire(string apiName)
    {
        var config = GetConfig(apiName);
        var bucket = _buckets.GetOrAdd(apiName, _ => new RateLimitBucket(config));
        return bucket.TryAcquire();
    }

    /// <summary>
    /// Waits until a permit is available for the specified API.
    /// </summary>
    public async Task<bool> WaitForPermitAsync(string apiName, CancellationToken cancellationToken = default)
    {
        var config = GetConfig(apiName);
        var bucket = _buckets.GetOrAdd(apiName, _ => new RateLimitBucket(config));
        return await bucket.WaitForPermitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the time until the next permit will be available.
    /// </summary>
    public TimeSpan? GetWaitTime(string apiName)
    {
        if (_buckets.TryGetValue(apiName, out var bucket))
        {
            return bucket.GetWaitTime();
        }
        return null;
    }

    /// <summary>
    /// Gets current rate limit statistics for an API.
    /// </summary>
    public RateLimitStats GetStats(string apiName)
    {
        if (_buckets.TryGetValue(apiName, out var bucket))
        {
            return bucket.GetStats();
        }
        return new RateLimitStats(0, 0, 0, TimeSpan.Zero);
    }

    /// <summary>
    /// Registers a custom rate limit configuration for an API.
    /// </summary>
    public void RegisterConfig(string apiName, int maxRequests, TimeSpan window, int burstLimit)
    {
        var config = new RateLimitConfig(maxRequests, window, burstLimit);
        _buckets.AddOrUpdate(apiName,
            _ => new RateLimitBucket(config),
            (_, existing) =>
            {
                // Replace with new bucket using new config
                return new RateLimitBucket(config);
            });
    }

    private static RateLimitConfig GetConfig(string apiName)
    {
        return DefaultConfigs.TryGetValue(apiName.ToLowerInvariant(), out var config)
            ? config
            : DefaultConfigs["default"];
    }

    private void CleanupOldEntries(object? state)
    {
        var expiredApis = _buckets
            .Where(kvp => kvp.Value.IsExpired())
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var api in expiredApis)
        {
            _buckets.TryRemove(api, out _);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cleanupTimer.Dispose();
    }
}

/// <summary>
/// Configuration for rate limiting.
/// </summary>
public record RateLimitConfig(int MaxRequests, TimeSpan Window, int BurstLimit);

/// <summary>
/// Statistics about rate limiting for an API.
/// </summary>
public record RateLimitStats(int RequestsInWindow, int AvailableTokens, int RejectedCount, TimeSpan WindowRemaining);

/// <summary>
/// Token bucket implementation for rate limiting.
/// </summary>
internal sealed class RateLimitBucket
{
    private readonly RateLimitConfig _config;
    private readonly Queue<DateTime> _requestTimes = new();
    private readonly object _lock = new();
    private int _availableTokens;
    private DateTime _lastRefill;
    private DateTime _lastActivity;
    private int _rejectedCount;

    public RateLimitBucket(RateLimitConfig config)
    {
        _config = config;
        _availableTokens = config.BurstLimit;
        _lastRefill = DateTime.UtcNow;
        _lastActivity = DateTime.UtcNow;
    }

    public bool TryAcquire()
    {
        lock (_lock)
        {
            RefillTokens();
            CleanOldRequests();

            _lastActivity = DateTime.UtcNow;

            // Check sliding window
            if (_requestTimes.Count >= _config.MaxRequests)
            {
                _rejectedCount++;
                return false;
            }

            // Check burst limit
            if (_availableTokens <= 0)
            {
                _rejectedCount++;
                return false;
            }

            // Grant permit
            _requestTimes.Enqueue(DateTime.UtcNow);
            _availableTokens--;
            return true;
        }
    }

    public async Task<bool> WaitForPermitAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (TryAcquire())
                return true;

            var waitTime = GetWaitTime() ?? TimeSpan.FromMilliseconds(100);
            await Task.Delay(waitTime, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    public TimeSpan? GetWaitTime()
    {
        lock (_lock)
        {
            if (_requestTimes.Count == 0)
                return null;

            var oldestRequest = _requestTimes.Peek();
            var windowEnd = oldestRequest.Add(_config.Window);
            var wait = windowEnd - DateTime.UtcNow;

            return wait > TimeSpan.Zero ? wait : null;
        }
    }

    public RateLimitStats GetStats()
    {
        lock (_lock)
        {
            CleanOldRequests();

            var windowRemaining = TimeSpan.Zero;
            if (_requestTimes.Count > 0)
            {
                var oldestRequest = _requestTimes.Peek();
                windowRemaining = oldestRequest.Add(_config.Window) - DateTime.UtcNow;
                if (windowRemaining < TimeSpan.Zero)
                    windowRemaining = TimeSpan.Zero;
            }

            return new RateLimitStats(
                _requestTimes.Count,
                _availableTokens,
                _rejectedCount,
                windowRemaining);
        }
    }

    public bool IsExpired()
    {
        lock (_lock)
        {
            return DateTime.UtcNow - _lastActivity > TimeSpan.FromMinutes(30);
        }
    }

    private void RefillTokens()
    {
        var now = DateTime.UtcNow;
        var elapsed = now - _lastRefill;

        // Calculate how many tokens to add based on elapsed time
        var tokensPerMs = (double)_config.BurstLimit / _config.Window.TotalMilliseconds;
        var tokensToAdd = (int)(elapsed.TotalMilliseconds * tokensPerMs);

        if (tokensToAdd > 0)
        {
            _availableTokens = Math.Min(_config.BurstLimit, _availableTokens + tokensToAdd);
            _lastRefill = now;
        }
    }

    private void CleanOldRequests()
    {
        var cutoff = DateTime.UtcNow - _config.Window;
        while (_requestTimes.Count > 0 && _requestTimes.Peek() < cutoff)
        {
            _requestTimes.Dequeue();
        }
    }
}

/// <summary>
/// Extension methods for rate limiting in HTTP clients.
/// </summary>
public static class RateLimiterExtensions
{
    /// <summary>
    /// Executes an HTTP request with rate limiting.
    /// </summary>
    public static async Task<T> ExecuteWithRateLimitAsync<T>(
        this RateLimiter rateLimiter,
        string apiName,
        Func<Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        await rateLimiter.WaitForPermitAsync(apiName, cancellationToken).ConfigureAwait(false);
        return await operation().ConfigureAwait(false);
    }

    /// <summary>
    /// Executes an HTTP request with rate limiting and retry on 429.
    /// </summary>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        this RateLimiter rateLimiter,
        string apiName,
        Func<Task<T>> operation,
        int maxRetries = 3,
        CancellationToken cancellationToken = default)
    {
        var retryCount = 0;

        while (true)
        {
            await rateLimiter.WaitForPermitAsync(apiName, cancellationToken).ConfigureAwait(false);

            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                retryCount++;
                if (retryCount >= maxRetries)
                    throw;

                // Exponential backoff
                var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount));
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
