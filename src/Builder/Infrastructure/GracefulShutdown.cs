using System.Collections.Concurrent;

namespace OoplesFinance.StockIndicators.Builder.Infrastructure;

/// <summary>
/// Manages graceful shutdown of application components.
/// Ensures proper cleanup and drain of active requests.
/// </summary>
public sealed class GracefulShutdownManager : IDisposable
{
    private readonly GracefulShutdownOptions _options;
    private readonly ConcurrentDictionary<string, IShutdownHandler> _handlers = new();
    private readonly ConcurrentDictionary<string, int> _handlerPriorities = new();
    private readonly SemaphoreSlim _shutdownLock = new(1, 1);
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly TaskCompletionSource<bool> _shutdownComplete = new();
    private ShutdownState _state = ShutdownState.Running;
    private DateTime? _shutdownRequestedAt;
    private int _activeRequests;

    /// <summary>
    /// Initializes a new instance of the GracefulShutdownManager.
    /// </summary>
    public GracefulShutdownManager(GracefulShutdownOptions? options = null)
    {
        _options = options ?? new GracefulShutdownOptions();

        // Register for process exit signals
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        Console.CancelKeyPress += OnCancelKeyPress;
    }

    /// <summary>
    /// Gets the current shutdown state.
    /// </summary>
    public ShutdownState State => _state;

    /// <summary>
    /// Gets the shutdown cancellation token.
    /// </summary>
    public CancellationToken ShutdownToken => _shutdownCts.Token;

    /// <summary>
    /// Gets a task that completes when shutdown is finished.
    /// </summary>
    public Task ShutdownCompleteTask => _shutdownComplete.Task;

    /// <summary>
    /// Gets whether shutdown has been requested.
    /// </summary>
    public bool IsShutdownRequested => _state != ShutdownState.Running;

    /// <summary>
    /// Gets the number of active requests.
    /// </summary>
    public int ActiveRequests => _activeRequests;

    /// <summary>
    /// Registers a shutdown handler with priority.
    /// </summary>
    public void RegisterHandler(string name, IShutdownHandler handler, int priority = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty", nameof(name));

        _handlers[name] = handler ?? throw new ArgumentNullException(nameof(handler));
        _handlerPriorities[name] = priority;
    }

    /// <summary>
    /// Unregisters a shutdown handler.
    /// </summary>
    public void UnregisterHandler(string name)
    {
        _handlers.TryRemove(name, out _);
        _handlerPriorities.TryRemove(name, out _);
    }

    /// <summary>
    /// Starts tracking a request.
    /// </summary>
    public RequestTracker? BeginRequest()
    {
        if (_state != ShutdownState.Running)
        {
            return null; // Don't accept new requests during shutdown
        }

        Interlocked.Increment(ref _activeRequests);
        return new RequestTracker(this);
    }

    /// <summary>
    /// Ends tracking a request.
    /// </summary>
    internal void EndRequest()
    {
        Interlocked.Decrement(ref _activeRequests);
    }

    /// <summary>
    /// Initiates graceful shutdown.
    /// </summary>
    public async Task<ShutdownResult> ShutdownAsync(
        ShutdownReason reason = ShutdownReason.Manual,
        CancellationToken ct = default)
    {
        await _shutdownLock.WaitAsync(ct);

        try
        {
            if (_state != ShutdownState.Running)
            {
                return new ShutdownResult
                {
                    Success = false,
                    Error = "Shutdown already in progress"
                };
            }

            _state = ShutdownState.ShuttingDown;
            _shutdownRequestedAt = DateTime.UtcNow;

            OnShutdownStarted?.Invoke(this, new ShutdownEventArgs
            {
                Reason = reason,
                Timestamp = _shutdownRequestedAt.Value
            });

            var result = new ShutdownResult
            {
                Reason = reason,
                StartedAt = _shutdownRequestedAt.Value
            };

            // Phase 1: Stop accepting new requests
            _state = ShutdownState.DrainingRequests;

            OnPhaseChanged?.Invoke(this, new ShutdownPhaseEventArgs
            {
                Phase = ShutdownPhase.DrainingRequests,
                Timestamp = DateTime.UtcNow
            });

            // Phase 2: Wait for active requests to complete
            var drainSuccess = await WaitForRequestDrainAsync(ct);
            if (!drainSuccess)
            {
                result.DrainedSuccessfully = false;
                result.RemainingRequests = _activeRequests;

                if (_options.ForceShutdownAfterTimeout)
                {
                    OnForceShutdown?.Invoke(this, new ForceShutdownEventArgs
                    {
                        RemainingRequests = _activeRequests,
                        Timestamp = DateTime.UtcNow
                    });
                }
                else
                {
                    result.Success = false;
                    result.Error = "Failed to drain all requests within timeout";
                    return result;
                }
            }
            else
            {
                result.DrainedSuccessfully = true;
            }

            // Phase 3: Execute shutdown handlers in priority order
            _state = ShutdownState.ExecutingHandlers;

            OnPhaseChanged?.Invoke(this, new ShutdownPhaseEventArgs
            {
                Phase = ShutdownPhase.ExecutingHandlers,
                Timestamp = DateTime.UtcNow
            });

            var handlerResults = await ExecuteHandlersAsync(ct);
            result.HandlerResults = handlerResults;

            // Phase 4: Final cleanup
            _state = ShutdownState.Cleanup;

            OnPhaseChanged?.Invoke(this, new ShutdownPhaseEventArgs
            {
                Phase = ShutdownPhase.Cleanup,
                Timestamp = DateTime.UtcNow
            });

            // Cancel the shutdown token
            _shutdownCts.Cancel();

            _state = ShutdownState.Completed;
            result.Success = handlerResults.Values.All(r => r.Success);
            result.CompletedAt = DateTime.UtcNow;
            result.TotalDuration = result.CompletedAt.Value - result.StartedAt;

            _shutdownComplete.TrySetResult(result.Success);

            OnShutdownCompleted?.Invoke(this, new ShutdownCompletedEventArgs
            {
                Result = result
            });

            return result;
        }
        finally
        {
            _shutdownLock.Release();
        }
    }

    /// <summary>
    /// Event raised when shutdown starts.
    /// </summary>
    public event EventHandler<ShutdownEventArgs>? OnShutdownStarted;

    /// <summary>
    /// Event raised when shutdown phase changes.
    /// </summary>
    public event EventHandler<ShutdownPhaseEventArgs>? OnPhaseChanged;

    /// <summary>
    /// Event raised when force shutdown occurs.
    /// </summary>
    public event EventHandler<ForceShutdownEventArgs>? OnForceShutdown;

    /// <summary>
    /// Event raised when shutdown completes.
    /// </summary>
    public event EventHandler<ShutdownCompletedEventArgs>? OnShutdownCompleted;

    /// <summary>
    /// Event raised when a handler completes.
    /// </summary>
    public event EventHandler<HandlerCompletedEventArgs>? OnHandlerCompleted;

    private async Task<bool> WaitForRequestDrainAsync(CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + _options.DrainTimeout;
        var checkInterval = TimeSpan.FromMilliseconds(100);

        while (DateTime.UtcNow < deadline)
        {
            if (_activeRequests == 0)
            {
                return true;
            }

            try
            {
                await Task.Delay(checkInterval, ct);
            }
            catch (OperationCanceledException)
            {
                return _activeRequests == 0;
            }
        }

        return _activeRequests == 0;
    }

    private async Task<Dictionary<string, HandlerResult>> ExecuteHandlersAsync(CancellationToken ct)
    {
        var results = new Dictionary<string, HandlerResult>();

        // Group handlers by priority and execute in order
        var orderedHandlers = _handlers
            .Select(kvp => new {
                Name = kvp.Key,
                Handler = kvp.Value,
                Priority = _handlerPriorities.TryGetValue(kvp.Key, out var p) ? p : 0
            })
            .OrderByDescending(h => h.Priority)
            .GroupBy(h => h.Priority)
            .ToList();

        foreach (var group in orderedHandlers)
        {
            // Execute handlers in same priority group concurrently
            var tasks = group.Select(async h =>
            {
                var startTime = DateTime.UtcNow;
                var result = new HandlerResult
                {
                    HandlerName = h.Name,
                    StartedAt = startTime
                };

                try
                {
                    using var timeout = new CancellationTokenSource(_options.HandlerTimeout);
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);

                    await h.Handler.OnShutdownAsync(linked.Token);

                    result.Success = true;
                    result.CompletedAt = DateTime.UtcNow;
                    result.Duration = result.CompletedAt.Value - startTime;
                }
                catch (OperationCanceledException)
                {
                    result.Success = false;
                    result.Error = "Handler timed out";
                    result.CompletedAt = DateTime.UtcNow;
                    result.Duration = result.CompletedAt.Value - startTime;
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = ex.Message;
                    result.Exception = ex;
                    result.CompletedAt = DateTime.UtcNow;
                    result.Duration = result.CompletedAt.Value - startTime;
                }

                OnHandlerCompleted?.Invoke(this, new HandlerCompletedEventArgs
                {
                    Result = result
                });

                return new KeyValuePair<string, HandlerResult>(h.Name, result);
            });

            var groupResults = await Task.WhenAll(tasks);

            foreach (var kvp in groupResults)
            {
                results[kvp.Key] = kvp.Value;
            }
        }

        return results;
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        if (_state == ShutdownState.Running)
        {
            // Blocking shutdown for process exit
            ShutdownAsync(ShutdownReason.ProcessExit).GetAwaiter().GetResult();
        }
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        if (_state == ShutdownState.Running)
        {
            e.Cancel = true; // Prevent immediate termination
            _ = ShutdownAsync(ShutdownReason.CtrlC);
        }
    }

    public void Dispose()
    {
        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        Console.CancelKeyPress -= OnCancelKeyPress;

        _shutdownCts.Dispose();
        _shutdownLock.Dispose();
    }
}

/// <summary>
/// Graceful shutdown options.
/// </summary>
public sealed class GracefulShutdownOptions
{
    /// <summary>Timeout for draining active requests.</summary>
    public TimeSpan DrainTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Timeout for individual shutdown handlers.</summary>
    public TimeSpan HandlerTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Whether to force shutdown after drain timeout.</summary>
    public bool ForceShutdownAfterTimeout { get; set; } = true;
}

/// <summary>
/// Shutdown handler interface.
/// </summary>
public interface IShutdownHandler
{
    Task OnShutdownAsync(CancellationToken ct = default);
}

/// <summary>
/// Shutdown state.
/// </summary>
public enum ShutdownState
{
    Running,
    ShuttingDown,
    DrainingRequests,
    ExecutingHandlers,
    Cleanup,
    Completed
}

/// <summary>
/// Shutdown phase.
/// </summary>
public enum ShutdownPhase
{
    DrainingRequests,
    ExecutingHandlers,
    Cleanup
}

/// <summary>
/// Shutdown reason.
/// </summary>
public enum ShutdownReason
{
    Manual,
    CtrlC,
    ProcessExit,
    HealthCheckFailed,
    MaintenanceWindow,
    ScaleDown,
    Update
}

/// <summary>
/// Shutdown result.
/// </summary>
public sealed class ShutdownResult
{
    public bool Success { get; set; }
    public ShutdownReason Reason { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? TotalDuration { get; set; }
    public bool DrainedSuccessfully { get; set; }
    public int RemainingRequests { get; set; }
    public Dictionary<string, HandlerResult> HandlerResults { get; set; } = [];
    public string? Error { get; set; }
}

/// <summary>
/// Handler result.
/// </summary>
public sealed class HandlerResult
{
    public string HandlerName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public string? Error { get; set; }
    public Exception? Exception { get; set; }
}

/// <summary>
/// Request tracker for tracking active requests.
/// </summary>
public sealed class RequestTracker : IDisposable
{
    private readonly GracefulShutdownManager _manager;
    private bool _disposed;

    internal RequestTracker(GracefulShutdownManager manager)
    {
        _manager = manager;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _manager.EndRequest();
    }
}

/// <summary>
/// Shutdown event args.
/// </summary>
public sealed class ShutdownEventArgs : EventArgs
{
    public ShutdownReason Reason { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Shutdown phase event args.
/// </summary>
public sealed class ShutdownPhaseEventArgs : EventArgs
{
    public ShutdownPhase Phase { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Force shutdown event args.
/// </summary>
public sealed class ForceShutdownEventArgs : EventArgs
{
    public int RemainingRequests { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Shutdown completed event args.
/// </summary>
public sealed class ShutdownCompletedEventArgs : EventArgs
{
    public ShutdownResult Result { get; set; } = new();
}

/// <summary>
/// Handler completed event args.
/// </summary>
public sealed class HandlerCompletedEventArgs : EventArgs
{
    public HandlerResult Result { get; set; } = new();
}

/// <summary>
/// Database connection shutdown handler.
/// </summary>
public sealed class DatabaseShutdownHandler : IShutdownHandler
{
    private readonly Func<CancellationToken, Task> _closeConnections;

    public DatabaseShutdownHandler(Func<CancellationToken, Task> closeConnections)
    {
        _closeConnections = closeConnections;
    }

    public async Task OnShutdownAsync(CancellationToken ct = default)
    {
        await _closeConnections(ct);
    }
}

/// <summary>
/// Message queue shutdown handler.
/// </summary>
public sealed class MessageQueueShutdownHandler : IShutdownHandler
{
    private readonly Func<CancellationToken, Task> _drainQueue;
    private readonly Func<CancellationToken, Task>? _closeConnection;

    public MessageQueueShutdownHandler(
        Func<CancellationToken, Task> drainQueue,
        Func<CancellationToken, Task>? closeConnection = null)
    {
        _drainQueue = drainQueue;
        _closeConnection = closeConnection;
    }

    public async Task OnShutdownAsync(CancellationToken ct = default)
    {
        await _drainQueue(ct);

        if (_closeConnection != null)
        {
            await _closeConnection(ct);
        }
    }
}

/// <summary>
/// Cache shutdown handler.
/// </summary>
public sealed class CacheShutdownHandler : IShutdownHandler
{
    private readonly Func<CancellationToken, Task>? _flushCache;
    private readonly Func<CancellationToken, Task>? _saveState;

    public CacheShutdownHandler(
        Func<CancellationToken, Task>? flushCache = null,
        Func<CancellationToken, Task>? saveState = null)
    {
        _flushCache = flushCache;
        _saveState = saveState;
    }

    public async Task OnShutdownAsync(CancellationToken ct = default)
    {
        if (_saveState != null)
        {
            await _saveState(ct);
        }

        if (_flushCache != null)
        {
            await _flushCache(ct);
        }
    }
}

/// <summary>
/// Background job shutdown handler.
/// </summary>
public sealed class BackgroundJobShutdownHandler : IShutdownHandler
{
    private readonly Func<CancellationToken, Task<int>> _waitForJobs;
    private readonly TimeSpan _maxWaitTime;

    public BackgroundJobShutdownHandler(
        Func<CancellationToken, Task<int>> waitForJobs,
        TimeSpan? maxWaitTime = null)
    {
        _waitForJobs = waitForJobs;
        _maxWaitTime = maxWaitTime ?? TimeSpan.FromSeconds(30);
    }

    public async Task OnShutdownAsync(CancellationToken ct = default)
    {
        using var timeout = new CancellationTokenSource(_maxWaitTime);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);

        try
        {
            var pendingJobs = await _waitForJobs(linked.Token);
            if (pendingJobs > 0)
            {
                // Log warning about incomplete jobs
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout reached, continue shutdown
        }
    }
}

/// <summary>
/// Subscription shutdown handler.
/// </summary>
public sealed class SubscriptionShutdownHandler : IShutdownHandler
{
    private readonly Func<CancellationToken, Task> _unsubscribe;

    public SubscriptionShutdownHandler(Func<CancellationToken, Task> unsubscribe)
    {
        _unsubscribe = unsubscribe;
    }

    public async Task OnShutdownAsync(CancellationToken ct = default)
    {
        await _unsubscribe(ct);
    }
}

/// <summary>
/// File system shutdown handler.
/// </summary>
public sealed class FileSystemShutdownHandler : IShutdownHandler
{
    private readonly Func<CancellationToken, Task>? _flushBuffers;
    private readonly Func<CancellationToken, Task>? _closeHandles;

    public FileSystemShutdownHandler(
        Func<CancellationToken, Task>? flushBuffers = null,
        Func<CancellationToken, Task>? closeHandles = null)
    {
        _flushBuffers = flushBuffers;
        _closeHandles = closeHandles;
    }

    public async Task OnShutdownAsync(CancellationToken ct = default)
    {
        if (_flushBuffers != null)
        {
            await _flushBuffers(ct);
        }

        if (_closeHandles != null)
        {
            await _closeHandles(ct);
        }
    }
}

/// <summary>
/// Trading system specific shutdown handler.
/// </summary>
public sealed class TradingSystemShutdownHandler : IShutdownHandler
{
    private readonly Func<CancellationToken, Task> _cancelPendingOrders;
    private readonly Func<CancellationToken, Task>? _closePositions;
    private readonly Func<CancellationToken, Task>? _disconnectFromBroker;
    private readonly bool _shouldClosePositions;

    public TradingSystemShutdownHandler(
        Func<CancellationToken, Task> cancelPendingOrders,
        Func<CancellationToken, Task>? closePositions = null,
        Func<CancellationToken, Task>? disconnectFromBroker = null,
        bool shouldClosePositions = false)
    {
        _cancelPendingOrders = cancelPendingOrders;
        _closePositions = closePositions;
        _disconnectFromBroker = disconnectFromBroker;
        _shouldClosePositions = shouldClosePositions;
    }

    public async Task OnShutdownAsync(CancellationToken ct = default)
    {
        // 1. Cancel all pending orders
        await _cancelPendingOrders(ct);

        // 2. Optionally close all positions
        if (_shouldClosePositions && _closePositions != null)
        {
            await _closePositions(ct);
        }

        // 3. Disconnect from broker
        if (_disconnectFromBroker != null)
        {
            await _disconnectFromBroker(ct);
        }
    }
}
