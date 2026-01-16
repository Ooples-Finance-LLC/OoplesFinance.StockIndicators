using OoplesFinance.StockIndicators.Enums;

namespace OoplesFinance.StockIndicators.Streaming;

public enum SeriesAlignmentPolicy
{
    LastKnown,
    Strict
}

public readonly struct SeriesKey : IEquatable<SeriesKey>
{
    public SeriesKey(string symbol, BarTimeframe timeframe)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol is required.", nameof(symbol));
        }

        Symbol = symbol;
        Timeframe = timeframe ?? throw new ArgumentNullException(nameof(timeframe));
    }

    public string Symbol { get; }
    public BarTimeframe Timeframe { get; }

    public bool Equals(SeriesKey other)
    {
        return string.Equals(Symbol, other.Symbol, StringComparison.OrdinalIgnoreCase)
            && Timeframe.Equals(other.Timeframe);
    }

    public override bool Equals(object? obj)
    {
        return obj is SeriesKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (StringComparer.OrdinalIgnoreCase.GetHashCode(Symbol) * 397) ^ Timeframe.GetHashCode();
        }
    }

    public override string ToString()
    {
        return $"{Symbol}:{Timeframe}";
    }
}

public interface IMultiSeriesIndicatorState
{
    IndicatorName Name { get; }
    void Reset();
    MultiSeriesIndicatorStateResult Update(MultiSeriesContext context, SeriesKey series, OhlcvBar bar, bool isFinal,
        bool includeOutputs);
}

public readonly struct MultiSeriesIndicatorStateResult
{
    public MultiSeriesIndicatorStateResult(bool hasValue, double value, IReadOnlyDictionary<string, double>? outputs)
    {
        HasValue = hasValue;
        Value = value;
        Outputs = outputs;
    }

    public bool HasValue { get; }
    public double Value { get; }
    public IReadOnlyDictionary<string, double>? Outputs { get; }
}

public sealed class MultiSeriesContext
{
    private readonly SeriesStore _store;

    internal MultiSeriesContext(SeriesStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public bool TryGetLatest(SeriesKey key, out OhlcvBar bar)
    {
        return _store.TryGetLatest(key, out bar);
    }

    public bool TryGetLatestFinal(SeriesKey key, out OhlcvBar bar)
    {
        return _store.TryGetLatestFinal(key, out bar);
    }
}

public sealed class MultiSeriesIndicatorStateUpdate
{
    public MultiSeriesIndicatorStateUpdate(SeriesKey primarySeries, SeriesKey updatedSeries, bool isFinalBar,
        IndicatorName indicator, double value, IReadOnlyDictionary<string, double>? outputs)
    {
        PrimarySeries = primarySeries;
        UpdatedSeries = updatedSeries;
        IsFinalBar = isFinalBar;
        Indicator = indicator;
        Value = value;
        Outputs = outputs;
    }

    public SeriesKey PrimarySeries { get; }
    public SeriesKey UpdatedSeries { get; }
    public bool IsFinalBar { get; }
    public IndicatorName Indicator { get; }
    public double Value { get; }
    public IReadOnlyDictionary<string, double>? Outputs { get; }
}

internal sealed class SeriesStore
{
    private readonly Dictionary<SeriesKey, SeriesSnapshot> _series = new();

    public void Update(SeriesKey key, OhlcvBar bar)
    {
        if (!_series.TryGetValue(key, out var snapshot))
        {
            snapshot = new SeriesSnapshot();
            _series[key] = snapshot;
        }

        snapshot.Latest = bar;
        if (bar.IsFinal)
        {
            snapshot.LatestFinal = bar;
        }
    }

    public bool TryGetLatest(SeriesKey key, out OhlcvBar bar)
    {
        if (_series.TryGetValue(key, out var snapshot) && snapshot.Latest != null)
        {
            bar = snapshot.Latest;
            return true;
        }

        bar = null!;
        return false;
    }

    public bool TryGetLatestFinal(SeriesKey key, out OhlcvBar bar)
    {
        if (_series.TryGetValue(key, out var snapshot) && snapshot.LatestFinal != null)
        {
            bar = snapshot.LatestFinal;
            return true;
        }

        bar = null!;
        return false;
    }

    private sealed class SeriesSnapshot
    {
        public OhlcvBar? Latest { get; set; }
        public OhlcvBar? LatestFinal { get; set; }
    }
}
