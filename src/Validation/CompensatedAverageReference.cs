using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class CompensatedAverageReference
{
    internal static double[] Calculate(IReadOnlyList<Bar> bars, int period, int order)
    {
        var stages = Enumerable.Repeat(new ReferenceFraction(0), order).ToArray();
        var alpha = 2d / ((long)period + 1);
        var a = ReferenceFraction.FromDouble(alpha);
        var residual = 1d;
        var output = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            residual = residual > 1e-10 ? (1 - alpha) * residual : 0;
            var compensation = ReferenceFraction.FromDouble(
                residual > 1e-10 ? 1 / (1 - residual) : 1
            );
            var value = ReferenceFraction.FromDouble(bars[i].Close);
            var total = new ReferenceFraction(0);
            for (var stage = 0; stage < order; stage++)
            {
                stages[stage] = (
                    a * value + (new ReferenceFraction(1) - a) * stages[stage]
                ).RoundExtendedBinary64();
                value = (stages[stage] * compensation).RoundExtendedBinary64();
                var weight =
                    order == 2
                        ? (stage == 0 ? 2 : -1)
                        : (
                            stage == 0 ? 3
                            : stage == 1 ? -3
                            : 1
                        );
                total += new ReferenceFraction(weight) * value;
            }
            output[i] = total.ToDouble();
            if (!FrameworkCompatibility.IsFinite(output[i]))
                break;
        }
        return output;
    }
}
