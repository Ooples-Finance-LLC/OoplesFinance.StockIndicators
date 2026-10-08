using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string, double[]> VossPredictiveValues(IReadOnlyList<Bar> bars, int length, double predict, double bandwidth)
    {
        length = Math.Max(1, length); var order = (int)Math.Max(2, Math.Min(530, Math.Ceiling(3 * predict)));
        var phase = bandwidth * 2 * Math.PI / length;
        if (double.IsInfinity(phase)) phase = (ReferenceFraction.FromDouble(bandwidth) * ReferenceFraction.FromDouble(2 * Math.PI) / new ReferenceFraction(length)).ToDouble();
        var cosine = Math.Cos(phase); var decay = 1 / cosine - Math.Sqrt(1 / (cosine * cosine) - 1);
        var drive = ReferenceFraction.FromDouble(.5 * (1 - decay));
        var feedback = ReferenceFraction.FromDouble(Math.Cos(2 * Math.PI / length) * (1 + decay)); var tail = ReferenceFraction.FromDouble(decay);
        var filter = new ReferenceFraction[bars.Count]; var voss = new ReferenceFraction[bars.Count];
        ReferenceFraction Round(ReferenceFraction value) => RoundRocBankStage(value);
        for (var i = 0; i < bars.Count; i++)
        {
            filter[i] = i <= 5 ? new ReferenceFraction(0) : Round(Round(Round(drive * Round(ReferenceFraction.FromDouble(bars[i].Close) - ReferenceFraction.FromDouble(bars[i - 2].Close))) + Round(feedback * filter[i - 1])) - Round(tail * filter[i - 2]));
            var terms = new ReferenceFraction(0);
            for (var lag = Math.Min(order, i); lag >= 1; lag--)
                terms = Round(terms + Round(ReferenceFraction.FromDouble((order - lag + 1d) / order) * voss[i - lag]));
            voss[i] = Round(Round(ReferenceFraction.FromDouble((3d + order) / 2) * filter[i]) - terms);
        }
        return new() { { "Voss", voss.Select(v => v.ToDouble()).ToArray() }, { "Filt", filter.Select(v => v.ToDouble()).ToArray() } };
    }
}
