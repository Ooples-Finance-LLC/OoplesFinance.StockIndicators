using System.Collections.Concurrent;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OoplesFinance.StockIndicators.Builder.Infrastructure;

/// <summary>
/// Manages data replication across multiple regions.
/// Provides eventual consistency with conflict resolution.
/// </summary>
public sealed class DataReplicator : IDisposable
{
    private readonly DataReplicatorOptions _options;
    private readonly IReplicationTransport _transport;
    private readonly ConcurrentDictionary<string, ReplicationState> _replicationStates = new();
    private readonly ConcurrentQueue<ReplicationEvent> _pendingEvents = new();
    private readonly SemaphoreSlim _processLock = new(1, 1);
    private readonly Timer _replicationTimer;
    private readonly Timer _healthCheckTimer;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the DataReplicator.
    /// </summary>
    public DataReplicator(
        DataReplicatorOptions options,
        IReplicationTransport transport)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));

        // Initialize replication states for each target region
        foreach (var region in _options.TargetRegions)
        {
            _replicationStates[region] = new ReplicationState
            {
                RegionId = region,
                LastSyncTime = DateTime.MinValue,
                Status = ReplicationStatus.Initializing
            };
        }

        // Start replication timer
        _replicationTimer = new Timer(
            ProcessPendingEvents,
            null,
            TimeSpan.FromSeconds(1),
            _options.ReplicationInterval);

        // Start health check timer
        _healthCheckTimer = new Timer(
            CheckReplicationHealth,
            null,
            TimeSpan.FromSeconds(5),
            _options.HealthCheckInterval);
    }

    /// <summary>
    /// Queues a data change for replication to all target regions.
    /// </summary>
    public async Task<ReplicationResult> ReplicateAsync<T>(
        string entityType,
        string entityId,
        T data,
        ReplicationPriority priority = ReplicationPriority.Normal,
        CancellationToken ct = default) where T : class
    {
        var replicationEvent = new ReplicationEvent
        {
            EventId = Guid.NewGuid().ToString("N"),
            EntityType = entityType,
            EntityId = entityId,
            Data = JsonSerializer.Serialize(data),
            DataHash = ComputeHash(data),
            Timestamp = DateTime.UtcNow,
            Priority = priority,
            SourceRegion = _options.SourceRegion,
            Version = GenerateVersion()
        };

        _pendingEvents.Enqueue(replicationEvent);

        OnEventQueued?.Invoke(this, new ReplicationEventArgs
        {
            Event = replicationEvent,
            TargetRegions = _options.TargetRegions.ToList()
        });

        // For high priority, process immediately
        if (priority == ReplicationPriority.High || priority == ReplicationPriority.Critical)
        {
            await ProcessEventImmediatelyAsync(replicationEvent, ct);
        }

        return new ReplicationResult
        {
            EventId = replicationEvent.EventId,
            Queued = true,
            QueuedAt = replicationEvent.Timestamp
        };
    }

    /// <summary>
    /// Performs a full synchronization with a target region.
    /// </summary>
    public async Task<SyncResult> FullSyncAsync(
        string targetRegion,
        Func<IAsyncEnumerable<ReplicationEvent>> dataProvider,
        CancellationToken ct = default)
    {
        if (!_replicationStates.TryGetValue(targetRegion, out var state))
        {
            return new SyncResult
            {
                Success = false,
                Error = $"Unknown target region: {targetRegion}"
            };
        }

        state.Status = ReplicationStatus.Syncing;
        var syncStartTime = DateTime.UtcNow;
        var syncedCount = 0;
        var failedCount = 0;
        var conflicts = new List<ConflictInfo>();

        try
        {
            await foreach (var evt in dataProvider().WithCancellation(ct))
            {
                try
                {
                    var result = await _transport.SendAsync(targetRegion, evt, ct);

                    if (result.Success)
                    {
                        syncedCount++;
                    }
                    else if (result.ConflictDetected)
                    {
                        var resolution = await ResolveConflictAsync(evt, result.ConflictingEvent!, ct);
                        conflicts.Add(new ConflictInfo
                        {
                            EventId = evt.EventId,
                            Resolution = resolution,
                            ResolvedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        failedCount++;
                    }
                }
                catch (Exception ex)
                {
                    failedCount++;
                    OnReplicationError?.Invoke(this, new ReplicationErrorEventArgs
                    {
                        Event = evt,
                        TargetRegion = targetRegion,
                        Error = ex
                    });
                }
            }

            state.LastSyncTime = DateTime.UtcNow;
            state.Status = ReplicationStatus.Active;

            return new SyncResult
            {
                Success = failedCount == 0,
                SyncedCount = syncedCount,
                FailedCount = failedCount,
                Conflicts = conflicts,
                Duration = DateTime.UtcNow - syncStartTime
            };
        }
        catch (Exception ex)
        {
            state.Status = ReplicationStatus.Error;
            state.LastError = ex.Message;

            return new SyncResult
            {
                Success = false,
                Error = ex.Message,
                SyncedCount = syncedCount,
                FailedCount = failedCount,
                Duration = DateTime.UtcNow - syncStartTime
            };
        }
    }

    /// <summary>
    /// Gets the replication lag for a specific region.
    /// </summary>
    public TimeSpan GetReplicationLag(string targetRegion)
    {
        if (_replicationStates.TryGetValue(targetRegion, out var state))
        {
            return DateTime.UtcNow - state.LastSyncTime;
        }

        return TimeSpan.MaxValue;
    }

    /// <summary>
    /// Gets the replication status for all regions.
    /// </summary>
    public IReadOnlyDictionary<string, ReplicationState> GetReplicationStatus()
    {
        return new Dictionary<string, ReplicationState>(_replicationStates);
    }

    /// <summary>
    /// Pauses replication to a specific region.
    /// </summary>
    public void PauseReplication(string targetRegion, string reason)
    {
        if (_replicationStates.TryGetValue(targetRegion, out var state))
        {
            state.Status = ReplicationStatus.Paused;
            state.PauseReason = reason;
            state.PausedAt = DateTime.UtcNow;

            OnReplicationPaused?.Invoke(this, new ReplicationPausedEventArgs
            {
                TargetRegion = targetRegion,
                Reason = reason
            });
        }
    }

    /// <summary>
    /// Resumes replication to a specific region.
    /// </summary>
    public void ResumeReplication(string targetRegion)
    {
        if (_replicationStates.TryGetValue(targetRegion, out var state))
        {
            state.Status = ReplicationStatus.Active;
            state.PauseReason = null;
            state.PausedAt = null;

            OnReplicationResumed?.Invoke(this, new ReplicationResumedEventArgs
            {
                TargetRegion = targetRegion
            });
        }
    }

    /// <summary>
    /// Event raised when an event is queued for replication.
    /// </summary>
    public event EventHandler<ReplicationEventArgs>? OnEventQueued;

    /// <summary>
    /// Event raised when an event is successfully replicated.
    /// </summary>
    public event EventHandler<ReplicationEventArgs>? OnEventReplicated;

    /// <summary>
    /// Event raised when a replication error occurs.
    /// </summary>
    public event EventHandler<ReplicationErrorEventArgs>? OnReplicationError;

    /// <summary>
    /// Event raised when a conflict is detected and resolved.
    /// </summary>
    public event EventHandler<ConflictResolvedEventArgs>? OnConflictResolved;

    /// <summary>
    /// Event raised when replication is paused.
    /// </summary>
    public event EventHandler<ReplicationPausedEventArgs>? OnReplicationPaused;

    /// <summary>
    /// Event raised when replication is resumed.
    /// </summary>
    public event EventHandler<ReplicationResumedEventArgs>? OnReplicationResumed;

    private async void ProcessPendingEvents(object? state)
    {
        if (_disposed) return;

        if (!await _processLock.WaitAsync(0)) return;

        try
        {
            var batch = new List<ReplicationEvent>();
            var batchSize = _options.BatchSize;

            while (batch.Count < batchSize && _pendingEvents.TryDequeue(out var evt))
            {
                batch.Add(evt);
            }

            if (batch.Count == 0) return;

            // Sort by priority
            batch = batch.OrderByDescending(e => e.Priority).ToList();

            foreach (var targetRegion in _options.TargetRegions)
            {
                if (!_replicationStates.TryGetValue(targetRegion, out var regionState)) continue;
                if (regionState.Status == ReplicationStatus.Paused) continue;

                await ReplicateBatchToRegionAsync(batch, targetRegion, CancellationToken.None);
            }
        }
        finally
        {
            _processLock.Release();
        }
    }

    private async Task ReplicateBatchToRegionAsync(
        List<ReplicationEvent> batch,
        string targetRegion,
        CancellationToken ct)
    {
        if (!_replicationStates.TryGetValue(targetRegion, out var state)) return;

        var successCount = 0;
        var failCount = 0;

        foreach (var evt in batch)
        {
            try
            {
                var result = await _transport.SendAsync(targetRegion, evt, ct);

                if (result.Success)
                {
                    successCount++;
                    OnEventReplicated?.Invoke(this, new ReplicationEventArgs
                    {
                        Event = evt,
                        TargetRegions = new List<string> { targetRegion }
                    });
                }
                else if (result.ConflictDetected)
                {
                    await ResolveConflictAsync(evt, result.ConflictingEvent!, ct);
                }
                else
                {
                    failCount++;
                    state.ConsecutiveFailures++;
                }
            }
            catch (Exception ex)
            {
                failCount++;
                state.ConsecutiveFailures++;
                state.LastError = ex.Message;

                OnReplicationError?.Invoke(this, new ReplicationErrorEventArgs
                {
                    Event = evt,
                    TargetRegion = targetRegion,
                    Error = ex
                });
            }
        }

        if (successCount > 0)
        {
            state.LastSyncTime = DateTime.UtcNow;
            state.ConsecutiveFailures = 0;
        }

        // Update status based on failure threshold
        if (state.ConsecutiveFailures >= _options.FailureThreshold)
        {
            state.Status = ReplicationStatus.Error;
        }
    }

    private async Task ProcessEventImmediatelyAsync(ReplicationEvent evt, CancellationToken ct)
    {
        foreach (var targetRegion in _options.TargetRegions)
        {
            if (!_replicationStates.TryGetValue(targetRegion, out var state)) continue;
            if (state.Status == ReplicationStatus.Paused) continue;

            try
            {
                var result = await _transport.SendAsync(targetRegion, evt, ct);

                if (result.Success)
                {
                    state.LastSyncTime = DateTime.UtcNow;
                    OnEventReplicated?.Invoke(this, new ReplicationEventArgs
                    {
                        Event = evt,
                        TargetRegions = new List<string> { targetRegion }
                    });
                }
                else if (result.ConflictDetected)
                {
                    await ResolveConflictAsync(evt, result.ConflictingEvent!, ct);
                }
            }
            catch (Exception ex)
            {
                OnReplicationError?.Invoke(this, new ReplicationErrorEventArgs
                {
                    Event = evt,
                    TargetRegion = targetRegion,
                    Error = ex
                });
            }
        }
    }

    private async Task<ConflictResolution> ResolveConflictAsync(
        ReplicationEvent localEvent,
        ReplicationEvent remoteEvent,
        CancellationToken ct)
    {
        ConflictResolution resolution;

        switch (_options.ConflictResolutionStrategy)
        {
            case ConflictResolutionStrategy.LastWriteWins:
                resolution = localEvent.Timestamp > remoteEvent.Timestamp
                    ? ConflictResolution.UseLocal
                    : ConflictResolution.UseRemote;
                break;

            case ConflictResolutionStrategy.SourceRegionWins:
                resolution = localEvent.SourceRegion == _options.SourceRegion
                    ? ConflictResolution.UseLocal
                    : ConflictResolution.UseRemote;
                break;

            case ConflictResolutionStrategy.HigherVersionWins:
                resolution = string.Compare(localEvent.Version, remoteEvent.Version, StringComparison.Ordinal) > 0
                    ? ConflictResolution.UseLocal
                    : ConflictResolution.UseRemote;
                break;

            case ConflictResolutionStrategy.Merge:
                // For merge, we need custom logic based on entity type
                resolution = await MergeEventsAsync(localEvent, remoteEvent, ct);
                break;

            default:
                resolution = ConflictResolution.UseLocal;
                break;
        }

        OnConflictResolved?.Invoke(this, new ConflictResolvedEventArgs
        {
            LocalEvent = localEvent,
            RemoteEvent = remoteEvent,
            Resolution = resolution
        });

        return resolution;
    }

    private Task<ConflictResolution> MergeEventsAsync(
        ReplicationEvent localEvent,
        ReplicationEvent remoteEvent,
        CancellationToken ct)
    {
        // Default merge strategy: prefer local for most recent field changes
        // In production, this would use field-level timestamps for true CRDT merge
        return Task.FromResult(
            localEvent.Timestamp > remoteEvent.Timestamp
                ? ConflictResolution.UseLocal
                : ConflictResolution.UseRemote);
    }

    private async void CheckReplicationHealth(object? state)
    {
        if (_disposed) return;

        foreach (var kvp in _replicationStates)
        {
            var regionId = kvp.Key;
            var regionState = kvp.Value;
            var lag = GetReplicationLag(regionId);

            if (lag > _options.MaxAcceptableLag && regionState.Status == ReplicationStatus.Active)
            {
                regionState.Status = ReplicationStatus.Lagging;
            }
            else if (lag <= _options.MaxAcceptableLag && regionState.Status == ReplicationStatus.Lagging)
            {
                regionState.Status = ReplicationStatus.Active;
            }

            // Attempt to reconnect error states
            if (regionState.Status == ReplicationStatus.Error &&
                DateTime.UtcNow - (regionState.LastErrorAt ?? DateTime.MinValue) > _options.RetryInterval)
            {
                try
                {
                    var connected = await _transport.TestConnectionAsync(regionId, CancellationToken.None);
                    if (connected)
                    {
                        regionState.Status = ReplicationStatus.Active;
                        regionState.ConsecutiveFailures = 0;
                        regionState.LastError = null;
                    }
                }
                catch
                {
                    regionState.LastErrorAt = DateTime.UtcNow;
                }
            }
        }
    }

    private static string ComputeHash<T>(T data)
    {
        var json = JsonSerializer.Serialize(data);
        var bytes = Encoding.UTF8.GetBytes(json);
#if NET461
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(bytes);
#else
        var hash = SHA256.HashData(bytes);
#endif
        return Convert.ToBase64String(hash);
    }

#if NET461
    private static readonly ThreadLocal<Random> ThreadLocalRandom = new(() => new Random());
#endif

    private static string GenerateVersion()
    {
        // Hybrid Logical Clock style version
        var timestamp = DateTime.UtcNow.Ticks;
#if NET461
        var random = ThreadLocalRandom.Value.Next(0, 10000);
#else
        var random = RandomNumberGenerator.GetInt32(0, 10000);
#endif
        return $"{timestamp:X16}{random:X4}";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _replicationTimer.Dispose();
        _healthCheckTimer.Dispose();
        _processLock.Dispose();
    }
}

/// <summary>
/// Data replicator options.
/// </summary>
public sealed class DataReplicatorOptions
{
    /// <summary>The source region identifier.</summary>
    public string SourceRegion { get; set; } = string.Empty;

    /// <summary>Target regions for replication.</summary>
    public List<string> TargetRegions { get; set; } = [];

    /// <summary>Interval between replication batches.</summary>
    public TimeSpan ReplicationInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>Interval between health checks.</summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Maximum events per batch.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>Conflict resolution strategy.</summary>
    public ConflictResolutionStrategy ConflictResolutionStrategy { get; set; } = ConflictResolutionStrategy.LastWriteWins;

    /// <summary>Maximum acceptable replication lag.</summary>
    public TimeSpan MaxAcceptableLag { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Number of consecutive failures before marking region as error.</summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>Retry interval for error states.</summary>
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromMinutes(1);
}

/// <summary>
/// Replication event.
/// </summary>
public sealed class ReplicationEvent
{
    public string EventId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public string DataHash { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public ReplicationPriority Priority { get; set; }
    public string SourceRegion { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// Replication priority.
/// </summary>
public enum ReplicationPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

/// <summary>
/// Replication state for a target region.
/// </summary>
public sealed class ReplicationState
{
    public string RegionId { get; set; } = string.Empty;
    public ReplicationStatus Status { get; set; }
    public DateTime LastSyncTime { get; set; }
    public string? LastError { get; set; }
    public DateTime? LastErrorAt { get; set; }
    public int ConsecutiveFailures { get; set; }
    public string? PauseReason { get; set; }
    public DateTime? PausedAt { get; set; }
}

/// <summary>
/// Replication status.
/// </summary>
public enum ReplicationStatus
{
    Initializing,
    Active,
    Syncing,
    Lagging,
    Paused,
    Error
}

/// <summary>
/// Conflict resolution strategy.
/// </summary>
public enum ConflictResolutionStrategy
{
    LastWriteWins,
    SourceRegionWins,
    HigherVersionWins,
    Merge
}

/// <summary>
/// Conflict resolution outcome.
/// </summary>
public enum ConflictResolution
{
    UseLocal,
    UseRemote,
    Merged
}

/// <summary>
/// Replication result.
/// </summary>
public sealed class ReplicationResult
{
    public string EventId { get; set; } = string.Empty;
    public bool Queued { get; set; }
    public DateTime QueuedAt { get; set; }
}

/// <summary>
/// Synchronization result.
/// </summary>
public sealed class SyncResult
{
    public bool Success { get; set; }
    public int SyncedCount { get; set; }
    public int FailedCount { get; set; }
    public List<ConflictInfo> Conflicts { get; set; } = [];
    public TimeSpan Duration { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Conflict information.
/// </summary>
public sealed class ConflictInfo
{
    public string EventId { get; set; } = string.Empty;
    public ConflictResolution Resolution { get; set; }
    public DateTime ResolvedAt { get; set; }
}

/// <summary>
/// Transport send result.
/// </summary>
public sealed class TransportSendResult
{
    public bool Success { get; set; }
    public bool ConflictDetected { get; set; }
    public ReplicationEvent? ConflictingEvent { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Replication event args.
/// </summary>
public sealed class ReplicationEventArgs : EventArgs
{
    public ReplicationEvent Event { get; set; } = new();
    public List<string> TargetRegions { get; set; } = [];
}

/// <summary>
/// Replication error event args.
/// </summary>
public sealed class ReplicationErrorEventArgs : EventArgs
{
    public ReplicationEvent Event { get; set; } = new();
    public string TargetRegion { get; set; } = string.Empty;
    public Exception Error { get; set; } = new Exception();
}

/// <summary>
/// Conflict resolved event args.
/// </summary>
public sealed class ConflictResolvedEventArgs : EventArgs
{
    public ReplicationEvent LocalEvent { get; set; } = new();
    public ReplicationEvent RemoteEvent { get; set; } = new();
    public ConflictResolution Resolution { get; set; }
}

/// <summary>
/// Replication paused event args.
/// </summary>
public sealed class ReplicationPausedEventArgs : EventArgs
{
    public string TargetRegion { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Replication resumed event args.
/// </summary>
public sealed class ReplicationResumedEventArgs : EventArgs
{
    public string TargetRegion { get; set; } = string.Empty;
}

/// <summary>
/// Interface for replication transport.
/// </summary>
public interface IReplicationTransport
{
    Task<TransportSendResult> SendAsync(string targetRegion, ReplicationEvent evt, CancellationToken ct = default);
    Task<bool> TestConnectionAsync(string targetRegion, CancellationToken ct = default);
}

/// <summary>
/// HTTP-based replication transport.
/// </summary>
public sealed class HttpReplicationTransport : IReplicationTransport, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, string> _regionEndpoints;
    private readonly TimeSpan _timeout;

    public HttpReplicationTransport(
        Dictionary<string, string> regionEndpoints,
        TimeSpan? timeout = null)
    {
        _regionEndpoints = regionEndpoints ?? throw new ArgumentNullException(nameof(regionEndpoints));
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
        _httpClient = new HttpClient { Timeout = _timeout };
    }

    public async Task<TransportSendResult> SendAsync(
        string targetRegion,
        ReplicationEvent evt,
        CancellationToken ct = default)
    {
        if (!_regionEndpoints.TryGetValue(targetRegion, out var endpoint))
        {
            return new TransportSendResult
            {
                Success = false,
                Error = $"Unknown region: {targetRegion}"
            };
        }

        try
        {
            var content = new StringContent(
                JsonSerializer.Serialize(evt),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync($"{endpoint}/replicate", content, ct);

            if (response.IsSuccessStatusCode)
            {
                return new TransportSendResult { Success = true };
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
#if NET461
                var conflictJson = await response.Content.ReadAsStringAsync();
#else
                var conflictJson = await response.Content.ReadAsStringAsync(ct);
#endif
                var conflictingEvent = JsonSerializer.Deserialize<ReplicationEvent>(conflictJson);

                return new TransportSendResult
                {
                    Success = false,
                    ConflictDetected = true,
                    ConflictingEvent = conflictingEvent
                };
            }

            return new TransportSendResult
            {
                Success = false,
                Error = $"HTTP {(int)response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            return new TransportSendResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    public async Task<bool> TestConnectionAsync(string targetRegion, CancellationToken ct = default)
    {
        if (!_regionEndpoints.TryGetValue(targetRegion, out var endpoint))
        {
            return false;
        }

        try
        {
            var response = await _httpClient.GetAsync($"{endpoint}/health", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
