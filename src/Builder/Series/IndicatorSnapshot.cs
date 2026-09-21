namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Immutable snapshot of indicator values at a point in time.
/// Lazily-resolved values are cached separately to maintain immutability of the original series.
/// </summary>
public sealed class IndicatorSnapshot
{
    private readonly Dictionary<SeriesHandle, ReadOnlyMemory<double>> _series;
    private readonly Dictionary<IndicatorKey, SeriesHandle> _keys;
    private readonly Func<SeriesHandle, ReadOnlyMemory<double>?>? _resolver;
    private readonly object _cacheLock = new();
    private Dictionary<SeriesHandle, ReadOnlyMemory<double>>? _lazyCache;

    /// <summary>
    /// Creates a new indicator snapshot.
    /// </summary>
    /// <summary>
    /// Creates a snapshot over series the runtime already holds as memory.
    /// </summary>
    /// <remarks>
    /// The batch evaluator hands out the pooled buffer an indicator was computed into rather than a copy of
    /// it, so its currency is <see cref="ReadOnlyMemory{T}"/>. Taking <c>double[]</c> here would force that
    /// copy back, which is the whole cost this avoids.
    /// </remarks>
    public IndicatorSnapshot(
        Dictionary<SeriesHandle, ReadOnlyMemory<double>> series,
        Dictionary<IndicatorKey, SeriesHandle> keys,
        Func<SeriesHandle, ReadOnlyMemory<double>?>? resolver = null)
    {
        _series = series;
        _keys = keys;
        _resolver = resolver;
    }

    /// <summary>
    /// Tries to get a series by handle.
    /// </summary>
    public bool TryGetSeries(SeriesHandle handle, out ReadOnlyMemory<double> values)
    {
        // First check the immutable original series
        if (_series.TryGetValue(handle, out var list))
        {
            values = list;
            return true;
        }

        // Then check the lazy cache (thread-safe)
        lock (_cacheLock)
        {
            if (_lazyCache != null && _lazyCache.TryGetValue(handle, out var cached))
            {
                values = cached;
                return true;
            }
        }

        // Try to resolve lazily
        if (_resolver != null)
        {
            var resolved = _resolver(handle);
            if (resolved.HasValue)
            {
                // Store in separate lazy cache to maintain immutability of _series
                lock (_cacheLock)
                {
                    _lazyCache ??= new Dictionary<SeriesHandle, ReadOnlyMemory<double>>();
                    _lazyCache[handle] = resolved.Value;
                }
                values = resolved.Value;
                return true;
            }
        }

        values = ReadOnlyMemory<double>.Empty;
        return false;
    }

    /// <summary>
    /// Tries to get a series by indicator key.
    /// </summary>
    public bool TryGetSeries(IndicatorKey key, out ReadOnlyMemory<double> values)
    {
        if (_keys.TryGetValue(key, out var handle))
        {
            return TryGetSeries(handle, out values);
        }

        values = ReadOnlyMemory<double>.Empty;
        return false;
    }

    /// <summary>
    /// Gets a series by handle, returning empty if not found.
    /// </summary>
    public ReadOnlyMemory<double> GetSeries(SeriesHandle handle)
    {
        return TryGetSeries(handle, out var values) ? values : ReadOnlyMemory<double>.Empty;
    }

    /// <summary>
    /// Gets a series by indicator key, returning empty if not found.
    /// </summary>
    public ReadOnlyMemory<double> GetSeries(IndicatorKey key)
    {
        return TryGetSeries(key, out var values) ? values : ReadOnlyMemory<double>.Empty;
    }

    /// <summary>
    /// Gets the last value of a series by handle.
    /// </summary>
    public double GetLastValue(SeriesHandle handle)
    {
        if (TryGetSeries(handle, out var values) && values.Length > 0)
        {
            return values.Span[values.Length - 1];
        }
        return double.NaN;
    }

    /// <summary>
    /// Gets the last value of a series by indicator key.
    /// </summary>
    public double GetLastValue(IndicatorKey key)
    {
        if (TryGetSeries(key, out var values) && values.Length > 0)
        {
            return values.Span[values.Length - 1];
        }
        return double.NaN;
    }
}
