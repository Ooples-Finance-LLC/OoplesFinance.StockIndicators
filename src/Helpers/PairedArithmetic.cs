using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Helpers;

// Paired components may exceed binary64 before cancellation or a bounded vote.
// Reuse the established binary64 stage rounding with an extended upper exponent.
internal sealed class PairedAverage : IDisposable
{
    private readonly TechnicalRatingAverage? _wide;
    private readonly IMovingAverageSmoother? _other;
    internal PairedAverage(MovingAvgType kind, int length)
    {
        if (StrengthWindow.Supports(kind)) _wide = new(kind, Math.Max(1, length));
        else _other = MovingAverageSmootherFactory.Create(kind, Math.Max(1, length));
    }
    internal TechnicalRatingValue Next(TechnicalRatingValue value, bool final) =>
        _wide is not null ? _wide.Next(value, final) : _other!.Next(value.Publish(), final);
    internal void Reset() { _wide?.Reset(); _other?.Reset(); }
    public void Dispose() { _wide?.Dispose(); _other?.Dispose(); }
}

internal sealed class PairedPmo
{
    private readonly int _first, _second;
    private double _previous;
    private TechnicalRatingValue _mean, _pmo;
    internal PairedPmo(int first, int second) { _first = Math.Max(1, first); _second = Math.Max(1, second); }
    internal TechnicalRatingValue Next(double value, bool final)
    {
        var roc = PairedRoc.Return(value, _previous);
        var mean = TechnicalRatingValue.Ratio((_first - 2L) * _mean.Units + 2 * roc.Units, _first);
        var scaled = mean * 10d;
        var pmo = TechnicalRatingValue.Ratio((_second - 2L) * _pmo.Units + 2 * scaled.Units, _second);
        if (final) { _previous = value; _mean = mean; _pmo = pmo; }
        return pmo;
    }
    internal void Reset() { _previous = 0; _mean = _pmo = default; }
}

internal sealed class PairedRoc
{
    private readonly int _length;
    private readonly Queue<double> _prices = new();
    internal PairedRoc(int length) => _length = Math.Max(1, length);
    internal TechnicalRatingValue Next(double value, bool final)
    {
        var previous = _prices.Count == _length ? _prices.Peek() : 0;
        var result = Return(value, previous);
        if (final) { if (_prices.Count == _length) _prices.Dequeue(); _prices.Enqueue(value); }
        return result;
    }
    internal static TechnicalRatingValue Return(double value, double previous) => previous == 0 ? default :
        TechnicalRatingValue.Ratio((ExactVarianceWindow.Units(value) - ExactVarianceWindow.Units(previous)) * 100 * TechnicalRatingValue.Unit,
            ExactVarianceWindow.Units(previous));
    internal void Reset() => _prices.Clear();
}

internal static class PairedOutput
{
    internal static double Publish(Type type, int slot, int index, TechnicalRatingValue value)
    {
        var result = value.Publish();
        if (double.IsInfinity(result) || double.IsNaN(result))
            throw new IndicatorOutputException(type, slot, index, result, IndicatorStartupPolicy.Finite);
        return result;
    }
}

internal static class PairedBatch
{
    internal static Dictionary<string, List<double>> Run(IReadOnlyList<double> primary, IReadOnlyList<double> benchmark,
        Func<SeriesKey, SeriesKey, IMultiSeriesIndicatorState> factory)
    {
        var p = new SeriesKey("PRIMARY", BarTimeframe.Minutes(1));
        var b = new SeriesKey("BENCHMARK", BarTimeframe.Minutes(1));
        var state = factory(p, b);
        using var lifetime = state as IDisposable;
        var context = new MultiSeriesContext(new SeriesStore());
        var result = new Dictionary<string, List<double>>();
        for (var i = 0; i < primary.Count; i++)
        {
            var time = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(i);
            OhlcvBar Bar(SeriesKey key, double value) => new(key.Symbol, key.Timeframe, time, time, value, value, value, value, 0, true);
            state.Update(context, b, Bar(b, benchmark[i]), true, false);
            var point = state.Update(context, p, Bar(p, primary[i]), true, true);
            if (!point.HasValue || point.Outputs is null) throw new InvalidOperationException("An aligned pair did not publish outputs.");
            foreach (var output in point.Outputs)
            {
                if (!result.TryGetValue(output.Key, out var values)) result[output.Key] = values = new List<double>(primary.Count);
                values.Add(output.Value);
            }
        }
        return result;
    }
}
