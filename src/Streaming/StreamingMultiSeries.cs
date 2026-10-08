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

    internal MultiSeriesContext(SeriesStore store, SeriesAlignmentPolicy alignmentPolicy = SeriesAlignmentPolicy.Strict,
        TimeSpan? maximumBenchmarkAge = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        if (!Enum.IsDefined(typeof(SeriesAlignmentPolicy), alignmentPolicy)) throw new ArgumentOutOfRangeException(nameof(alignmentPolicy));
        if (maximumBenchmarkAge < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maximumBenchmarkAge));
        AlignmentPolicy = alignmentPolicy; MaximumBenchmarkAge = maximumBenchmarkAge;
    }

    public SeriesAlignmentPolicy AlignmentPolicy { get; }
    /// <summary>Maximum age of a committed benchmark in LastKnown mode; null permits any past observation.</summary>
    public TimeSpan? MaximumBenchmarkAge { get; }

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
        PairedSeriesAlignment.ValidateObservation(bar);
        if (_series.TryGetValue(key, out var previous) && previous.LatestFinal is { } final
            && (bar.EndTime <= final.EndTime || bar.EndTime.Kind != final.EndTime.Kind))
            throw new ArgumentException("Duplicate or out-of-order observations require reset and replay.", nameof(bar));
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
