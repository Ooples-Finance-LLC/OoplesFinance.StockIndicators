using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static readonly IndicatorErrorBudget MovingAverageAdaptiveFilterBudget = new(0, 4e-15, requireSameSign: true);
    internal static Dictionary<string, double[]> MovingAverageAdaptiveFilterOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var o = indicator.CreateOptions();
        return MovingAverageAdaptiveFilterValues(Closes(bars), Integer(o, "Length", 10), Number(o, .15, "Filter"), Number(o, .667, "FastAlpha"), Number(o, .0645, "SlowAlpha")).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) MovingAverageAdaptiveFilterValues(double[] prices, int length, double filter, double fast, double slow)
    {
        length = Math.Max(1, length); ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        var zero = R(0); var previousAnchor = zero; var previousResidual = zero; var changes = new ReferenceFraction[prices.Length];
        ReferenceFraction Compact(ReferenceFraction value)
        {
            if (value.Sign == 0) return zero;
            var factor = new ReferenceFraction(BigInteger.One << 512); var scale = R(1); var magnitude = Math.Abs(value.ToDouble());
            while (double.IsInfinity(magnitude) || magnitude >= Math.Pow(2, 512)) { value /= factor; scale *= factor; magnitude = Math.Abs(value.ToDouble()); }
            while (magnitude < Math.Pow(2, -256)) { value *= factor; scale /= factor; magnitude = Math.Abs(value.ToDouble()); }
            var result = zero;
            for (var part = 0; part < 4; part++) { var component = R(value.ToDouble()); result += component; value -= component; }
            return result * scale;
        }
        ReferenceFraction Abs(ReferenceFraction v) => v.Sign < 0 ? zero - v : v;
        for (var i = 0; i < prices.Length; i++)
        {
            var er = zero;
            if (i >= length)
            {
                var travel = zero; for (var j = i - length + 1; j <= i; j++) travel += Abs(R(prices[j]) - R(prices[j - 1]));
                if (travel.Sign > 0) er = Abs(R(prices[i]) - R(prices[i - length])) / travel;
            }
            var alpha = R(slow) + er * (R(fast) - R(slow));
            var distance = i == 0 ? zero : R(prices[i]) - previousAnchor - previousResidual;
            changes[i] = alpha * alpha * distance;
            var gap = (alpha * alpha - R(1)) * distance; var mean = R(prices[i]) + gap;
            if (Abs(mean).CompareTo(Abs(gap)) < 0) { previousAnchor = zero; previousResidual = Compact(mean); }
            else { previousAnchor = R(prices[i]); previousResidual = Compact(gap); }
        }
        var values = new double[prices.Length]; var signals = new Signal[prices.Length];
        var ema = Average(prices, length, 3); var previousVariance = zero; var previousSlope = zero;
        for (var i = 0; i < prices.Length; i++)
        {
            var variance = zero;
            if (i + 1 >= length)
            {
                var sample = changes.Skip(i - length + 1).Take(length).ToArray();
                var mean = sample.Aggregate(zero, (sum, v) => sum + v) / R(length);
                variance = sample.Aggregate(zero, (sum, v) => sum + (v - mean) * (v - mean)) / R(length);
            }
            // Scale the variance before taking a root, independently of production.
            values[i] = (variance * R(filter) * R(filter)).SqrtToDouble();
            var slope = R(prices[i]) - R(ema[i]); var direction = slope.CompareTo(previousSlope);
            var active = filter == 0 || variance.CompareTo(previousVariance) >= 0; // NOSONAR: S1244 - A zero multiplier makes every volatility value exactly zero.
            signals[i] = !active ? Signal.None : slope.Sign > 0 && direction > 0 ? Signal.StrongBuy : slope.Sign < 0 && direction < 0 ? Signal.StrongSell
                : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
            previousVariance = variance; previousSlope = slope;
        }
        return (new Dictionary<string, double[]> { ["Maaf"] = values }, signals);
    }
}
