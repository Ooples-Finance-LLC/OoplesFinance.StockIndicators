using OoplesFinance.StockIndicators.Indicators;
using R = OoplesFinance.StockIndicators.Validation.ReferenceFraction;
namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static (double[] Values, Signal[] Signals) CombSpectrumValues(IReadOnlyList<Bar> bars, int upper, int lower, double bandwidth)
    {
        upper = Math.Max(1, upper); lower = Math.Max(1, lower);
        var roof = new R[bars.Count]; RoofingValues(bars, upper, lower, false, roof);
        var powers = new List<(int Period, R[] Values)>();
        R At(R[] a, long i) => i < 0 ? new R(0) : a[(int)i];
        for (long period = lower; period <= upper; period++)
        {
            var angle = 2 * Math.PI * bandwidth / period;
            if (double.IsInfinity(angle))
            {
                var turns = R.FromDouble(bandwidth) / new R(period); var (n, d) = turns.Components;
                var remainder = (n % d + d) % d;
                angle = (new R(remainder) / new R(d)).ToDouble() * (2 * Math.PI);
            }
            var cosine = Math.Cos(angle);
            var decay = Math.Max(.01, Math.Min(.99, cosine <= 0 ? .01 : cosine / (1 + Math.Sqrt((1 - cosine) * (1 + cosine)))));
            var gain = R.FromDouble(.5 * (1 - decay)); var feedback = R.FromDouble(Math.Cos(2 * Math.PI / period) * (1 + decay));
            var damping = R.FromDouble(decay); var band = new R[bars.Count]; var energy = new R[bars.Count];
            for (var i = 0; i < bars.Count; i++)
            {
                band[i] = RoundRocBankStage(gain * (roof[i] - At(roof, i - 2)) + feedback * At(band, i - 1) - damping * At(band, i - 2));
                var sum = new R(0);
                // Oracle sums the actual stored trajectory, independently of delayed filters.
                for (var j = (int)Math.Max(0, i - period); j < i; j++) sum += band[j] * band[j];
                energy[i] = sum / (new R(period) * new R(period));
            }
            powers.Add(((int)period, energy));
        }
        var values = new double[bars.Count]; var signals = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var maximum = powers.Aggregate(new R(0), (m, p) => m.CompareTo(p.Values[i]) >= 0 ? m : p.Values[i]);
            var retained = powers.Where(p => maximum.Sign > 0 && p.Values[i].CompareTo(maximum / new R(2)) >= 0).ToArray();
            var total = retained.Aggregate(new R(0), (s, p) => s + p.Values[i]);
            var weighted = retained.Aggregate(new R(0), (s, p) => s + new R(p.Period) * p.Values[i]);
            values[i] = total.Sign == 0 ? 0 : (weighted / total).ToDouble();
            var slope = roof[i] - At(roof, i - 1); var previous = At(roof, i - 1) - At(roof, i - 2);
            signals[i] = slope.Sign > 0 ? slope.CompareTo(previous) > 0 ? Signal.StrongBuy : Signal.Buy
                : slope.Sign < 0 ? slope.CompareTo(previous) < 0 ? Signal.StrongSell : Signal.Sell : Signal.None;
        }
        return (values, signals);
    }
}
