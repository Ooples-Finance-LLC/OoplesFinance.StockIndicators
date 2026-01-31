using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;

namespace OoplesFinance.StockIndicators.Builder.Infrastructure;

/// <summary>
/// Manages multi-region deployment configuration and routing.
/// Provides geographic load balancing and failover capabilities.
/// </summary>
public sealed class RegionManager : IDisposable
{
    private readonly RegionManagerOptions _options;
    private readonly IRegionHealthChecker _healthChecker;
    private readonly ConcurrentDictionary<string, RegionStatus> _regionStatuses = new();
    private readonly SemaphoreSlim _statusLock = new(1, 1);
    private readonly Timer _healthCheckTimer;
    private bool _disposed;
#if NET461
    private static readonly ThreadLocal<Random> ThreadLocalRandom = new(() => new Random());
#endif

    /// <summary>
    /// Initializes a new instance of the RegionManager.
    /// </summary>
    public RegionManager(
        RegionManagerOptions options,
        IRegionHealthChecker healthChecker)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _healthChecker = healthChecker ?? throw new ArgumentNullException(nameof(healthChecker));

        // Initialize region statuses
        foreach (var region in _options.Regions)
        {
            _regionStatuses[region.RegionId] = new RegionStatus
            {
                Region = region,
                IsHealthy = true,
                LastChecked = DateTime.UtcNow
            };
        }

        // Start health check timer
        _healthCheckTimer = new Timer(
            CheckRegionHealth,
            null,
            TimeSpan.Zero,
            _options.HealthCheckInterval);
    }

    /// <summary>
    /// Gets the optimal region for a request based on latency and availability.
    /// </summary>
    public async Task<RegionInfo?> GetOptimalRegionAsync(
        string? clientIp = null,
        string? preferredRegion = null,
        CancellationToken ct = default)
    {
        var healthyRegions = GetHealthyRegions();

        if (healthyRegions.Count == 0)
        {
            // All regions down - return primary even if unhealthy
            return _options.Regions.FirstOrDefault(r => r.IsPrimary);
        }

        // If preferred region is specified and healthy, use it
        if (!string.IsNullOrEmpty(preferredRegion))
        {
            var preferred = healthyRegions.FirstOrDefault(r =>
                r.RegionId.Equals(preferredRegion, StringComparison.OrdinalIgnoreCase));

            if (preferred != null)
            {
                return preferred;
            }
        }

        // If client IP is provided, select region by geographic proximity
        if (clientIp is { Length: > 0 } ip)
        {
            var clientRegion = await DetermineClientRegionAsync(ip, ct);
            if (clientRegion != null)
            {
                var nearestRegion = FindNearestRegion(clientRegion, healthyRegions);
                if (nearestRegion != null)
                {
                    return nearestRegion;
                }
            }
        }

        // Fall back to weighted round-robin
        return SelectByWeight(healthyRegions);
    }

    /// <summary>
    /// Gets all healthy regions.
    /// </summary>
    public IReadOnlyList<RegionInfo> GetHealthyRegions()
    {
        return _regionStatuses.Values
            .Where(s => s.IsHealthy)
            .Select(s => s.Region)
            .OrderByDescending(r => r.IsPrimary)
            .ThenByDescending(r => r.Weight)
            .ToList();
    }

    /// <summary>
    /// Gets the status of all regions.
    /// </summary>
    public IReadOnlyList<RegionStatus> GetRegionStatuses()
    {
        return _regionStatuses.Values.ToList();
    }

    /// <summary>
    /// Gets the primary region.
    /// </summary>
    public RegionInfo? GetPrimaryRegion()
    {
        return _options.Regions.FirstOrDefault(r => r.IsPrimary);
    }

    /// <summary>
    /// Marks a region as healthy or unhealthy.
    /// </summary>
    public async Task SetRegionHealthAsync(
        string regionId,
        bool isHealthy,
        string? reason = null,
        CancellationToken ct = default)
    {
        await _statusLock.WaitAsync(ct);
        try
        {
            if (_regionStatuses.TryGetValue(regionId, out var status))
            {
                var wasHealthy = status.IsHealthy;
                status.IsHealthy = isHealthy;
                status.LastChecked = DateTime.UtcNow;
                status.LastError = isHealthy ? null : reason;

                if (wasHealthy != isHealthy)
                {
                    status.StateChangedAt = DateTime.UtcNow;

                    // Raise event for monitoring
                    OnRegionStatusChanged?.Invoke(this, new RegionStatusChangedEventArgs
                    {
                        RegionId = regionId,
                        IsHealthy = isHealthy,
                        Reason = reason
                    });
                }
            }
        }
        finally
        {
            _statusLock.Release();
        }
    }

    /// <summary>
    /// Initiates a failover from one region to another.
    /// </summary>
    public async Task<FailoverResult> InitiateFailoverAsync(
        string fromRegionId,
        string toRegionId,
        FailoverReason reason,
        CancellationToken ct = default)
    {
        var fromRegion = _options.Regions.FirstOrDefault(r => r.RegionId == fromRegionId);
        var toRegion = _options.Regions.FirstOrDefault(r => r.RegionId == toRegionId);

        if (fromRegion == null || toRegion == null)
        {
            return new FailoverResult
            {
                Success = false,
                Error = "Invalid region specified"
            };
        }

        // Mark source region as unhealthy
        await SetRegionHealthAsync(fromRegionId, false, $"Failover initiated: {reason}", ct);

        // Verify target region is healthy
        var targetHealthy = await _healthChecker.CheckHealthAsync(toRegion, ct);
        if (!targetHealthy.IsHealthy)
        {
            return new FailoverResult
            {
                Success = false,
                FromRegion = fromRegionId,
                ToRegion = toRegionId,
                Error = "Target region is not healthy"
            };
        }

        // If failover is to make a new primary, update configuration
        if (fromRegion.IsPrimary)
        {
            fromRegion.IsPrimary = false;
            toRegion.IsPrimary = true;
        }

        OnFailoverInitiated?.Invoke(this, new FailoverEventArgs
        {
            FromRegion = fromRegionId,
            ToRegion = toRegionId,
            Reason = reason,
            Timestamp = DateTime.UtcNow
        });

        return new FailoverResult
        {
            Success = true,
            FromRegion = fromRegionId,
            ToRegion = toRegionId,
            InitiatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Gets region endpoints for a specific service.
    /// </summary>
    public IReadOnlyList<ServiceEndpoint> GetServiceEndpoints(
        string serviceName,
        bool healthyOnly = true)
    {
        var regions = healthyOnly ? GetHealthyRegions() : _options.Regions;

        return regions
            .Where(r => r.ServiceEndpoints.ContainsKey(serviceName))
            .Select(r => new ServiceEndpoint
            {
                RegionId = r.RegionId,
                ServiceName = serviceName,
                Endpoint = r.ServiceEndpoints[serviceName],
                Weight = r.Weight,
                IsPrimary = r.IsPrimary
            })
            .OrderByDescending(e => e.IsPrimary)
            .ThenByDescending(e => e.Weight)
            .ToList();
    }

    /// <summary>
    /// Event raised when a region's status changes.
    /// </summary>
    public event EventHandler<RegionStatusChangedEventArgs>? OnRegionStatusChanged;

    /// <summary>
    /// Event raised when a failover is initiated.
    /// </summary>
    public event EventHandler<FailoverEventArgs>? OnFailoverInitiated;

    private async void CheckRegionHealth(object? state)
    {
        if (_disposed) return;

        foreach (var region in _options.Regions)
        {
            try
            {
                var healthResult = await _healthChecker.CheckHealthAsync(region, CancellationToken.None);
                await SetRegionHealthAsync(region.RegionId, healthResult.IsHealthy, healthResult.Error);

                if (_regionStatuses.TryGetValue(region.RegionId, out var status))
                {
                    status.Latency = healthResult.Latency;
                    status.LastResponseTime = healthResult.ResponseTime;
                }
            }
            catch (Exception ex)
            {
                await SetRegionHealthAsync(region.RegionId, false, ex.Message);
            }
        }

        // Check for automatic failover conditions
        await CheckForAutomaticFailoverAsync();
    }

    private async Task CheckForAutomaticFailoverAsync()
    {
        if (!_options.EnableAutomaticFailover) return;

        var primaryRegion = GetPrimaryRegion();
        if (primaryRegion == null) return;

        if (!_regionStatuses.TryGetValue(primaryRegion.RegionId, out var primaryStatus)) return;

        // Check if primary has been unhealthy for too long
        if (!primaryStatus.IsHealthy &&
            primaryStatus.StateChangedAt.HasValue &&
            DateTime.UtcNow - primaryStatus.StateChangedAt.Value > _options.FailoverThreshold)
        {
            // Find best healthy secondary
            var healthySecondary = GetHealthyRegions()
                .FirstOrDefault(r => !r.IsPrimary);

            if (healthySecondary != null)
            {
                await InitiateFailoverAsync(
                    primaryRegion.RegionId,
                    healthySecondary.RegionId,
                    FailoverReason.PrimaryUnhealthy,
                    CancellationToken.None);
            }
        }
    }

    private async Task<string?> DetermineClientRegionAsync(string clientIp, CancellationToken ct)
    {
        // Simple IP-based region detection
        // In production, use a GeoIP service
        if (IPAddress.TryParse(clientIp, out var ip))
        {
            // Map IP ranges to regions (simplified)
            // This should use a proper GeoIP database
            var firstOctet = ip.GetAddressBytes()[0];

            return firstOctet switch
            {
                >= 1 and < 50 => "us-east",
                >= 50 and < 100 => "us-west",
                >= 100 and < 150 => "eu-west",
                >= 150 and < 200 => "ap-southeast",
                _ => null
            };
        }

        return null;
    }

    private RegionInfo? FindNearestRegion(string clientRegion, IReadOnlyList<RegionInfo> healthyRegions)
    {
        // Define region distances (simplified)
        var regionDistances = new Dictionary<string, Dictionary<string, int>>
        {
            { "us-east", new Dictionary<string, int> { { "us-east", 0 }, { "us-west", 50 }, { "eu-west", 100 }, { "ap-southeast", 200 } } },
            { "us-west", new Dictionary<string, int> { { "us-west", 0 }, { "us-east", 50 }, { "ap-southeast", 100 }, { "eu-west", 150 } } },
            { "eu-west", new Dictionary<string, int> { { "eu-west", 0 }, { "us-east", 100 }, { "ap-southeast", 150 }, { "us-west", 150 } } },
            { "ap-southeast", new Dictionary<string, int> { { "ap-southeast", 0 }, { "us-west", 100 }, { "eu-west", 150 }, { "us-east", 200 } } }
        };

        if (!regionDistances.TryGetValue(clientRegion, out var distances))
        {
            return healthyRegions.FirstOrDefault();
        }

        return healthyRegions
            .OrderBy(r => {
                if (distances.TryGetValue(r.RegionId, out var dist))
                    return dist;
                return int.MaxValue;
            })
            .ThenByDescending(r => r.Weight)
            .FirstOrDefault();
    }

    private RegionInfo? SelectByWeight(IReadOnlyList<RegionInfo> regions)
    {
        if (regions.Count == 0) return null;
        if (regions.Count == 1) return regions[0];

        var totalWeight = regions.Sum(r => r.Weight);
#if NET461
        var randomValue = ThreadLocalRandom.Value.Next(totalWeight);
#else
        var randomValue = Random.Shared.Next(totalWeight);
#endif

        var cumulativeWeight = 0;
        foreach (var region in regions)
        {
            cumulativeWeight += region.Weight;
            if (randomValue < cumulativeWeight)
            {
                return region;
            }
        }

        return regions[0];
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _healthCheckTimer.Dispose();
        _statusLock.Dispose();
    }
}

/// <summary>
/// Region manager options.
/// </summary>
public sealed class RegionManagerOptions
{
    /// <summary>List of configured regions.</summary>
    public List<RegionInfo> Regions { get; set; } = [];

    /// <summary>Interval between health checks.</summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Whether to enable automatic failover.</summary>
    public bool EnableAutomaticFailover { get; set; } = true;

    /// <summary>Time before triggering automatic failover.</summary>
    public TimeSpan FailoverThreshold { get; set; } = TimeSpan.FromMinutes(2);
}

/// <summary>
/// Region information.
/// </summary>
public sealed class RegionInfo
{
    public string RegionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int Weight { get; set; } = 100;
    public string HealthCheckUrl { get; set; } = string.Empty;
    public Dictionary<string, string> ServiceEndpoints { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = [];
}

/// <summary>
/// Region status.
/// </summary>
public sealed class RegionStatus
{
    public RegionInfo Region { get; set; } = new();
    public bool IsHealthy { get; set; }
    public DateTime LastChecked { get; set; }
    public DateTime? StateChangedAt { get; set; }
    public string? LastError { get; set; }
    public TimeSpan? Latency { get; set; }
    public DateTime? LastResponseTime { get; set; }
}

/// <summary>
/// Service endpoint.
/// </summary>
public sealed class ServiceEndpoint
{
    public string RegionId { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public int Weight { get; set; }
    public bool IsPrimary { get; set; }
}

/// <summary>
/// Region status changed event args.
/// </summary>
public sealed class RegionStatusChangedEventArgs : EventArgs
{
    public string RegionId { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Failover event args.
/// </summary>
public sealed class FailoverEventArgs : EventArgs
{
    public string FromRegion { get; set; } = string.Empty;
    public string ToRegion { get; set; } = string.Empty;
    public FailoverReason Reason { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Failover reason.
/// </summary>
public enum FailoverReason
{
    Manual,
    PrimaryUnhealthy,
    HighLatency,
    Maintenance,
    DisasterRecovery
}

/// <summary>
/// Failover result.
/// </summary>
public sealed class FailoverResult
{
    public bool Success { get; set; }
    public string? FromRegion { get; set; }
    public string? ToRegion { get; set; }
    public DateTime? InitiatedAt { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Interface for region health checking.
/// </summary>
public interface IRegionHealthChecker
{
    Task<RegionHealthCheckResult> CheckHealthAsync(RegionInfo region, CancellationToken ct = default);
}

/// <summary>
/// Region health check result.
/// </summary>
public sealed class RegionHealthCheckResult
{
    public bool IsHealthy { get; set; }
    public TimeSpan? Latency { get; set; }
    public DateTime? ResponseTime { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// HTTP-based region health checker.
/// </summary>
public sealed class HttpRegionHealthChecker : IRegionHealthChecker, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _timeout;

    public HttpRegionHealthChecker(TimeSpan? timeout = null)
    {
        _timeout = timeout ?? TimeSpan.FromSeconds(10);
        _httpClient = new HttpClient { Timeout = _timeout };
    }

    public async Task<RegionHealthCheckResult> CheckHealthAsync(RegionInfo region, CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var response = await _httpClient.GetAsync(region.HealthCheckUrl, ct);
            var latency = DateTime.UtcNow - startTime;

            return new RegionHealthCheckResult
            {
                IsHealthy = response.IsSuccessStatusCode,
                Latency = latency,
                ResponseTime = DateTime.UtcNow,
                Error = response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            return new RegionHealthCheckResult
            {
                IsHealthy = false,
                Latency = DateTime.UtcNow - startTime,
                ResponseTime = DateTime.UtcNow,
                Error = ex.Message
            };
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
