using System.Buffers;
using System.Collections;

namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// A high-performance buffer type that uses ArrayPool internally for zero heap allocations in hot paths.
/// Implements IReadOnlyList&lt;T&gt; for easy enumeration and indexing, and IDisposable to return the buffer to the pool.
/// </summary>
/// <typeparam name="T">The element type (must be a value type for performance).</typeparam>
public sealed class IndicatorBuffer<T> : IReadOnlyList<T>, IDisposable
    where T : struct
{
    private static readonly ArrayPool<T> SharedPool = ArrayPool<T>.Shared;

    private readonly ArrayPool<T> _pool;
    private T[]? _array;
    private int _count;
    private volatile bool _disposed;

    /// <summary>
    /// Initializes a new buffer with the specified capacity.
    /// </summary>
    /// <param name="capacity">The minimum capacity of the buffer.</param>
    /// <param name="pool">Optional custom array pool. Uses ArrayPool&lt;T&gt;.Shared if not specified.</param>
    public IndicatorBuffer(int capacity, ArrayPool<T>? pool = null)
    {
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be non-negative.");
        }

        _pool = pool ?? SharedPool;
        _array = capacity > 0 ? _pool.Rent(capacity) : Array.Empty<T>();
        _count = 0;
    }

    /// <summary>
    /// Initializes a new buffer from existing data.
    /// </summary>
    /// <param name="source">The source data to copy into the buffer.</param>
    /// <param name="pool">Optional custom array pool. Uses ArrayPool&lt;T&gt;.Shared if not specified.</param>
    public IndicatorBuffer(ReadOnlySpan<T> source, ArrayPool<T>? pool = null)
    {
        _pool = pool ?? SharedPool;
        _array = source.Length > 0 ? _pool.Rent(source.Length) : Array.Empty<T>();
        source.CopyTo(_array);
        _count = source.Length;
    }

    /// <summary>
    /// Gets the number of elements in the buffer.
    /// </summary>
    public int Count
    {
        get
        {
            ThrowIfDisposed();
            return _count;
        }
    }

    /// <summary>
    /// Gets the current capacity of the buffer.
    /// </summary>
    public int Capacity
    {
        get
        {
            ThrowIfDisposed();
            return _array?.Length ?? 0;
        }
    }

    /// <summary>
    /// Gets the element at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the element to get.</param>
    /// <returns>The element at the specified index.</returns>
    /// <exception cref="IndexOutOfRangeException">The index is out of range.</exception>
    public T this[int index]
    {
        get
        {
            ThrowIfDisposed();
            if ((uint)index >= (uint)_count)
            {
                throw new IndexOutOfRangeException($"Index {index} is out of range. Count: {_count}");
            }
            return _array![index];
        }
    }

    /// <summary>
    /// Returns a Span&lt;T&gt; over the buffer contents for high-performance internal access.
    /// </summary>
    /// <returns>A Span&lt;T&gt; over the buffer contents.</returns>
    public Span<T> AsSpan()
    {
        ThrowIfDisposed();
        return _array.AsSpan(0, _count);
    }

    /// <summary>
    /// Returns a Span&lt;T&gt; over a portion of the buffer contents.
    /// </summary>
    /// <param name="start">The start index.</param>
    /// <param name="length">The length of the span.</param>
    /// <returns>A Span&lt;T&gt; over the specified portion.</returns>
    public Span<T> AsSpan(int start, int length)
    {
        ThrowIfDisposed();
        if (start < 0 || start > _count)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }
        if (length < 0 || start + length > _count)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
        return _array.AsSpan(start, length);
    }

    /// <summary>
    /// Returns a Memory&lt;T&gt; over the buffer contents for async scenarios.
    /// </summary>
    /// <returns>A Memory&lt;T&gt; over the buffer contents.</returns>
    public Memory<T> AsMemory()
    {
        ThrowIfDisposed();
        return _array.AsMemory(0, _count);
    }

    /// <summary>
    /// Returns a Memory&lt;T&gt; over a portion of the buffer contents.
    /// </summary>
    /// <param name="start">The start index.</param>
    /// <param name="length">The length of the memory.</param>
    /// <returns>A Memory&lt;T&gt; over the specified portion.</returns>
    public Memory<T> AsMemory(int start, int length)
    {
        ThrowIfDisposed();
        if (start < 0 || start > _count)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }
        if (length < 0 || start + length > _count)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
        return _array.AsMemory(start, length);
    }

    /// <summary>
    /// Copies the buffer contents to a new user-owned List&lt;T&gt;.
    /// </summary>
    /// <returns>A new List&lt;T&gt; containing a copy of the buffer contents.</returns>
    public List<T> ToList()
    {
        ThrowIfDisposed();
        var list = new List<T>(_count);
        for (int i = 0; i < _count; i++)
        {
            list.Add(_array![i]);
        }
        return list;
    }

    /// <summary>
    /// Copies the buffer contents to a new user-owned array.
    /// </summary>
    /// <returns>A new array containing a copy of the buffer contents.</returns>
    public T[] ToArray()
    {
        ThrowIfDisposed();
        if (_count == 0)
        {
            return Array.Empty<T>();
        }
        var result = new T[_count];
        AsSpan().CopyTo(result);
        return result;
    }

    /// <summary>
    /// Asynchronously copies the buffer contents to a new user-owned List&lt;T&gt;.
    /// Yields to allow other tasks to run during large buffer operations.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that produces a new List&lt;T&gt; containing a copy of the buffer contents.</returns>
    public async Task<List<T>> ToListAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var list = new List<T>(_count);
        const int batchSize = 1024;
        for (int i = 0; i < _count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            list.Add(_array![i]);
            if (i > 0 && i % batchSize == 0)
            {
                await Task.Yield();
            }
        }
        return list;
    }

    /// <summary>
    /// Asynchronously copies the buffer contents to a new user-owned array.
    /// Yields to allow other tasks to run during large buffer operations.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that produces a new array containing a copy of the buffer contents.</returns>
    public async Task<T[]> ToArrayAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        if (_count == 0)
        {
            return Array.Empty<T>();
        }
        var result = new T[_count];
        const int batchSize = 4096;
        for (int offset = 0; offset < _count; offset += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int len = Math.Min(batchSize, _count - offset);
            AsSpan(offset, len).CopyTo(result.AsSpan(offset, len));
            if (offset + len < _count)
            {
                await Task.Yield();
            }
        }
        return result;
    }

#if !NET461
    /// <summary>
    /// Returns an async enumerable that iterates through the buffer.
    /// Useful for streaming processing scenarios.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>An IAsyncEnumerable that yields buffer elements.</returns>
    public async IAsyncEnumerable<T> AsAsyncEnumerable([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        int count = _count;
        var array = _array;
        const int yieldFrequency = 256;
        for (int i = 0; i < count && array is not null; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return array[i];
            if (i > 0 && i % yieldFrequency == 0)
            {
                await Task.Yield();
            }
        }
    }
#endif

    /// <summary>
    /// Adds an element to the end of the buffer, growing if necessary.
    /// </summary>
    /// <param name="item">The item to add.</param>
    public void Add(T item)
    {
        ThrowIfDisposed();
        EnsureCapacity(_count + 1);
        _array![_count++] = item;
    }

    /// <summary>
    /// Adds a range of elements to the buffer.
    /// </summary>
    /// <param name="items">The items to add.</param>
    public void AddRange(ReadOnlySpan<T> items)
    {
        ThrowIfDisposed();
        EnsureCapacity(_count + items.Length);
        items.CopyTo(_array.AsSpan(_count));
        _count += items.Length;
    }

    /// <summary>
    /// Sets an element at the specified index.
    /// </summary>
    /// <param name="index">The index at which to set the element.</param>
    /// <param name="value">The value to set.</param>
    public void SetAt(int index, T value)
    {
        ThrowIfDisposed();
        if ((uint)index >= (uint)_count)
        {
            throw new IndexOutOfRangeException($"Index {index} is out of range. Count: {_count}");
        }
        _array![index] = value;
    }

    /// <summary>
    /// Clears all elements from the buffer without returning it to the pool.
    /// </summary>
    public void Clear()
    {
        ThrowIfDisposed();
        if (_count > 0 && _array is not null)
        {
            Array.Clear(_array, 0, _count);
        }
        _count = 0;
    }

    /// <summary>
    /// Ensures the buffer has at least the specified capacity.
    /// </summary>
    /// <param name="capacity">The minimum capacity required.</param>
    public void EnsureCapacity(int capacity)
    {
        ThrowIfDisposed();
        if (_array is null || _array.Length < capacity)
        {
            var newArray = _pool.Rent(capacity);
            if (_array is not null && _count > 0)
            {
                Array.Copy(_array, newArray, _count);
                ReturnArrayToPool(_array);
            }
            _array = newArray;
        }
    }

    /// <summary>
    /// Returns an enumerator that iterates through the buffer.
    /// Thread-safe for concurrent reads.
    /// </summary>
    public IEnumerator<T> GetEnumerator()
    {
        ThrowIfDisposed();
        // Capture count at enumeration start for thread safety
        int count = _count;
        var array = _array;
        for (int i = 0; i < count && array is not null; i++)
        {
            yield return array[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Returns the buffer to the pool and clears the array.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        if (_array is not null && _array.Length > 0)
        {
            ReturnArrayToPool(_array);
        }
        _array = null;
        _count = 0;
    }

    private void ReturnArrayToPool(T[] array)
    {
        // Clear the array before returning to prevent data leakage
        _pool.Return(array, clearArray: true);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(IndicatorBuffer<T>));
        }
    }

    /// <summary>
    /// Creates an IndicatorBuffer from an existing collection.
    /// </summary>
    /// <param name="source">The source collection.</param>
    /// <param name="pool">Optional custom array pool.</param>
    /// <returns>A new IndicatorBuffer containing the source data.</returns>
    public static IndicatorBuffer<T> From(IEnumerable<T> source, ArrayPool<T>? pool = null)
    {
        if (source is ICollection<T> collection)
        {
            var buffer = new IndicatorBuffer<T>(collection.Count, pool);
            foreach (var item in collection)
            {
                buffer._array![buffer._count++] = item;
            }
            return buffer;
        }
        else
        {
            // Fall back to array copy for non-collection enumerables
            var array = source.ToArray();
            return new IndicatorBuffer<T>(array.AsSpan(), pool);
        }
    }

    /// <summary>
    /// Creates an IndicatorBuffer from an array without copying.
    /// The array ownership is transferred to the buffer.
    /// </summary>
    /// <param name="array">The array to wrap.</param>
    /// <param name="count">The number of valid elements in the array.</param>
    /// <param name="pool">The pool the array was rented from. If null, the array will not be returned to any pool on dispose.</param>
    /// <returns>A new IndicatorBuffer wrapping the array.</returns>
    internal static IndicatorBuffer<T> WrapPooledArray(T[] array, int count, ArrayPool<T>? pool)
    {
        var buffer = new IndicatorBuffer<T>(0, pool);
        buffer._array = array;
        buffer._count = count;
        return buffer;
    }
}
