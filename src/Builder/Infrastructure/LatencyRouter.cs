using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http;

namespace OoplesFinance.StockIndicators.Builder.Infrastructure;

/// <summary>
/// Routes requests to endpoints with lowest latency.
/// Provides intelligent request routing based on measured performance.
/// </summary>
public sealed class LatencyRouter : IDisposable
{
    private readonly LatencyRouterOptions _options;
    private readonly ConcurrentDictionary<string, EndpointMetrics> _endpointMetrics = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _endpointLocks = new();
    private readonly Timer _probeTimer;
    private readonly Timer _decayTimer;
    private readonly HttpClient _probeClient;
    private bool _disposed;
#if NET461
    private static readonly ThreadLocal<Random> ThreadLocalRandom = new(() => new Random());
#endif

    /// <summary>
    /// Initializes a new instance of the LatencyRouter.
    /// </summary>
    public LatencyRouter(LatencyRouterOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        // Initialize metrics for all endpoints
        foreach (var endpoint in _options.Endpoints)
        {
            _endpointMetrics[endpoint.Id] = new EndpointMetrics
            {
                EndpointId = endpoint.Id,
                Endpoint = endpoint,
                Status = EndpointStatus.Unknown,
                AverageLatency = TimeSpan.MaxValue
            };
            _endpointLocks[endpoint.Id] = new SemaphoreSlim(endpoint.MaxConcurrentRequests);
        }

        _probeClient = new HttpClient { Timeout = _options.ProbeTimeout };

        // Start probe timer
        _probeTimer = new Timer(
            ProbeEndpoints,
            null,
            TimeSpan.Zero,
            _options.ProbeInterval);

        // Start decay timer for EWMA
        _decayTimer = new Timer(
            ApplyDecay,
            null,
            _options.DecayInterval,
            _options.DecayInterval);
    }

    /// <summary>
    /// Selects the best endpoint based on latency and availability.
    /// </summary>
    public async Task<RoutingDecision> SelectEndpointAsync(
        RoutingContext context,
        CancellationToken ct = default)
    {
        var availableEndpoints = GetAvailableEndpoints(context);

        if (availableEndpoints.Count == 0)
        {
            return new RoutingDecision
            {
                Success = false,
                Error = "No available endpoints"
            };
        }

        EndpointMetrics selectedMetrics;

        switch (_options.RoutingStrategy)
        {
            case RoutingStrategy.LowestLatency:
                selectedMetrics = SelectByLowestLatency(availableEndpoints);
                break;

            case RoutingStrategy.WeightedLatency:
                selectedMetrics = SelectByWeightedLatency(availableEndpoints);
                break;

            case RoutingStrategy.RoundRobin:
                selectedMetrics = SelectByRoundRobin(availableEndpoints);
                break;

            case RoutingStrategy.LeastConnections:
                selectedMetrics = SelectByLeastConnections(availableEndpoints);
                break;

            case RoutingStrategy.Geographic:
                selectedMetrics = await SelectByGeographicProximityAsync(availableEndpoints, context, ct);
                break;

            default:
                selectedMetrics = SelectByLowestLatency(availableEndpoints);
                break;
        }

        // Try to acquire connection slot
        if (_endpointLocks.TryGetValue(selectedMetrics.EndpointId, out var semaphore))
        {
            if (!await semaphore.WaitAsync(0, ct))
            {
                // Endpoint at capacity, try next best
                var alternatives = availableEndpoints
                    .Where(e => e.EndpointId != selectedMetrics.EndpointId)
                    .OrderBy(e => e.AverageLatency)
                    .ToList();

                foreach (var alt in alternatives)
                {
                    if (_endpointLocks.TryGetValue(alt.EndpointId, out var altSem))
                    {
                        if (await altSem.WaitAsync(0, ct))
                        {
                            selectedMetrics = alt;
                            break;
                        }
                    }
                }
            }
        }

        selectedMetrics.ActiveConnections++;
        selectedMetrics.TotalRequests++;

        var decision = new RoutingDecision
        {
            Success = true,
            Endpoint = selectedMetrics.Endpoint,
            Metrics = selectedMetrics,
            DecisionReason = $"Selected by {_options.RoutingStrategy} strategy"
        };

        OnRoutingDecision?.Invoke(this, new RoutingDecisionEventArgs { Decision = decision });

        return decision;
    }

    /// <summary>
    /// Releases an endpoint after request completion.
    /// </summary>
    public void ReleaseEndpoint(string endpointId)
    {
        if (_endpointMetrics.TryGetValue(endpointId, out var metrics))
        {
            Interlocked.Decrement(ref metrics._activeConnections);
        }

        if (_endpointLocks.TryGetValue(endpointId, out var semaphore))
        {
            try
            {
                semaphore.Release();
            }
            catch (SemaphoreFullException)
            {
                // Already released
            }
        }
    }

    /// <summary>
    /// Records the latency of a completed request.
    /// </summary>
    public void RecordLatency(string endpointId, TimeSpan latency, bool success)
    {
        if (!_endpointMetrics.TryGetValue(endpointId, out var metrics)) return;

        // Update EWMA latency
        if (metrics.AverageLatency == TimeSpan.MaxValue)
        {
            metrics.AverageLatency = latency;
        }
        else
        {
            var alpha = _options.EwmaAlpha;
            var newAvg = TimeSpan.FromTicks(
                (long)(alpha * latency.Ticks + (1 - alpha) * metrics.AverageLatency.Ticks));
            metrics.AverageLatency = newAvg;
        }

        // Update min/max
        if (latency < metrics.MinLatency)
        {
            metrics.MinLatency = latency;
        }
        if (latency > metrics.MaxLatency)
        {
            metrics.MaxLatency = latency;
        }

        // Update P50/P99 using streaming approximation
        UpdatePercentiles(metrics, latency);

        // Update success rate
        if (success)
        {
            metrics.SuccessfulRequests++;
        }
        else
        {
            metrics.FailedRequests++;
            metrics.ConsecutiveFailures++;

            // Check for circuit breaker trigger
            if (metrics.ConsecutiveFailures >= _options.CircuitBreakerThreshold)
            {
                metrics.Status = EndpointStatus.CircuitOpen;
                metrics.CircuitOpenedAt = DateTime.UtcNow;

                OnEndpointStatusChanged?.Invoke(this, new EndpointStatusChangedEventArgs
                {
                    EndpointId = endpointId,
                    NewStatus = EndpointStatus.CircuitOpen,
                    Reason = $"Circuit breaker triggered after {metrics.ConsecutiveFailures} consecutive failures"
                });
            }
        }

        if (success)
        {
            metrics.ConsecutiveFailures = 0;
        }

        metrics.LastResponseTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets metrics for all endpoints.
    /// </summary>
    public IReadOnlyList<EndpointMetrics> GetAllMetrics()
    {
        return _endpointMetrics.Values.ToList();
    }

    /// <summary>
    /// Gets metrics for a specific endpoint.
    /// </summary>
    public EndpointMetrics? GetMetrics(string endpointId)
    {
        return _endpointMetrics.TryGetValue(endpointId, out var metrics) ? metrics : null;
    }

    /// <summary>
    /// Manually marks an endpoint as healthy or unhealthy.
    /// </summary>
    public void SetEndpointStatus(string endpointId, EndpointStatus status, string? reason = null)
    {
        if (_endpointMetrics.TryGetValue(endpointId, out var metrics))
        {
            var oldStatus = metrics.Status;
            metrics.Status = status;

            if (status == EndpointStatus.Healthy)
            {
                metrics.ConsecutiveFailures = 0;
                metrics.CircuitOpenedAt = null;
            }

            if (oldStatus != status)
            {
                OnEndpointStatusChanged?.Invoke(this, new EndpointStatusChangedEventArgs
                {
                    EndpointId = endpointId,
                    NewStatus = status,
                    PreviousStatus = oldStatus,
                    Reason = reason
                });
            }
        }
    }

    /// <summary>
    /// Event raised when an endpoint's status changes.
    /// </summary>
    public event EventHandler<EndpointStatusChangedEventArgs>? OnEndpointStatusChanged;

    /// <summary>
    /// Event raised when a routing decision is made.
    /// </summary>
    public event EventHandler<RoutingDecisionEventArgs>? OnRoutingDecision;

    private List<EndpointMetrics> GetAvailableEndpoints(RoutingContext context)
    {
        var now = DateTime.UtcNow;

        return _endpointMetrics.Values
            .Where(m =>
            {
                // Skip unhealthy endpoints
                if (m.Status == EndpointStatus.Unhealthy) return false;

                // Check circuit breaker
                if (m.Status == EndpointStatus.CircuitOpen)
                {
                    // Allow half-open after timeout
                    if (m.CircuitOpenedAt.HasValue &&
                        now - m.CircuitOpenedAt.Value > _options.CircuitBreakerTimeout)
                    {
                        m.Status = EndpointStatus.HalfOpen;
                    }
                    else
                    {
                        return false;
                    }
                }

                // Check region filter
                if (context.PreferredRegions != null && context.PreferredRegions.Count > 0)
                {
                    if (!context.PreferredRegions.Contains(m.Endpoint.Region))
                    {
                        return false;
                    }
                }

                // Check capability filter
                if (context.RequiredCapabilities != null)
                {
                    foreach (var cap in context.RequiredCapabilities)
                    {
                        if (!m.Endpoint.Capabilities.Contains(cap))
                        {
                            return false;
                        }
                    }
                }

                return true;
            })
            .ToList();
    }

    private EndpointMetrics SelectByLowestLatency(List<EndpointMetrics> endpoints)
    {
        return endpoints.OrderBy(e => e.AverageLatency).First();
    }

    private EndpointMetrics SelectByWeightedLatency(List<EndpointMetrics> endpoints)
    {
        // Weight by inverse of latency
        var totalInverseLatency = endpoints.Sum(e =>
            e.AverageLatency == TimeSpan.MaxValue ? 0 : 1.0 / e.AverageLatency.TotalMilliseconds);

        if (totalInverseLatency == 0)
        {
#if NET461
            return endpoints[ThreadLocalRandom.Value.Next(endpoints.Count)];
#else
            return endpoints[Random.Shared.Next(endpoints.Count)];
#endif
        }

#if NET461
        var randomValue = ThreadLocalRandom.Value.NextDouble() * totalInverseLatency;
#else
        var randomValue = Random.Shared.NextDouble() * totalInverseLatency;
#endif
        var cumulative = 0.0;

        foreach (var endpoint in endpoints)
        {
            if (endpoint.AverageLatency == TimeSpan.MaxValue) continue;

            cumulative += 1.0 / endpoint.AverageLatency.TotalMilliseconds;
            if (randomValue <= cumulative)
            {
                return endpoint;
            }
        }

        return endpoints[0];
    }

    private EndpointMetrics SelectByRoundRobin(List<EndpointMetrics> endpoints)
    {
        // Simple round-robin using request count
        var minRequests = endpoints.Min(e => e.TotalRequests);
        return endpoints.First(e => e.TotalRequests == minRequests);
    }

    private EndpointMetrics SelectByLeastConnections(List<EndpointMetrics> endpoints)
    {
        return endpoints.OrderBy(e => e.ActiveConnections).First();
    }

    private async Task<EndpointMetrics> SelectByGeographicProximityAsync(
        List<EndpointMetrics> endpoints,
        RoutingContext context,
        CancellationToken ct)
    {
        if (context.ClientIp is not { Length: > 0 } clientIp)
        {
            return SelectByLowestLatency(endpoints);
        }

        // Simple IP-based region determination
        var clientRegion = DetermineClientRegion(clientIp);
        if (string.IsNullOrEmpty(clientRegion))
        {
            return SelectByLowestLatency(endpoints);
        }

        // Prefer endpoints in client's region
        var regionalEndpoints = endpoints
            .Where(e => e.Endpoint.Region == clientRegion)
            .ToList();

        if (regionalEndpoints.Count > 0)
        {
            return SelectByLowestLatency(regionalEndpoints);
        }

        // Fall back to lowest latency
        return SelectByLowestLatency(endpoints);
    }

    private string? DetermineClientRegion(string clientIp)
    {
        if (!IPAddress.TryParse(clientIp, out var ip)) return null;

        // Simplified IP-to-region mapping (production would use GeoIP database)
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

    private void UpdatePercentiles(EndpointMetrics metrics, TimeSpan latency)
    {
        // Use P2 algorithm approximation for streaming percentiles
        metrics.LatencySamples.Enqueue(latency);

        // Keep only recent samples
        while (metrics.LatencySamples.Count > _options.PercentileSampleSize)
        {
            metrics.LatencySamples.TryDequeue(out _);
        }

        if (metrics.LatencySamples.Count >= 10)
        {
            var sorted = metrics.LatencySamples.OrderBy(t => t).ToList();
            var p50Index = (int)(sorted.Count * 0.5);
            var p99Index = (int)(sorted.Count * 0.99);

            metrics.P50Latency = sorted[p50Index];
            metrics.P99Latency = sorted[Math.Min(p99Index, sorted.Count - 1)];
        }
    }

    private async void ProbeEndpoints(object? state)
    {
        if (_disposed) return;

        foreach (var kvp in _endpointMetrics)
        {
            var endpointId = kvp.Key;
            var metrics = kvp.Value;
            if (string.IsNullOrEmpty(metrics.Endpoint.HealthCheckUrl)) continue;

            try
            {
                var sw = Stopwatch.StartNew();
                var response = await _probeClient.GetAsync(metrics.Endpoint.HealthCheckUrl);
                sw.Stop();

                var wasUnhealthy = metrics.Status == EndpointStatus.Unhealthy ||
                                   metrics.Status == EndpointStatus.CircuitOpen;

                if (response.IsSuccessStatusCode)
                {
                    RecordLatency(endpointId, sw.Elapsed, true);

                    if (metrics.Status == EndpointStatus.Unknown ||
                        metrics.Status == EndpointStatus.HalfOpen)
                    {
                        SetEndpointStatus(endpointId, EndpointStatus.Healthy, "Health check passed");
                    }
                }
                else
                {
                    RecordLatency(endpointId, sw.Elapsed, false);

                    if (metrics.Status == EndpointStatus.HalfOpen)
                    {
                        SetEndpointStatus(endpointId, EndpointStatus.CircuitOpen, "Health check failed in half-open state");
                        metrics.CircuitOpenedAt = DateTime.UtcNow;
                    }
                }
            }
            catch (Exception ex)
            {
                RecordLatency(endpointId, _options.ProbeTimeout, false);

                if (metrics.Status == EndpointStatus.Unknown)
                {
                    SetEndpointStatus(endpointId, EndpointStatus.Unhealthy, ex.Message);
                }
            }
        }
    }

    private void ApplyDecay(object? state)
    {
        if (_disposed) return;

        // Apply EWMA decay to move latencies toward baseline
        foreach (var metrics in _endpointMetrics.Values)
        {
            if (metrics.AverageLatency != TimeSpan.MaxValue)
            {
                var decayFactor = _options.DecayFactor;
                var baseline = _options.BaselineLatency;

                var newLatency = TimeSpan.FromTicks(
                    (long)(metrics.AverageLatency.Ticks * decayFactor +
                           baseline.Ticks * (1 - decayFactor)));

                metrics.AverageLatency = newLatency;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _probeTimer.Dispose();
        _decayTimer.Dispose();
        _probeClient.Dispose();

        foreach (var semaphore in _endpointLocks.Values)
        {
            semaphore.Dispose();
        }
    }
}

/// <summary>
/// Latency router options.
/// </summary>
public sealed class LatencyRouterOptions
{
    /// <summary>Available endpoints.</summary>
    public List<EndpointConfig> Endpoints { get; set; } = [];

    /// <summary>Routing strategy.</summary>
    public RoutingStrategy RoutingStrategy { get; set; } = RoutingStrategy.LowestLatency;

    /// <summary>Interval between health probes.</summary>
    public TimeSpan ProbeInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Timeout for health probes.</summary>
    public TimeSpan ProbeTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>EWMA alpha for latency calculation.</summary>
    public double EwmaAlpha { get; set; } = 0.3;

    /// <summary>Interval between decay applications.</summary>
    public TimeSpan DecayInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Decay factor toward baseline.</summary>
    public double DecayFactor { get; set; } = 0.9;

    /// <summary>Baseline latency for decay.</summary>
    public TimeSpan BaselineLatency { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>Number of failures before circuit breaker opens.</summary>
    public int CircuitBreakerThreshold { get; set; } = 5;

    /// <summary>Time before circuit breaker allows half-open.</summary>
    public TimeSpan CircuitBreakerTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Number of samples for percentile calculation.</summary>
    public int PercentileSampleSize { get; set; } = 1000;
}

/// <summary>
/// Endpoint configuration.
/// </summary>
public sealed class EndpointConfig
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string HealthCheckUrl { get; set; } = string.Empty;
    public int MaxConcurrentRequests { get; set; } = 100;
    public int Weight { get; set; } = 100;
    public HashSet<string> Capabilities { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = [];
}

/// <summary>
/// Endpoint metrics.
/// </summary>
public sealed class EndpointMetrics
{
    public string EndpointId { get; set; } = string.Empty;
    public EndpointConfig Endpoint { get; set; } = new();
    public EndpointStatus Status { get; set; }
    public TimeSpan AverageLatency { get; set; } = TimeSpan.MaxValue;
    public TimeSpan MinLatency { get; set; } = TimeSpan.MaxValue;
    public TimeSpan MaxLatency { get; set; } = TimeSpan.Zero;
    public TimeSpan P50Latency { get; set; }
    public TimeSpan P99Latency { get; set; }
    public long TotalRequests { get; set; }
    public long SuccessfulRequests { get; set; }
    public long FailedRequests { get; set; }
    public int ConsecutiveFailures { get; set; }
    public DateTime? LastResponseTime { get; set; }
    public DateTime? CircuitOpenedAt { get; set; }

    internal int _activeConnections;
    public int ActiveConnections
    {
        get => _activeConnections;
        set => _activeConnections = value;
    }

    public ConcurrentQueue<TimeSpan> LatencySamples { get; } = new();

    public double SuccessRate => TotalRequests > 0
        ? (double)SuccessfulRequests / TotalRequests
        : 0;
}

/// <summary>
/// Endpoint status.
/// </summary>
public enum EndpointStatus
{
    Unknown,
    Healthy,
    Unhealthy,
    CircuitOpen,
    HalfOpen
}

/// <summary>
/// Routing strategy.
/// </summary>
public enum RoutingStrategy
{
    LowestLatency,
    WeightedLatency,
    RoundRobin,
    LeastConnections,
    Geographic
}

/// <summary>
/// Routing context.
/// </summary>
public sealed class RoutingContext
{
    public string? ClientIp { get; set; }
    public string? RequestId { get; set; }
    public List<string>? PreferredRegions { get; set; }
    public HashSet<string>? RequiredCapabilities { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// Routing decision.
/// </summary>
public sealed class RoutingDecision
{
    public bool Success { get; set; }
    public EndpointConfig? Endpoint { get; set; }
    public EndpointMetrics? Metrics { get; set; }
    public string? DecisionReason { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Endpoint status changed event args.
/// </summary>
public sealed class EndpointStatusChangedEventArgs : EventArgs
{
    public string EndpointId { get; set; } = string.Empty;
    public EndpointStatus NewStatus { get; set; }
    public EndpointStatus? PreviousStatus { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Routing decision event args.
/// </summary>
public sealed class RoutingDecisionEventArgs : EventArgs
{
    public RoutingDecision Decision { get; set; } = new();
    public RoutingContext Context { get; set; } = new();
}
