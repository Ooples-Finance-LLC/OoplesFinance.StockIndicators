using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> BayesianOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return BayesianValues(bars, Integer(options, "Length", 20), AverageKind(options, 1)).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) BayesianValues(IReadOnlyList<Bar> bars, int length, int kind, double multiplier = 2.5, double threshold = 15, double[]? externalMean = null)
    {
        length = Math.Max(1, length); ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var prices = bars.Select(b => R(b.Close)).ToArray(); var means = externalMean is null ? SmoothRocBankStage(prices, length, kind) : externalMean.Select(R).ToArray();
        var upper = new int[bars.Count]; var basis = new int[bars.Count]; var down = new double[bars.Count]; var up = new double[bars.Count]; var prime = new double[bars.Count]; var signals = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var window = Window(prices, i, length).ToArray(); var average = window.Aggregate(R(0), (a, b) => a + b) / new ReferenceFraction(window.Length);
            var variance = window.Aggregate(R(0), (a, b) => a + (b - average) * (b - average)) / new ReferenceFraction(window.Length);
            var deviation = i < length - 1 ? 0 : variance.SqrtToDouble();
            var band = (means[i] + R(deviation) * R(multiplier)).RoundExtendedBinary64(); upper[i] = prices[i].CompareTo(band); basis[i] = prices[i].CompareTo(means[i]);
            var upperSigns = Window(upper, i, length).ToArray(); var basisSigns = Window(basis, i, length).ToArray();
            double Evidence(int sign)
            {
                var first = upperSigns.Count(s => s == sign); var second = basisSigns.Count(s => s == sign);
                var firstTotal = Math.Max(1, upperSigns.Count(s => s != 0)); var secondTotal = Math.Max(1, basisSigns.Count(s => s != 0));
                var joint = new ReferenceFraction(first) * new ReferenceFraction(second); var other = new ReferenceFraction(firstTotal - first) * new ReferenceFraction(secondTotal - second);
                return (joint + other).Sign == 0 ? 0 : (joint / (joint + other)).ToDouble();
            }
            down[i] = Evidence(1); up[i] = Evidence(-1); var mass = R(down[i]) * R(up[i]); var opposite = (R(1) - R(down[i])) * (R(1) - R(up[i])); prime[i] = (mass + opposite).Sign == 0 ? 0 : (mass / (mass + opposite)).ToDouble();
            var pd = i == 0 ? 0 : down[i - 1]; var pu = i == 0 ? 0 : up[i - 1]; var pp = i == 0 ? 0 : prime[i - 1];
            var buy = prime[i] > threshold / 100 && pp == 0 || up[i] < 1 && pu == 1; var sell = prime[i] == 0 && pp > threshold / 100 || down[i] < 1 && pd == 1; // NOSONAR: S1244 - The reference preserves exact zero/one probability transitions, not a tolerance band.
            signals[i] = buy ? Signal.Buy : sell ? Signal.Sell : Signal.None;
        }
        return (new Dictionary<string, double[]> { ["SigmaProbsDown"] = down, ["SigmaProbsUp"] = up, ["ProbPrime"] = prime }, signals);
    }
}
