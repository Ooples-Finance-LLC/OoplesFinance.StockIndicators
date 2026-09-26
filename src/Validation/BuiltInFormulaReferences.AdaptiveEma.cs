using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> AdaptiveEmaOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return AdaptiveEmaOutputs(bars, Math.Max(1, Integer(options, "Length", 10)), AverageKind(options, 1));
    }
    internal static IReadOnlyDictionary<string, double[]> AdaptiveEmaOutputs(IReadOnlyList<Bar> bars, int length, int kind)
    {
        var zero = new ReferenceFraction(0); var one = new ReferenceFraction(1); var two = new ReferenceFraction(2);
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var seeds = SmoothRocBankStage(prices, length, kind); var previous = zero; var values = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var high = ReferenceFraction.FromDouble(bars.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(length, i + 1)).Max(b => b.High));
            var low = ReferenceFraction.FromDouble(bars.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(length, i + 1)).Min(b => b.Low));
            var range = high - low; var distance = prices[i] - (high + low) / two;
            if (distance.Sign < 0) distance = zero - distance;
            var offset = range.Sign <= 0 ? 0 : Math.Min(1, (two * distance / range).ToDouble());
            var rate = ReferenceFraction.FromDouble((two / new ReferenceFraction(length + 1L)).ToDouble() * (1 + offset));
            previous = i <= length ? seeds[i] : (previous + rate * (prices[i] - previous)).RoundExtendedBinary64();
            values[i] = previous.ToDouble();
        }
        return Outputs(("Aema", values));
    }
}
