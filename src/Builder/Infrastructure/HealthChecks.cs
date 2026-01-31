using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime;

namespace OoplesFinance.StockIndicators.Builder.Infrastructure;

/// <summary>
/// Comprehensive health check system with self-healing capabilities.
/// Monitors application health, dependencies, and resources.
/// </summary>
public sealed class HealthCheckService : IDisposable
{
    private readonly HealthCheckOptions _options;
    private readonly ConcurrentDictionary<string, IHealthCheck> _healthChecks = new();
    private readonly ConcurrentDictionary<string, HealthCheckResult> _lastResults = new();
    private readonly ConcurrentDictionary<string, int> _consecutiveFailures = new();
    private readonly Timer _checkTimer;
    private readonly SemaphoreSlim _checkLock = new(1, 1);
    private OverallHealthStatus _overallStatus = OverallHealthStatus.Unknown;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the HealthCheckService.
    /// </summary>
    public HealthCheckService(HealthCheckOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        // Register built-in health checks
        RegisterBuiltInChecks();

        // Start health check timer
        _checkTimer = new Timer(
            RunHealthChecks,
            null,
            TimeSpan.FromSeconds(5),
            _options.CheckInterval);
    }

    /// <summary>
    /// Registers a custom health check.
    /// </summary>
    public void RegisterHealthCheck(string name, IHealthCheck healthCheck)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty", nameof(name));

        _healthChecks[name] = healthCheck ?? throw new ArgumentNullException(nameof(healthCheck));
        _consecutiveFailures[name] = 0;
    }

    /// <summary>
    /// Unregisters a health check.
    /// </summary>
    public void UnregisterHealthCheck(string name)
    {
        _healthChecks.TryRemove(name, out _);
        _lastResults.TryRemove(name, out _);
        _consecutiveFailures.TryRemove(name, out _);
    }

    /// <summary>
    /// Runs all health checks immediately.
    /// </summary>
    public async Task<HealthReport> CheckHealthAsync(CancellationToken ct = default)
    {
        await _checkLock.WaitAsync(ct);

        try
        {
            var results = new Dictionary<string, HealthCheckResult>();
            var sw = Stopwatch.StartNew();

            foreach (var kvp in _healthChecks)
            {
                var name = kvp.Key;
                var check = kvp.Value;
                try
                {
                    using var timeout = new CancellationTokenSource(_options.CheckTimeout);
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);

                    var checkSw = Stopwatch.StartNew();
                    var result = await check.CheckHealthAsync(linked.Token);
                    checkSw.Stop();

                    result.Duration = checkSw.Elapsed;
                    results[name] = result;
                    _lastResults[name] = result;

                    // Track consecutive failures
                    if (result.Status == HealthStatus.Unhealthy)
                    {
                        _consecutiveFailures.AddOrUpdate(name, 1, (_, v) => v + 1);

                        // Trigger self-healing if threshold exceeded
                        if (_consecutiveFailures[name] >= _options.SelfHealingThreshold)
                        {
                            await TriggerSelfHealingAsync(name, check, result, ct);
                        }
                    }
                    else
                    {
                        _consecutiveFailures[name] = 0;
                    }
                }
                catch (OperationCanceledException)
                {
                    results[name] = new HealthCheckResult
                    {
                        Status = HealthStatus.Unhealthy,
                        Description = "Health check timed out",
                        Duration = _options.CheckTimeout
                    };
                    _lastResults[name] = results[name];
                    _consecutiveFailures.AddOrUpdate(name, 1, (_, v) => v + 1);
                }
                catch (Exception ex)
                {
                    results[name] = new HealthCheckResult
                    {
                        Status = HealthStatus.Unhealthy,
                        Description = $"Health check failed: {ex.Message}",
                        Exception = ex
                    };
                    _lastResults[name] = results[name];
                    _consecutiveFailures.AddOrUpdate(name, 1, (_, v) => v + 1);
                }
            }

            sw.Stop();

            // Determine overall status
            _overallStatus = DetermineOverallStatus(results);

            var report = new HealthReport
            {
                Status = _overallStatus,
                Results = results,
                TotalDuration = sw.Elapsed,
                Timestamp = DateTime.UtcNow
            };

            OnHealthCheckCompleted?.Invoke(this, new HealthCheckCompletedEventArgs { Report = report });

            return report;
        }
        finally
        {
            _checkLock.Release();
        }
    }

    /// <summary>
    /// Gets the current health status.
    /// </summary>
    public OverallHealthStatus GetCurrentStatus() => _overallStatus;

    /// <summary>
    /// Gets the last result for a specific check.
    /// </summary>
    public HealthCheckResult? GetLastResult(string checkName)
    {
        return _lastResults.TryGetValue(checkName, out var result) ? result : null;
    }

    /// <summary>
    /// Gets all last results.
    /// </summary>
    public IReadOnlyDictionary<string, HealthCheckResult> GetAllLastResults()
    {
        return new Dictionary<string, HealthCheckResult>(_lastResults);
    }

    /// <summary>
    /// Event raised when health check completes.
    /// </summary>
    public event EventHandler<HealthCheckCompletedEventArgs>? OnHealthCheckCompleted;

    /// <summary>
    /// Event raised when self-healing is triggered.
    /// </summary>
    public event EventHandler<SelfHealingEventArgs>? OnSelfHealingTriggered;

    /// <summary>
    /// Event raised when status changes.
    /// </summary>
    public event EventHandler<HealthStatusChangedEventArgs>? OnStatusChanged;

    private void RegisterBuiltInChecks()
    {
        if (_options.EnableMemoryCheck)
        {
            RegisterHealthCheck("memory", new MemoryHealthCheck(_options.MemoryThresholdMB));
        }

        if (_options.EnableGCCheck)
        {
            RegisterHealthCheck("gc", new GarbageCollectionHealthCheck(
                _options.MaxGen2Collections,
                _options.CheckInterval));
        }

        if (_options.EnableThreadPoolCheck)
        {
            RegisterHealthCheck("threadpool", new ThreadPoolHealthCheck(
                _options.MinAvailableWorkerThreads,
                _options.MinAvailableIOThreads));
        }

        if (_options.EnableDiskSpaceCheck && _options.DiskPath is { Length: > 0 } diskPath)
        {
            RegisterHealthCheck("disk", new DiskSpaceHealthCheck(
                diskPath,
                _options.MinFreeDiskSpaceMB));
        }
    }

    private async void RunHealthChecks(object? state)
    {
        if (_disposed) return;

        try
        {
            await CheckHealthAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Log but don't crash
            Debug.WriteLine($"Health check error: {ex.Message}");
        }
    }

    private OverallHealthStatus DetermineOverallStatus(Dictionary<string, HealthCheckResult> results)
    {
        if (results.Count == 0)
        {
            return OverallHealthStatus.Unknown;
        }

        var unhealthyCount = results.Values.Count(r => r.Status == HealthStatus.Unhealthy);
        var degradedCount = results.Values.Count(r => r.Status == HealthStatus.Degraded);
        var criticalChecks = _options.CriticalChecks ?? [];

        // Check if any critical check is unhealthy
        foreach (var critical in criticalChecks)
        {
            if (results.TryGetValue(critical, out var result) && result.Status == HealthStatus.Unhealthy)
            {
                var oldStatus = _overallStatus;
                if (oldStatus != OverallHealthStatus.Unhealthy)
                {
                    OnStatusChanged?.Invoke(this, new HealthStatusChangedEventArgs
                    {
                        OldStatus = oldStatus,
                        NewStatus = OverallHealthStatus.Unhealthy,
                        Reason = $"Critical check '{critical}' is unhealthy"
                    });
                }
                return OverallHealthStatus.Unhealthy;
            }
        }

        if (unhealthyCount > 0)
        {
            var threshold = _options.UnhealthyThresholdPercent / 100.0 * results.Count;
            if (unhealthyCount >= threshold)
            {
                return OverallHealthStatus.Unhealthy;
            }
            return OverallHealthStatus.Degraded;
        }

        if (degradedCount > 0)
        {
            return OverallHealthStatus.Degraded;
        }

        return OverallHealthStatus.Healthy;
    }

    private async Task TriggerSelfHealingAsync(
        string checkName,
        IHealthCheck check,
        HealthCheckResult result,
        CancellationToken ct)
    {
        if (check is not ISelfHealingHealthCheck selfHealing)
        {
            return;
        }

        OnSelfHealingTriggered?.Invoke(this, new SelfHealingEventArgs
        {
            CheckName = checkName,
            FailureCount = _consecutiveFailures[checkName],
            LastResult = result,
            TriggeredAt = DateTime.UtcNow
        });

        try
        {
            var healed = await selfHealing.AttemptHealingAsync(ct);

            if (healed)
            {
                _consecutiveFailures[checkName] = 0;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Self-healing failed for {checkName}: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _checkTimer.Dispose();
        _checkLock.Dispose();

        foreach (var check in _healthChecks.Values)
        {
            if (check is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}

/// <summary>
/// Health check options.
/// </summary>
public sealed class HealthCheckOptions
{
    /// <summary>Interval between health checks.</summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Timeout for individual health checks.</summary>
    public TimeSpan CheckTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Number of consecutive failures before self-healing.</summary>
    public int SelfHealingThreshold { get; set; } = 3;

    /// <summary>Percentage of unhealthy checks to mark overall unhealthy.</summary>
    public int UnhealthyThresholdPercent { get; set; } = 50;

    /// <summary>Names of critical health checks.</summary>
    public List<string>? CriticalChecks { get; set; }

    /// <summary>Enable memory health check.</summary>
    public bool EnableMemoryCheck { get; set; } = true;

    /// <summary>Memory threshold in MB before degraded.</summary>
    public long MemoryThresholdMB { get; set; } = 1024;

    /// <summary>Enable GC health check.</summary>
    public bool EnableGCCheck { get; set; } = true;

    /// <summary>Max Gen2 collections per interval.</summary>
    public int MaxGen2Collections { get; set; } = 5;

    /// <summary>Enable thread pool health check.</summary>
    public bool EnableThreadPoolCheck { get; set; } = true;

    /// <summary>Minimum available worker threads.</summary>
    public int MinAvailableWorkerThreads { get; set; } = 10;

    /// <summary>Minimum available IO threads.</summary>
    public int MinAvailableIOThreads { get; set; } = 10;

    /// <summary>Enable disk space health check.</summary>
    public bool EnableDiskSpaceCheck { get; set; } = false;

    /// <summary>Path to check for disk space.</summary>
    public string? DiskPath { get; set; }

    /// <summary>Minimum free disk space in MB.</summary>
    public long MinFreeDiskSpaceMB { get; set; } = 1024;
}

/// <summary>
/// Health check interface.
/// </summary>
public interface IHealthCheck
{
    Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct = default);
}

/// <summary>
/// Self-healing health check interface.
/// </summary>
public interface ISelfHealingHealthCheck : IHealthCheck
{
    Task<bool> AttemptHealingAsync(CancellationToken ct = default);
}

/// <summary>
/// Health check result.
/// </summary>
public sealed class HealthCheckResult
{
    public HealthStatus Status { get; set; }
    public string? Description { get; set; }
    public TimeSpan Duration { get; set; }
    public Exception? Exception { get; set; }
    public Dictionary<string, object> Data { get; set; } = [];
}

/// <summary>
/// Health status.
/// </summary>
public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}

/// <summary>
/// Overall health status.
/// </summary>
public enum OverallHealthStatus
{
    Unknown,
    Healthy,
    Degraded,
    Unhealthy
}

/// <summary>
/// Health report.
/// </summary>
public sealed class HealthReport
{
    public OverallHealthStatus Status { get; set; }
    public Dictionary<string, HealthCheckResult> Results { get; set; } = [];
    public TimeSpan TotalDuration { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Health check completed event args.
/// </summary>
public sealed class HealthCheckCompletedEventArgs : EventArgs
{
    public HealthReport Report { get; set; } = new();
}

/// <summary>
/// Self-healing event args.
/// </summary>
public sealed class SelfHealingEventArgs : EventArgs
{
    public string CheckName { get; set; } = string.Empty;
    public int FailureCount { get; set; }
    public HealthCheckResult LastResult { get; set; } = new();
    public DateTime TriggeredAt { get; set; }
}

/// <summary>
/// Health status changed event args.
/// </summary>
public sealed class HealthStatusChangedEventArgs : EventArgs
{
    public OverallHealthStatus OldStatus { get; set; }
    public OverallHealthStatus NewStatus { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Memory health check.
/// </summary>
public sealed class MemoryHealthCheck : IHealthCheck, ISelfHealingHealthCheck
{
    private readonly long _thresholdMB;

    public MemoryHealthCheck(long thresholdMB)
    {
        _thresholdMB = thresholdMB;
    }

    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct = default)
    {
        var workingSet = Environment.WorkingSet;
        var workingSetMB = workingSet / (1024 * 1024);
        var gcMemory = GC.GetTotalMemory(false);
        var gcMemoryMB = gcMemory / (1024 * 1024);

        var status = workingSetMB < _thresholdMB * 0.8
            ? HealthStatus.Healthy
            : workingSetMB < _thresholdMB
                ? HealthStatus.Degraded
                : HealthStatus.Unhealthy;

        return Task.FromResult(new HealthCheckResult
        {
            Status = status,
            Description = $"Working set: {workingSetMB} MB, GC memory: {gcMemoryMB} MB",
            Data = new Dictionary<string, object>
            {
                ["workingSetMB"] = workingSetMB,
                ["gcMemoryMB"] = gcMemoryMB,
                ["thresholdMB"] = _thresholdMB
            }
        });
    }

    public Task<bool> AttemptHealingAsync(CancellationToken ct = default)
    {
        // Force garbage collection
#if NET461
        GC.Collect(2, GCCollectionMode.Forced, true, true);
#else
        GC.Collect(2, GCCollectionMode.Aggressive, true, true);
#endif
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Request LOH compaction
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;

        return Task.FromResult(true);
    }
}

/// <summary>
/// Garbage collection health check.
/// </summary>
public sealed class GarbageCollectionHealthCheck : IHealthCheck
{
    private readonly int _maxGen2Collections;
    private readonly TimeSpan _interval;
    private int _lastGen2Count;
    private DateTime _lastCheck = DateTime.UtcNow;

    public GarbageCollectionHealthCheck(int maxGen2Collections, TimeSpan interval)
    {
        _maxGen2Collections = maxGen2Collections;
        _interval = interval;
        _lastGen2Count = GC.CollectionCount(2);
    }

    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct = default)
    {
        var currentGen2 = GC.CollectionCount(2);
        var gen2Delta = currentGen2 - _lastGen2Count;
        var elapsed = DateTime.UtcNow - _lastCheck;

        // Calculate collections per interval
        var collectionsPerInterval = elapsed.TotalMilliseconds > 0
            ? gen2Delta * (_interval.TotalMilliseconds / elapsed.TotalMilliseconds)
            : gen2Delta;

        _lastGen2Count = currentGen2;
        _lastCheck = DateTime.UtcNow;

        var status = collectionsPerInterval < _maxGen2Collections * 0.5
            ? HealthStatus.Healthy
            : collectionsPerInterval < _maxGen2Collections
                ? HealthStatus.Degraded
                : HealthStatus.Unhealthy;

        return Task.FromResult(new HealthCheckResult
        {
            Status = status,
            Description = $"Gen2 collections in period: {gen2Delta}, rate: {collectionsPerInterval:F1}/interval",
            Data = new Dictionary<string, object>
            {
                ["gen0Collections"] = GC.CollectionCount(0),
                ["gen1Collections"] = GC.CollectionCount(1),
                ["gen2Collections"] = currentGen2,
                ["gen2Delta"] = gen2Delta,
                ["collectionsPerInterval"] = collectionsPerInterval
            }
        });
    }
}

/// <summary>
/// Thread pool health check.
/// </summary>
public sealed class ThreadPoolHealthCheck : IHealthCheck, ISelfHealingHealthCheck
{
    private readonly int _minWorkerThreads;
    private readonly int _minIOThreads;

    public ThreadPoolHealthCheck(int minWorkerThreads, int minIOThreads)
    {
        _minWorkerThreads = minWorkerThreads;
        _minIOThreads = minIOThreads;
    }

    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct = default)
    {
        ThreadPool.GetAvailableThreads(out var workerThreads, out var ioThreads);
        ThreadPool.GetMaxThreads(out var maxWorker, out var maxIO);

        var workerUtilization = 1.0 - ((double)workerThreads / maxWorker);
        var ioUtilization = 1.0 - ((double)ioThreads / maxIO);

        var status = workerThreads >= _minWorkerThreads && ioThreads >= _minIOThreads
            ? (workerUtilization < 0.8 && ioUtilization < 0.8 ? HealthStatus.Healthy : HealthStatus.Degraded)
            : HealthStatus.Unhealthy;

        return Task.FromResult(new HealthCheckResult
        {
            Status = status,
            Description = $"Available workers: {workerThreads}/{maxWorker}, IO: {ioThreads}/{maxIO}",
            Data = new Dictionary<string, object>
            {
                ["availableWorkers"] = workerThreads,
                ["availableIO"] = ioThreads,
                ["maxWorkers"] = maxWorker,
                ["maxIO"] = maxIO,
                ["workerUtilization"] = workerUtilization,
                ["ioUtilization"] = ioUtilization
            }
        });
    }

    public Task<bool> AttemptHealingAsync(CancellationToken ct = default)
    {
        // Increase thread pool minimum if needed
        ThreadPool.GetMinThreads(out var minWorker, out var minIO);
        ThreadPool.GetMaxThreads(out var maxWorker, out var maxIO);

        var newMinWorker = Math.Min(minWorker * 2, maxWorker);
        var newMinIO = Math.Min(minIO * 2, maxIO);

        ThreadPool.SetMinThreads(newMinWorker, newMinIO);

        return Task.FromResult(true);
    }
}

/// <summary>
/// Disk space health check.
/// </summary>
public sealed class DiskSpaceHealthCheck : IHealthCheck
{
    private readonly string _path;
    private readonly long _minFreeMB;

    public DiskSpaceHealthCheck(string path, long minFreeMB)
    {
        _path = path;
        _minFreeMB = minFreeMB;
    }

    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct = default)
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(_path) ?? _path);

            if (!drive.IsReady)
            {
                return Task.FromResult(new HealthCheckResult
                {
                    Status = HealthStatus.Unhealthy,
                    Description = $"Drive {drive.Name} is not ready"
                });
            }

            var freeSpaceMB = drive.AvailableFreeSpace / (1024 * 1024);
            var totalSpaceMB = drive.TotalSize / (1024 * 1024);
            var usedPercent = 100.0 * (1 - ((double)freeSpaceMB / totalSpaceMB));

            var status = freeSpaceMB > _minFreeMB * 2
                ? HealthStatus.Healthy
                : freeSpaceMB > _minFreeMB
                    ? HealthStatus.Degraded
                    : HealthStatus.Unhealthy;

            return Task.FromResult(new HealthCheckResult
            {
                Status = status,
                Description = $"Free space: {freeSpaceMB} MB ({100 - usedPercent:F1}% free)",
                Data = new Dictionary<string, object>
                {
                    ["freeSpaceMB"] = freeSpaceMB,
                    ["totalSpaceMB"] = totalSpaceMB,
                    ["usedPercent"] = usedPercent,
                    ["driveName"] = drive.Name
                }
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new HealthCheckResult
            {
                Status = HealthStatus.Unhealthy,
                Description = $"Failed to check disk space: {ex.Message}",
                Exception = ex
            });
        }
    }
}

/// <summary>
/// HTTP endpoint health check.
/// </summary>
public sealed class HttpEndpointHealthCheck : IHealthCheck, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly TimeSpan _timeout;

    public HttpEndpointHealthCheck(string endpoint, TimeSpan? timeout = null)
    {
        _endpoint = endpoint;
        _timeout = timeout ?? TimeSpan.FromSeconds(10);
        _httpClient = new HttpClient { Timeout = _timeout };
    }

    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var response = await _httpClient.GetAsync(_endpoint, ct);
            sw.Stop();

            var halfTimeout = TimeSpan.FromTicks(_timeout.Ticks / 2);
            var status = response.IsSuccessStatusCode
                ? (sw.Elapsed < halfTimeout ? HealthStatus.Healthy : HealthStatus.Degraded)
                : HealthStatus.Unhealthy;

            return new HealthCheckResult
            {
                Status = status,
                Description = $"HTTP {(int)response.StatusCode} in {sw.ElapsedMilliseconds}ms",
                Duration = sw.Elapsed,
                Data = new Dictionary<string, object>
                {
                    ["statusCode"] = (int)response.StatusCode,
                    ["endpoint"] = _endpoint,
                    ["responseTimeMs"] = sw.ElapsedMilliseconds
                }
            };
        }
        catch (Exception ex)
        {
            sw.Stop();

            return new HealthCheckResult
            {
                Status = HealthStatus.Unhealthy,
                Description = $"Failed to reach {_endpoint}: {ex.Message}",
                Duration = sw.Elapsed,
                Exception = ex
            };
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

/// <summary>
/// Database connectivity health check.
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly Func<CancellationToken, Task<bool>> _connectionTest;
    private readonly string _connectionName;

    public DatabaseHealthCheck(string connectionName, Func<CancellationToken, Task<bool>> connectionTest)
    {
        _connectionName = connectionName;
        _connectionTest = connectionTest;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var connected = await _connectionTest(ct);
            sw.Stop();

            return new HealthCheckResult
            {
                Status = connected ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                Description = connected
                    ? $"Database '{_connectionName}' connected in {sw.ElapsedMilliseconds}ms"
                    : $"Database '{_connectionName}' connection failed",
                Duration = sw.Elapsed,
                Data = new Dictionary<string, object>
                {
                    ["connectionName"] = _connectionName,
                    ["connected"] = connected,
                    ["responseTimeMs"] = sw.ElapsedMilliseconds
                }
            };
        }
        catch (Exception ex)
        {
            sw.Stop();

            return new HealthCheckResult
            {
                Status = HealthStatus.Unhealthy,
                Description = $"Database '{_connectionName}' error: {ex.Message}",
                Duration = sw.Elapsed,
                Exception = ex
            };
        }
    }
}

/// <summary>
/// Network connectivity health check.
/// </summary>
public sealed class NetworkHealthCheck : IHealthCheck
{
    private readonly string[] _hosts;
    private readonly int _timeoutMs;

    public NetworkHealthCheck(string[] hosts, int timeoutMs = 5000)
    {
        _hosts = hosts;
        _timeoutMs = timeoutMs;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct = default)
    {
        var results = new List<(string Host, bool Reachable, long RoundtripMs)>();

        using var ping = new Ping();

        foreach (var host in _hosts)
        {
            try
            {
                var reply = await ping.SendPingAsync(host, _timeoutMs);
                results.Add((host, reply.Status == IPStatus.Success, reply.RoundtripTime));
            }
            catch
            {
                results.Add((host, false, -1));
            }
        }

        var reachableCount = results.Count(r => r.Reachable);
        var avgLatency = results.Where(r => r.Reachable).Select(r => r.RoundtripMs).DefaultIfEmpty(0).Average();

        var status = reachableCount == _hosts.Length
            ? HealthStatus.Healthy
            : reachableCount > 0
                ? HealthStatus.Degraded
                : HealthStatus.Unhealthy;

        return new HealthCheckResult
        {
            Status = status,
            Description = $"Network: {reachableCount}/{_hosts.Length} hosts reachable, avg latency {avgLatency:F1}ms",
            Data = new Dictionary<string, object>
            {
                ["reachableCount"] = reachableCount,
                ["totalHosts"] = _hosts.Length,
                ["avgLatencyMs"] = avgLatency,
                ["results"] = results.Select(r => new { r.Host, r.Reachable, r.RoundtripMs }).ToList()
            }
        };
    }
}
