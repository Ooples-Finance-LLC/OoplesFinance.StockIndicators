using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ChartmillOutputs(IReadOnlyList<Bar> bars, int length, int kind = 1, bool selected = false, double[]? externalCenter = null)
    {
        length = Math.Max(1, length);
        var input = bars.Select(b => ReferenceFraction.FromDouble(selected ? b.Close : ((ReferenceFraction.FromDouble(b.High) + ReferenceFraction.FromDouble(b.Low)) / new ReferenceFraction(2)).ToDouble())).ToArray();
        var center = externalCenter is null ? SmoothRocBankStage(input, length, kind) : externalCenter.Select(ReferenceFraction.FromDouble).ToArray();
        var ranges = bars.Select((b, i) =>
        {
            var high = ReferenceFraction.FromDouble(b.High); var low = ReferenceFraction.FromDouble(b.Low); var previous = ReferenceFraction.FromDouble(bars[i > 0 ? i - 1 : 0].Close);
            return RoundRocBankStage(new[] { high - low, (high - previous).Abs(), (low - previous).Abs() }.Max());
        }).ToArray();
        var atr = SmoothRocBankStage(ranges, length, kind); var factor = ReferenceFraction.FromDouble(Math.Sqrt(length));
        double[] Normalize(Func<Bar, double> price) => bars.Select((b, i) =>
        {
            var denominator = RoundRocBankStage(atr[i] * factor);
            return denominator.Sign == 0 ? 0 : Math.Max(-1, Math.Min(1, (RoundRocBankStage(ReferenceFraction.FromDouble(price(b)) - center[i]) / denominator).ToDouble()));
        }).ToArray();
        return Outputs(("Cmvc", Normalize(b => b.Close)), ("Cmvo", Normalize(b => b.Open)), ("Cmvh", Normalize(b => b.High)), ("Cmvl", Normalize(b => b.Low)));
    }
}
