namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Immutable snapshot of indicator values at a point in time.
/// </summary>
public sealed class IndicatorSnapshot
{
    private readonly Dictionary<SeriesHandle, double[]> _series;
    private readonly Dictionary<IndicatorKey, SeriesHandle> _keys;
    private readonly Func<SeriesHandle, double[]?>? _resolver;

    /// <summary>
    /// Creates a new indicator snapshot.
    /// </summary>
    public IndicatorSnapshot(
        Dictionary<SeriesHandle, double[]> series,
        Dictionary<IndicatorKey, SeriesHandle> keys,
        Func<SeriesHandle, double[]?>? resolver = null)
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
        if (_series.TryGetValue(handle, out var list))
        {
            values = list;
            return true;
        }

        if (_resolver != null)
        {
            var resolved = _resolver(handle);
            if (resolved != null)
            {
                _series[handle] = resolved;
                values = resolved;
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
