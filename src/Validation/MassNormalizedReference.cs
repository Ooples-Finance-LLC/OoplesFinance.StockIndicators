using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class MassNormalizedReference
{
    internal static double[] Calculate(IReadOnlyList<Bar> bars, double[] alphas)
    {
        var means = new double[alphas.Length];
        var masses = new double[alphas.Length];
        var residuals = Enumerable.Repeat(1d, alphas.Length).ToArray();
        var result = new double[bars.Count];
        var one = new ReferenceFraction(1);
        for (var i = 0; i < bars.Count; i++)
        {
            var input = bars[i].Close;
            var sum = new ReferenceFraction(0);
            for (var j = 0; j < alphas.Length; j++)
            {
                var a = ReferenceFraction.FromDouble(alphas[j]);
                var old = (one - a) * ReferenceFraction.FromDouble(masses[j]);
                var mass = a + old;
                var numerator =
                    a * ReferenceFraction.FromDouble(input)
                    + old * ReferenceFraction.FromDouble(means[j]);
                residuals[j] = residuals[j] > 1e-10 ? (1 - alphas[j]) * residuals[j] : 0;
                input = (numerator / (residuals[j] == 0 ? one : mass)).ToDouble();
                means[j] = input;
                masses[j] = residuals[j] == 0 ? 1 : mass.ToDouble();
                sum +=
                    new ReferenceFraction(
                        alphas.Length == 1 ? 1
                        : j == 0 || j == 2 ? 4
                        : j == 1 ? -6
                        : -1
                    ) * ReferenceFraction.FromDouble(input);
            }
            result[i] = sum.ToDouble();
            if (!FrameworkCompatibility.IsFinite(result[i]))
                break;
        }
        return result;
    }
}
