using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static readonly IndicatorErrorBudget NegativeVolumeDisparityBudget = new(0, 4e-15, requireSameSign: true);
    internal static Dictionary<string, double[]> NegativeVolumeDisparityOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return NegativeVolumeDisparityValues(bars, Integer(options, "Length", 33), Integer(options, "SignalLength", 4),
            AverageKind(options, 1), Number(options, 1.1, "Top"), Number(options, .9, "Bottom")).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals, double[] Index) NegativeVolumeDisparityValues(
        IReadOnlyList<Bar> bars, int length, int signalLength, int kind = 1, double top = 1.1, double bottom = .9,
        double[]? selected = null, double[]? externalPriceMean = null, double[]? externalIndexMean = null, double[]? externalSignalMean = null)
    {
        length = Math.Max(1, length); signalLength = Math.Max(1, signalLength);
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var zero = R(0); var one = R(1); var factor = new ReferenceFraction(BigInteger.One << 512);
        ReferenceFraction Compact(ReferenceFraction value)
        {
            if (value.Sign == 0) return zero;
            var scale = one; var magnitude = Math.Abs(value.ToDouble());
            while (double.IsInfinity(magnitude) || magnitude >= Math.Pow(2, 512)) { value /= factor; scale *= factor; magnitude = Math.Abs(value.ToDouble()); }
            while (magnitude < Math.Pow(2, -256)) { value *= factor; scale /= factor; magnitude = Math.Abs(value.ToDouble()); }
            var result = zero;
            for (var part = 0; part < 4; part++) { var component = R(value.ToDouble()); result += component; value -= component; }
            return result * scale;
        }
        ReferenceFraction Root(ReferenceFraction square)
        {
            var scale = one; var rootFactor = new ReferenceFraction(BigInteger.One << 256); var magnitude = square.ToDouble();
            while (double.IsInfinity(magnitude) || magnitude >= Math.Pow(2, 512)) { square /= factor; scale *= rootFactor; magnitude = square.ToDouble(); }
            while (magnitude < Math.Pow(2, -256)) { square *= factor; scale /= rootFactor; magnitude = square.ToDouble(); }
            var root = R(square.SqrtToDouble());
            // Two exact Newton corrections from the binary64 seed provide
            // over 200 bits, independently of production's 106-bit root ratio.
            for (var step = 0; step < 2; step++) root = (root + square / root) / R(2);
            // Store four binary64 residuals after refinement; window moments and
            // the cancellation discriminant remain exact.
            return Compact(root * scale);
        }
        ReferenceFraction[] Mean(ReferenceFraction[] values, int period)
        {
            if (kind is not (1 or 2 or 3 or 6)) return Average(values.Select(v => v.ToDouble()).ToArray(), period, kind).Select(R).ToArray();
            var result = new ReferenceFraction[values.Length]; var previous = zero;
            for (var i = 0; i < values.Length; i++)
            {
                if (period == 1) result[i] = values[i];
                else if (kind == 6) result[i] = Compact((previous * R(period - 1) + values[i]) / R(period));
                else if (kind == 3 && i >= period) result[i] = Compact((previous * R(period - 1) + R(2) * values[i]) / new ReferenceFraction(period + 1L));
                else if (kind == 1 && i + 1 < period) result[i] = zero;
                else
                {
                    var total = zero;
                    for (var j = Math.Max(0, i - period + 1); j <= i; j++) total += values[j] * R(kind == 2 ? period - i + j : 1);
                    result[i] = total / new ReferenceFraction(kind == 2 ? (long)period * (period + 1L) / 2 : Math.Min(i + 1, period));
                }
                previous = result[i];
            }
            return result;
        }
        var prices = (selected ?? Closes(bars)).Select(R).ToArray(); var indices = new ReferenceFraction[bars.Count]; var index = R(1000);
        for (var i = 0; i < bars.Count; i++)
        {
            if (i > 0 && prices[i - 1].Sign != 0 && bars[i].Volume < bars[i - 1].Volume)
                index = (index + index * (prices[i] - prices[i - 1]) / prices[i - 1].Abs()).RoundExtendedBinary64();
            indices[i] = index;
        }
        ReferenceFraction[] Coordinate(ReferenceFraction[] values, ReferenceFraction[] means)
        {
            var result = new ReferenceFraction[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                if (i + 1 < length) { result[i] = one; continue; }
                var sample = values.Skip(i - length + 1).Take(length).ToArray(); var mean = sample.Aggregate(zero, (sum, value) => sum + value) / R(length);
                var variance = sample.Aggregate(zero, (sum, value) => sum + (value - mean) * (value - mean)) / R(length);
                if (variance.Sign == 0) { result[i] = one; continue; }
                var delta = values[i] - means[i];
                if (delta.Sign == 0) { result[i] = R(1.5); continue; }
                var squaredZ = delta * delta / variance; var absoluteZ = Root(squaredZ);
                result[i] = delta.Sign > 0 ? R(1.5) + absoluteZ / R(4)
                    : (R(36) - squaredZ) / (R(4) * (R(6) + absoluteZ));
            }
            return result;
        }
        var priceMeans = externalPriceMean?.Select(R).ToArray() ?? Mean(prices, length);
        var indexMeans = externalIndexMean?.Select(R).ToArray() ?? Mean(indices, length);
        var pricePositions = Coordinate(prices, priceMeans); var indexPositions = Coordinate(indices, indexMeans);
        var line = pricePositions.Select((value, i) => indexPositions[i].Sign == 0 ? zero : value / indexPositions[i]).ToArray();
        var signal = externalSignalMean?.Select(R).ToArray() ?? Mean(line, signalLength);
        var trades = new Signal[bars.Count]; var previousLine = zero; var previousState = 0;
        for (var i = 0; i < line.Length; i++)
        {
            var state = previousLine.CompareTo(R(bottom)) < 0 && line[i].CompareTo(R(bottom)) > 0 || line[i].CompareTo(signal[i]) > 0 ? 1
                : previousLine.CompareTo(R(top)) > 0 && line[i].CompareTo(R(top)) < 0 || line[i].CompareTo(R(bottom)) < 0 ? -1 : previousState;
            trades[i] = state > 0 && state > previousState ? Signal.StrongBuy : state < 0 && state < previousState ? Signal.StrongSell
                : state > 0 ? Signal.Buy : state < 0 ? Signal.Sell : Signal.None;
            previousLine = line[i]; previousState = state;
        }
        return (new Dictionary<string, double[]> { ["Nvdi"] = line.Select(v => v.ToDouble()).ToArray(), ["Signal"] = signal.Select(v => v.ToDouble()).ToArray() }, trades, indices.Select(v => v.ToDouble()).ToArray());
    }
}
