using System.Buffers;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class PooledRingBuffer<T> : IDisposable
{
    private T[] _buffer;
    private int _start;
    private int _count;
    private bool _disposed;

    public PooledRingBuffer(int capacity)
    {
        if (capacity < 1)
        {
            capacity = 1;
        }

        Capacity = capacity;
        _buffer = ArrayPool<T>.Shared.Rent(capacity);
    }

    public int Capacity { get; }
    public int Count => _count;

    public bool TryAdd(T value, out T removed)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PooledRingBuffer<T>));
        }

        if (_count < Capacity)
        {
            var index = (_start + _count) % Capacity;
            _buffer[index] = value;
            _count++;
            removed = default!;
            return false;
        }

        removed = _buffer[_start];
        _buffer[_start] = value;
        _start = (_start + 1) % Capacity;
        return true;
    }

    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _buffer[(_start + index) % Capacity];
        }
    }

    public void CopyTo(Span<T> destination)
    {
        if (destination.Length < _count)
        {
            throw new ArgumentException("Destination is too small.", nameof(destination));
        }

        for (var i = 0; i < _count; i++)
        {
            destination[i] = this[i];
        }
    }

    public void Clear()
    {
        _start = 0;
        _count = 0;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ArrayPool<T>.Shared.Return(_buffer, clearArray: true);
        _buffer = Array.Empty<T>();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~PooledRingBuffer()
    {
        if (_disposed)
        {
            return;
        }

        ArrayPool<T>.Shared.Return(_buffer, clearArray: true);
    }
}
