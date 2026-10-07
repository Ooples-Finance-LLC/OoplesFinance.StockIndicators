using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RoundedKaufmanTrajectory(IReadOnlyList<Bar> bars,
        int length, int fastLength = 2, int slowLength = 30)
    {
        var fast = ReferenceFraction.FromDouble((new ReferenceFraction(2) / new ReferenceFraction(fastLength + 1L)).ToDouble());
        var slow = ReferenceFraction.FromDouble((new ReferenceFraction(2) / new ReferenceFraction(slowLength + 1L)).ToDouble());
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var average = new double[bars.Count];
        var efficiency = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            if (i < length) { average[i] = bars[i].Close; continue; }
            var travel = new ReferenceFraction(0);
            for (var j = i - length + 1; j <= i; j++) travel += (prices[j] - prices[j - 1]).Abs();
            efficiency[i] = travel.Sign == 0 ? 0 : ((prices[i] - prices[i - length]).Abs() / travel).ToDouble();
            var er = ReferenceFraction.FromDouble(efficiency[i]);
            var gain = ReferenceFraction.FromDouble(((new ReferenceFraction(1) - er) * slow + er * fast).ToDouble());
            var squared = ReferenceFraction.FromDouble((gain * gain).ToDouble());
            var previous = ReferenceFraction.FromDouble(average[i - 1]);
            average[i] = ((new ReferenceFraction(1) - squared) * previous + squared * prices[i]).ToDouble();
        }
        return Outputs(("Kama", average), ("Er", efficiency));
    }
}
