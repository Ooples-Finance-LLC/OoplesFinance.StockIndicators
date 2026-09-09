namespace OoplesFinance.StockIndicators.Builder.Resilience;

using System.Net.Http;

/// <summary>
/// Circuit breaker pattern implementation for handling transient failures.
/// Prevents cascading failures by failing fast when a service is unhealthy.
/// </summary>
public sealed class CircuitBreaker
{
    private readonly object _lockObj = new();
    private readonly int _failureThreshold;
    private readonly int _successThreshold;
    private readonly TimeSpan _openDuration;

    private CircuitState _state = CircuitState.Closed;
    private int _failureCount;
    private int _successCount;
    private DateTime _lastFailureTime;
    private DateTime _openedAt;
    private Exception? _lastException;

    /// <summary>Gets the current state of the circuit breaker.</summary>
    public CircuitState State
    {
        get
        {
            lock (_lockObj)
            {
                if (_state == CircuitState.Open && DateTime.UtcNow - _openedAt >= _openDuration)
                {
                    _state = CircuitState.HalfOpen;
                }
                return _state;
            }
        }
    }

    /// <summary>Gets the number of consecutive failures.</summary>
    public int FailureCount
    {
        get { lock (_lockObj) { return _failureCount; } }
    }

    /// <summary>Gets the last exception that caused a failure.</summary>
    public Exception? LastException
    {
        get { lock (_lockObj) { return _lastException; } }
    }

    /// <summary>
    /// Creates a new circuit breaker.
    /// </summary>
    /// <param name="failureThreshold">Number of failures before opening the circuit.</param>
    /// <param name="successThreshold">Number of successes in half-open state to close the circuit.</param>
    /// <param name="openDuration">How long the circuit stays open before testing again.</param>
    public CircuitBreaker(
        int failureThreshold = 5,
        int successThreshold = 2,
        TimeSpan? openDuration = null)
    {
        _failureThreshold = failureThreshold;
        _successThreshold = successThreshold;
        _openDuration = openDuration ?? TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Executes an action through the circuit breaker.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        EnsureCircuitAllowsExecution();

        try
        {
            var result = await action().ConfigureAwait(false);
            RecordSuccess();
            return result;
        }
        catch (Exception ex)
        {
            RecordFailure(ex);
            throw;
        }
    }

    /// <summary>
    /// Executes an action through the circuit breaker.
    /// </summary>
    public async Task ExecuteAsync(Func<Task> action)
    {
        EnsureCircuitAllowsExecution();

        try
        {
            await action().ConfigureAwait(false);
            RecordSuccess();
        }
        catch (Exception ex)
        {
            RecordFailure(ex);
            throw;
        }
    }

    /// <summary>
    /// Executes an action through the circuit breaker with fallback.
    /// </summary>
    public async Task<T> ExecuteWithFallbackAsync<T>(
        Func<Task<T>> action,
        Func<Exception?, Task<T>> fallback)
    {
        try
        {
            return await ExecuteAsync(action).ConfigureAwait(false);
        }
        catch (CircuitBreakerOpenException ex)
        {
            return await fallback(ex.InnerException).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return await fallback(ex).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Manually opens the circuit.
    /// </summary>
    public void Open()
    {
        lock (_lockObj)
        {
            _state = CircuitState.Open;
            _openedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Manually closes the circuit.
    /// </summary>
    public void Close()
    {
        lock (_lockObj)
        {
            _state = CircuitState.Closed;
            _failureCount = 0;
            _successCount = 0;
        }
    }

    /// <summary>
    /// Resets the circuit breaker to its initial state.
    /// </summary>
    public void Reset()
    {
        lock (_lockObj)
        {
            _state = CircuitState.Closed;
            _failureCount = 0;
            _successCount = 0;
            _lastException = null;
        }
    }

    private void EnsureCircuitAllowsExecution()
    {
        var currentState = State; // This may transition from Open to HalfOpen

        if (currentState == CircuitState.Open)
        {
            throw new CircuitBreakerOpenException(
                $"Circuit breaker is open. Last failure: {_lastException?.Message}",
                _lastException);
        }
    }

    private void RecordSuccess()
    {
        lock (_lockObj)
        {
            if (_state == CircuitState.HalfOpen)
            {
                _successCount++;
                if (_successCount >= _successThreshold)
                {
                    _state = CircuitState.Closed;
                    _failureCount = 0;
                    _successCount = 0;
                }
            }
            else
            {
                _failureCount = 0; // Reset failure count on success
            }
        }
    }

    private void RecordFailure(Exception ex)
    {
        lock (_lockObj)
        {
            _lastException = ex;
            _lastFailureTime = DateTime.UtcNow;
            _failureCount++;
            _successCount = 0;

            if (_state == CircuitState.HalfOpen)
            {
                _state = CircuitState.Open;
                _openedAt = DateTime.UtcNow;
            }
            else if (_failureCount >= _failureThreshold)
            {
                _state = CircuitState.Open;
                _openedAt = DateTime.UtcNow;
            }
        }
    }
}

/// <summary>
/// Circuit breaker states.
/// </summary>
public enum CircuitState
{
    /// <summary>Circuit is closed, requests flow through normally.</summary>
    Closed,

    /// <summary>Circuit is open, requests fail immediately.</summary>
    Open,

    /// <summary>Circuit is testing if the service has recovered.</summary>
    HalfOpen
}

/// <summary>
/// Exception thrown when the circuit breaker is open.
/// </summary>
public sealed class CircuitBreakerOpenException : Exception
{
    /// <summary>
    /// Creates a new circuit breaker open exception.
    /// </summary>
    public CircuitBreakerOpenException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Retry policy with configurable strategies.
/// </summary>
public sealed class RetryPolicy
{
    private readonly int _maxRetries;
    private readonly TimeSpan _initialDelay;
    private readonly double _backoffMultiplier;
    private readonly TimeSpan _maxDelay;
    private readonly Func<Exception, bool>? _shouldRetry;
    private readonly Action<int, Exception, TimeSpan>? _onRetry;

    /// <summary>
    /// Creates a new retry policy.
    /// </summary>
    public RetryPolicy(
        int maxRetries = 3,
        TimeSpan? initialDelay = null,
        double backoffMultiplier = 2.0,
        TimeSpan? maxDelay = null,
        Func<Exception, bool>? shouldRetry = null,
        Action<int, Exception, TimeSpan>? onRetry = null)
    {
        _maxRetries = maxRetries;
        _initialDelay = initialDelay ?? TimeSpan.FromMilliseconds(100);
        _backoffMultiplier = backoffMultiplier;
        _maxDelay = maxDelay ?? TimeSpan.FromSeconds(30);
        _shouldRetry = shouldRetry;
        _onRetry = onRetry;
    }

    /// <summary>
    /// Executes an action with retry.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        var delay = _initialDelay;

        for (var attempt = 0; attempt <= _maxRetries; attempt++)
        {
            try
            {
                return await action().ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < _maxRetries && ShouldRetry(ex))
            {
                _onRetry?.Invoke(attempt + 1, ex, delay);

                await Task.Delay(delay).ConfigureAwait(false);

                delay = TimeSpan.FromTicks(
                    Math.Min((long)(delay.Ticks * _backoffMultiplier), _maxDelay.Ticks));
            }
        }

        // This line is unreachable but needed for compiler
        throw new InvalidOperationException("Retry logic failed unexpectedly");
    }

    /// <summary>
    /// Executes an action with retry.
    /// </summary>
    public async Task ExecuteAsync(Func<Task> action)
    {
        await ExecuteAsync(async () =>
        {
            await action().ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);
    }

    private bool ShouldRetry(Exception ex)
    {
        if (_shouldRetry is not null)
        {
            return _shouldRetry(ex);
        }

        // Default: retry on transient failures
        return ex is TimeoutException
            || ex is HttpRequestException
            || ex is TaskCanceledException
            || ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates a policy with exponential backoff.
    /// </summary>
    public static RetryPolicy ExponentialBackoff(
        int maxRetries = 3,
        TimeSpan? initialDelay = null)
    {
        return new RetryPolicy(
            maxRetries: maxRetries,
            initialDelay: initialDelay ?? TimeSpan.FromMilliseconds(100),
            backoffMultiplier: 2.0);
    }

    /// <summary>
    /// Creates a policy with linear backoff.
    /// </summary>
    public static RetryPolicy LinearBackoff(
        int maxRetries = 3,
        TimeSpan? delay = null)
    {
        return new RetryPolicy(
            maxRetries: maxRetries,
            initialDelay: delay ?? TimeSpan.FromMilliseconds(500),
            backoffMultiplier: 1.0);
    }

    /// <summary>
    /// Creates a policy with no delay between retries.
    /// </summary>
    public static RetryPolicy Immediate(int maxRetries = 3)
    {
        return new RetryPolicy(
            maxRetries: maxRetries,
            initialDelay: TimeSpan.Zero,
            backoffMultiplier: 1.0);
    }
}

/// <summary>
/// Dead letter queue for failed operations.
/// </summary>
public sealed class DeadLetterQueue<T>
{
    private readonly Queue<DeadLetterItem<T>> _queue = new();
    private readonly object _lockObj = new();
    private readonly int _maxSize;
    private readonly Action<DeadLetterItem<T>>? _onEnqueue;

    /// <summary>Gets the number of items in the queue.</summary>
    public int Count
    {
        get { lock (_lockObj) { return _queue.Count; } }
    }

    /// <summary>
    /// Creates a new dead letter queue.
    /// </summary>
    public DeadLetterQueue(int maxSize = 1000, Action<DeadLetterItem<T>>? onEnqueue = null)
    {
        _maxSize = maxSize;
        _onEnqueue = onEnqueue;
    }

    /// <summary>
    /// Enqueues a failed item.
    /// </summary>
    public void Enqueue(T item, Exception exception, string? context = null)
    {
        var deadLetter = new DeadLetterItem<T>
        {
            Item = item,
            Exception = exception,
            Context = context ?? string.Empty,
            FailedAt = DateTime.UtcNow,
            RetryCount = 0
        };

        lock (_lockObj)
        {
            if (_queue.Count >= _maxSize)
            {
                _queue.Dequeue(); // Remove oldest item
            }
            _queue.Enqueue(deadLetter);
        }

        _onEnqueue?.Invoke(deadLetter);
    }

    /// <summary>
    /// Dequeues the next item for retry.
    /// </summary>
    public DeadLetterItem<T>? Dequeue()
    {
        lock (_lockObj)
        {
            return _queue.Count > 0 ? _queue.Dequeue() : null;
        }
    }

    /// <summary>
    /// Gets all items without removing them.
    /// </summary>
    public IReadOnlyList<DeadLetterItem<T>> PeekAll()
    {
        lock (_lockObj)
        {
            return _queue.ToList();
        }
    }

    /// <summary>
    /// Clears all items from the queue.
    /// </summary>
    public void Clear()
    {
        lock (_lockObj)
        {
            _queue.Clear();
        }
    }

    /// <summary>
    /// Re-enqueues an item with incremented retry count.
    /// </summary>
    public void Requeue(DeadLetterItem<T> item)
    {
        lock (_lockObj)
        {
            item.RetryCount++;
            item.LastRetryAt = DateTime.UtcNow;

            if (_queue.Count >= _maxSize)
            {
                _queue.Dequeue();
            }
            _queue.Enqueue(item);
        }
    }
}

/// <summary>
/// Item in the dead letter queue.
/// </summary>
public sealed class DeadLetterItem<T>
{
    /// <summary>Gets or sets the failed item.</summary>
    public T Item { get; set; } = default!;

    /// <summary>Gets or sets the exception that caused the failure.</summary>
    public Exception Exception { get; set; } = null!;

    /// <summary>Gets or sets additional context.</summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>Gets or sets when the item failed.</summary>
    public DateTime FailedAt { get; set; }

    /// <summary>Gets or sets the number of retry attempts.</summary>
    public int RetryCount { get; set; }

    /// <summary>Gets or sets when the last retry was attempted.</summary>
    public DateTime? LastRetryAt { get; set; }
}

/// <summary>
/// Resilient executor combining circuit breaker and retry.
/// </summary>
public sealed class ResilientExecutor
{
    private readonly CircuitBreaker _circuitBreaker;
    private readonly RetryPolicy _retryPolicy;

    /// <summary>
    /// Creates a new resilient executor.
    /// </summary>
    public ResilientExecutor(
        CircuitBreaker? circuitBreaker = null,
        RetryPolicy? retryPolicy = null)
    {
        _circuitBreaker = circuitBreaker ?? new CircuitBreaker();
        _retryPolicy = retryPolicy ?? RetryPolicy.ExponentialBackoff();
    }

    /// <summary>
    /// Executes an action with circuit breaker and retry.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        return await _circuitBreaker.ExecuteAsync(
            () => _retryPolicy.ExecuteAsync(action)
        ).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes an action with circuit breaker and retry.
    /// </summary>
    public async Task ExecuteAsync(Func<Task> action)
    {
        await _circuitBreaker.ExecuteAsync(
            () => _retryPolicy.ExecuteAsync(action)
        ).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a default resilient executor.
    /// </summary>
    public static ResilientExecutor Default => new(
        new CircuitBreaker(failureThreshold: 5, successThreshold: 2, openDuration: TimeSpan.FromSeconds(30)),
        RetryPolicy.ExponentialBackoff(maxRetries: 3));
}

/// <summary>
/// Timeout policy for operations.
/// </summary>
public sealed class TimeoutPolicy
{
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Creates a new timeout policy.
    /// </summary>
    public TimeoutPolicy(TimeSpan timeout)
    {
        _timeout = timeout;
    }

    /// <summary>
    /// Executes an action with timeout.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action)
    {
        using var cts = new CancellationTokenSource(_timeout);

        try
        {
            return await action(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            throw new TimeoutException($"Operation timed out after {_timeout.TotalSeconds}s");
        }
    }

    /// <summary>
    /// Executes an action with timeout.
    /// </summary>
    public async Task ExecuteAsync(Func<CancellationToken, Task> action)
    {
        using var cts = new CancellationTokenSource(_timeout);

        try
        {
            await action(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            throw new TimeoutException($"Operation timed out after {_timeout.TotalSeconds}s");
        }
    }
}

/// <summary>
/// Rate limiter using sliding window algorithm.
/// </summary>
public sealed class SlidingWindowRateLimiter
{
    private readonly Queue<DateTime> _requestTimestamps = new();
    private readonly object _lockObj = new();
    private readonly int _maxRequests;
    private readonly TimeSpan _window;

    /// <summary>
    /// Creates a new sliding window rate limiter.
    /// </summary>
    public SlidingWindowRateLimiter(int maxRequests, TimeSpan window)
    {
        _maxRequests = maxRequests;
        _window = window;
    }

    /// <summary>
    /// Checks if a request is allowed.
    /// </summary>
    public bool TryAcquire()
    {
        lock (_lockObj)
        {
            var now = DateTime.UtcNow;
            var cutoff = now - _window;

            // Remove old entries
            while (_requestTimestamps.Count > 0 && _requestTimestamps.Peek() < cutoff)
            {
                _requestTimestamps.Dequeue();
            }

            if (_requestTimestamps.Count < _maxRequests)
            {
                _requestTimestamps.Enqueue(now);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Waits until a request is allowed.
    /// </summary>
    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        while (!TryAcquire())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gets the time until the next request is allowed.
    /// </summary>
    public TimeSpan GetWaitTime()
    {
        lock (_lockObj)
        {
            if (_requestTimestamps.Count < _maxRequests)
            {
                return TimeSpan.Zero;
            }

            var oldest = _requestTimestamps.Peek();
            var waitUntil = oldest + _window;
            var wait = waitUntil - DateTime.UtcNow;

            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }
    }
}
