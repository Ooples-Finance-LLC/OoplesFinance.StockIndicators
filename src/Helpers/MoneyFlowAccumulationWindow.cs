using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class MoneyFlowAccumulationWindow
{
    private ExactMeanAccumulator _total;
    internal RocBankValue Next(double high, double low, double close, double volume, bool commit)
    {
        var flow = Flow(high, low, close, volume);
        var total = _total; flow.AddTo(ref total);
        if (commit) _total = total;
        return RocBankValue.Round(total);
    }
#if !NETFRAMEWORK
    // Only the captured batch line opts into compact wide-sum publication.
    // Streaming callers retain Next and its original publication path.
    internal RocBankValue NextBatch(double high, double low, double close, double volume, bool commit)
    {
        var flow = BatchFlow(high, low, close, volume);
        var total = _total; flow.AddTo(ref total);
        if (commit) _total = total;
        double ordinary = total.SumWithCompactRounding();
        return !double.IsInfinity(ordinary) ? new RocBankValue(ordinary) : RocBankValue.Round(total);
    }

    private static RocBankValue BatchFlow(double high, double low, double close, double volume)
    {
        if (high == low) return default; // NOSONAR: exact degenerate-range identity.
        const double twiceMinimumNormal = 4.450147717014403e-308;
        if (double.IsFinite(high) && high >= twiceMinimumNormal
            && low >= high * .5 && close >= low && close <= high)
        {
            // These finite normal prices lie within a factor of two. Sterbenz
            // makes close-low, high-close and high-low exact, including tiny
            // differences. The lower guard also makes high*.5 an exact scaling.
            if (volume == 0) return default; // NOSONAR: finite-price zero-volume identity.
            if (Binary64ArithmeticCertificate.TryDifference(close - low, high - close, out double balance)
                && Binary64ArithmeticCertificate.TryProduct(balance, volume, out double product))
            {
                double value = product / (high - low);
                if (double.IsFinite(value)) return new RocBankValue(value);
            }
        }
        return Flow(high, low, close, volume);
    }
#endif
    internal static RocBankValue Flow(double high, double low, double close, double volume)
    {
        RocBankValue flow = default;
#pragma warning disable S1244 // Only an exactly zero range uses the degenerate result; nonzero subnormal ranges remain meaningful.
        if (high != low)
#pragma warning restore S1244
        {
#if !NETFRAMEWORK
            if (TryExactFlow(high, low, close, volume, out var ordinary)) return new RocBankValue(ordinary);
#endif
            var numerator = new ExactMeanAccumulator();
            numerator.AddProduct(close, volume, 2); numerator.AddProduct(high, volume, -1); numerator.AddProduct(low, volume, -1);
            for (var shift = 0; ; shift += 1024)
            {
                var denominator = new ExactMeanAccumulator(); var scale = BigInteger.One << shift;
                denominator.Add(high, scale); denominator.Add(low, -scale);
                var value = numerator.Ratio(denominator);
                if (!double.IsInfinity(value)) { flow = new RocBankValue(value, shift); break; }
            }
        }
        return flow;
    }
#if !NETFRAMEWORK
    private static bool TryExactFlow(double high, double low, double close, double volume, out double flow)
    {
        flow = 0;
        if (!Binary64ArithmeticCertificate.TryDifference(close, low, out var buying)
            || !Binary64ArithmeticCertificate.TryDifference(high, close, out var selling)
            || !Binary64ArithmeticCertificate.TryDifference(buying, selling, out var balance)
            || !Binary64ArithmeticCertificate.TryDifference(high, low, out var range)
            || !Binary64ArithmeticCertificate.TryProduct(balance, volume, out var product)) return false;
        flow = product / range;
        // Unpublished overflow uses RocBankValue's extended exponent instead.
        return double.IsFinite(flow);
    }
#endif
    internal void Reset() => _total = default;
}

internal sealed class MoneyFlowAverageWindow : IDisposable
{
    private readonly MoneyFlowAccumulationWindow _line = new();
    private readonly RocBankAverage? _first, _second;
    private readonly IMovingAverageSmoother? _firstFallback, _secondFallback;
    private readonly bool _oscillator;
    internal MoneyFlowAverageWindow(MovingAvgType kind, int firstLength, int? secondLength = null)
    {
        _oscillator = secondLength.HasValue;
        if (StrengthWindow.Supports(kind))
        {
            _first = new RocBankAverage(kind, firstLength, int.MaxValue);
            if (secondLength.HasValue) _second = new RocBankAverage(kind, secondLength.Value, int.MaxValue);
        }
        else
        {
            _firstFallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, firstLength));
            if (secondLength.HasValue) _secondFallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, secondLength.Value));
        }
    }
    internal (double Line, double Signal) Next(double high, double low, double close, double volume, bool commit,
        double? customerFirst = null, double? customerSecond = null)
    {
        var line = _line.Next(high, low, close, volume, commit);
        var first = customerFirst.HasValue ? new RocBankValue(customerFirst.Value) : _first is null
            ? new RocBankValue(_firstFallback!.Next(line.Publish(), commit)) : _first.Next(line, commit);
        var signal = new ExactMeanAccumulator(); first.AddTo(ref signal);
        if (_oscillator)
        {
            var second = customerSecond.HasValue ? new RocBankValue(customerSecond.Value) : _second is null
                ? new RocBankValue(_secondFallback!.Next(line.Publish(), commit)) : _second.Next(line, commit);
            second.AddTo(ref signal, -1);
        }
        return (line.Publish(), signal.Mean(1));
    }
    internal void Reset() { _line.Reset(); _first?.Reset(); _second?.Reset(); _firstFallback?.Reset(); _secondFallback?.Reset(); }
    public void Dispose() { _first?.Dispose(); _second?.Dispose(); _firstFallback?.Dispose(); _secondFallback?.Dispose(); }
}
