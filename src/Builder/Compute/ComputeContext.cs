using System.Buffers;
using System.Collections.Concurrent;

namespace OoplesFinance.StockIndicators.Builder.Compute;

/// <summary>
/// Manages ArrayPool for a single IndicatorRuntime instance.
/// Provides efficient buffer allocation and tracking for indicator computations.
/// </summary>
/// <remarks>
/// <para>Each IndicatorRuntime owns a ComputeContext that manages buffer lifecycle.</para>
/// <para>Buffers are rented from ArrayPool.Shared for maximum efficiency.</para>
/// <para>When the context is disposed, all active buffers are returned to the pool.</para>
/// </remarks>
internal sealed class ComputeContext : IDisposable
{
    private readonly ArrayPool<double> _pool;
    private readonly ConcurrentDictionary<double[], byte> _activeBuffers;
    private volatile bool _disposed;
    private long _rentCount;
    private long _returnCount;

    /// <summary>
    /// Creates a new compute context using the shared ArrayPool.
    /// </summary>
    public ComputeContext() : this(ArrayPool<double>.Shared)
    {
    }

    /// <summary>
    /// Creates a new compute context using a custom ArrayPool.
    /// </summary>
    /// <param name="pool">The array pool to use for buffer allocation.</param>
    public ComputeContext(ArrayPool<double> pool)
    {
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        _activeBuffers = new ConcurrentDictionary<double[], byte>(ReferenceEqualityComparer.Instance);
    }

    /// <summary>
    /// Gets the number of buffers currently rented.
    /// </summary>
    public int ActiveBufferCount => _activeBuffers.Count;

    /// <summary>
    /// Gets the total number of rent operations.
    /// </summary>
    public long RentCount => Interlocked.Read(ref _rentCount);

    /// <summary>
    /// Gets the total number of return operations.
    /// </summary>
    public long ReturnCount => Interlocked.Read(ref _returnCount);

    /// <summary>
    /// Rents a buffer from the pool.
    /// </summary>
    /// <param name="length">The required logical length of the buffer.</param>
    /// <returns>A ComputeBuffer wrapping the rented array.</returns>
    /// <exception cref="ObjectDisposedException">The context has been disposed.</exception>
    public ComputeBuffer Rent(int length)
    {
        ThrowIfDisposed();

        if (length <= 0)
        {
            // Return empty buffer that doesn't need pooling
            return new ComputeBuffer(Array.Empty<double>(), 0, this);
        }

        var array = _pool.Rent(length);
        _activeBuffers.TryAdd(array, 0);
        Interlocked.Increment(ref _rentCount);

        return new ComputeBuffer(array, length, this);
    }

    /// <summary>
    /// Rents a buffer and initializes it with source data.
    /// </summary>
    /// <param name="source">The source data to copy into the buffer.</param>
    /// <returns>A ComputeBuffer containing a copy of the source data.</returns>
    public ComputeBuffer RentAndCopy(ReadOnlySpan<double> source)
    {
        if (source.IsEmpty)
        {
            return new ComputeBuffer(Array.Empty<double>(), 0, this);
        }

        var buffer = Rent(source.Length);
        source.CopyTo(buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Returns an array to the pool.
    /// </summary>
    /// <param name="array">The array to return.</param>
    internal void Return(double[] array)
    {
        if (array is null || array.Length == 0)
        {
            return;
        }

        if (_activeBuffers.TryRemove(array, out _))
        {
            // Don't clear the array - indicator data doesn't contain sensitive information
            // and clearing has a performance cost
            _pool.Return(array, clearArray: false);
            Interlocked.Increment(ref _returnCount);
        }
    }

    /// <summary>
    /// Disposes the context and returns all active buffers to the pool.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Return all active buffers
        foreach (var kvp in _activeBuffers)
        {
            _pool.Return(kvp.Key, clearArray: false);
            Interlocked.Increment(ref _returnCount);
        }

        _activeBuffers.Clear();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ComputeContext));
        }
    }

    /// <summary>
    /// Reference equality comparer for arrays to use as dictionary keys.
    /// </summary>
    private sealed class ReferenceEqualityComparer : IEqualityComparer<double[]>
    {
        public static readonly ReferenceEqualityComparer Instance = new();

        public bool Equals(double[]? x, double[]? y) => ReferenceEquals(x, y);

        public int GetHashCode(double[] obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
