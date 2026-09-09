using System.Buffers;

namespace OoplesFinance.StockIndicators.Builder.Compute;

/// <summary>
/// Pooled buffer that auto-returns to pool on dispose.
/// This is a lightweight struct for internal use in the compute layer.
/// </summary>
/// <remarks>
/// <para>This struct is designed for maximum performance in indicator computation hot paths.</para>
/// <para>It wraps a rented array and provides zero-allocation access through Memory and Span.</para>
/// <para>The buffer MUST be disposed to return the array to the pool.</para>
/// </remarks>
internal readonly struct ComputeBuffer : IDisposable
{
    private readonly double[] _array;
    private readonly int _length;
    private readonly ComputeContext _context;

    /// <summary>
    /// Creates a new compute buffer wrapping a rented array.
    /// </summary>
    /// <param name="array">The rented array from the pool.</param>
    /// <param name="length">The logical length of data in the array.</param>
    /// <param name="context">The compute context that owns the array pool.</param>
    internal ComputeBuffer(double[] array, int length, ComputeContext context)
    {
        _array = array;
        _length = length;
        _context = context;
    }

    /// <summary>
    /// Gets a ReadOnlyMemory view of the buffer contents.
    /// </summary>
    public ReadOnlyMemory<double> Memory => _array.AsMemory(0, _length);

    /// <summary>
    /// Gets a ReadOnlySpan view of the buffer contents.
    /// </summary>
    public ReadOnlySpan<double> Span => _array.AsSpan(0, _length);

    /// <summary>
    /// Gets a writable Span for filling the buffer during computation.
    /// </summary>
    internal Span<double> WritableSpan => _array.AsSpan(0, _length);

    /// <summary>
    /// Gets the underlying array for direct access.
    /// </summary>
    internal double[] Array => _array;

    /// <summary>
    /// Gets the logical length of data in the buffer.
    /// </summary>
    public int Length => _length;

    /// <summary>
    /// Returns the array to the pool.
    /// </summary>
    public void Dispose()
    {
        _context.Return(_array);
    }

    /// <summary>
    /// Copies the buffer contents to a new array.
    /// Use this when you need the data to outlive the buffer.
    /// </summary>
    /// <returns>A new array containing a copy of the buffer data.</returns>
    public double[] ToArray()
    {
        if (_length == 0)
        {
            return System.Array.Empty<double>();
        }

        var result = new double[_length];
        Span.CopyTo(result);
        return result;
    }
}
