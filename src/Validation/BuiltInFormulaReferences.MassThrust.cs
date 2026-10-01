using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static readonly IndicatorErrorBudget MassThrustBudget = new(0, 4e-15, requireSameSign: true);
    internal static Dictionary<string, double[]> MassThrustOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return MassThrustValues(bars, Integer(options, "Length", 14), AverageKind(options, 3), indicator.BatchName == IndicatorName.MassThrustOscillator).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) MassThrustValues(IReadOnlyList<Bar> bars, int length, int kind, bool oscillator)
    {
        length = Math.Max(1, length);
        ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        ReferenceFraction Compact(ReferenceFraction value)
        {
            if (value.Sign == 0) return R(0);
            // Bound only recursive memory, using four independent binary64
            // residuals (about 212 bits), beyond production's 106-bit state.
            // Normalize first to preserve both exponent extremes. Finite-window
            // ratios, products, means and the current result remain rational.
            var factor = new ReferenceFraction(BigInteger.One << 512); var scale = R(1);
            var magnitude = Math.Abs(value.ToDouble());
            while (double.IsInfinity(magnitude) || magnitude >= Math.Pow(2, 512)) { value /= factor; scale *= factor; magnitude = Math.Abs(value.ToDouble()); }
            while (magnitude < Math.Pow(2, -256)) { value *= factor; scale /= factor; magnitude = Math.Abs(value.ToDouble()); }
            var total = R(0);
            for (var part = 0; part < 4; part++) { var component = R(value.ToDouble()); total += component; value -= component; }
            return total * scale;
        }
        var prices = bars.Select(b => R(b.Close)).ToArray(); var gains = new ReferenceFraction[bars.Count]; var losses = new ReferenceFraction[bars.Count];
        var up = new ReferenceFraction[bars.Count]; var down = new ReferenceFraction[bars.Count]; var line = new ReferenceFraction[bars.Count];
        ReferenceFraction Sum(ReferenceFraction[] values, int i)
        { var total = R(0); for (var j = Math.Max(0, i - length + 1); j <= i; j++) total += values[j]; return total; }
        for (var i = 0; i < bars.Count; i++)
        {
            var change = i == 0 ? R(0) : prices[i] - prices[i - 1];
            gains[i] = change.Sign > 0 ? change : R(0); losses[i] = change.Sign < 0 ? R(0) - change : R(0);
            var advances = Sum(gains, i); var declines = Sum(losses, i);
            up[i] = change.Sign > 0 ? R(bars[i].Volume) / advances : R(0); down[i] = change.Sign < 0 ? R(bars[i].Volume) / declines : R(0);
            var positive = advances * Sum(up, i); var negative = declines * Sum(down, i); var total = positive + negative;
            line[i] = oscillator ? total.Sign == 0 ? R(0) : R(100) * (positive - negative) / total : (positive - negative) / R(1000000);
        }
        var signals = new ReferenceFraction[bars.Count]; var previous = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            if (length == 1) signals[i] = line[i];
            else if (kind == 6) signals[i] = (previous * R(length - 1) + line[i]) / R(length);
            else if (kind == 3 && i >= length) signals[i] = (previous * R(length - 1) + R(2) * line[i]) / R(length + 1d);
            else if (kind == 1 && i + 1 < length) signals[i] = R(0);
            else
            {
                var sum = R(0); var start = Math.Max(0, i - length + 1);
                for (var j = start; j <= i; j++) sum += line[j] * R(kind == 2 ? length - i + j : 1);
                signals[i] = sum / (kind == 2 ? R(length) * R(length + 1d) / R(2) : R(i - start + 1));
            }
            previous = kind == 6 || kind == 3 && i >= length ? Compact(signals[i]) : signals[i];
        }
        if (kind is 4 or 5) signals = Average(line.Select(v => v.ToDouble()).ToArray(), length, kind).Select(R).ToArray();
        var trades = new Signal[bars.Count]; var oldSlope = R(0); var oldLine = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var slope = oscillator ? line[i] - signals[i] : signals[i]; var direction = slope.CompareTo(oldSlope);
            trades[i] = slope.Sign > 0 && direction > 0 ? Signal.StrongBuy : slope.Sign < 0 && direction < 0 ? Signal.StrongSell
                : slope.Sign > 0 || oscillator && oldLine.CompareTo(R(-50)) < 0 && line[i].CompareTo(R(-50)) > 0 ? Signal.Buy
                : slope.Sign < 0 || oscillator && oldLine.CompareTo(R(50)) > 0 && line[i].CompareTo(R(50)) < 0 ? Signal.Sell : Signal.None;
            oldSlope = slope; oldLine = line[i];
        }
        return (new Dictionary<string, double[]> { [oscillator ? "Mto" : "Mti"] = line.Select(v => v.ToDouble()).ToArray(), ["Signal"] = signals.Select(v => v.ToDouble()).ToArray() }, trades);
    }
}
