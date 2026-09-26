using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RocBandOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); return RocBandOutputs(bars, Math.Max(1, Integer(options, "Length", 12)), Math.Max(1, Integer(options, "SmoothLength", 3)), AverageKind(options, 3));
    }
    internal static IReadOnlyDictionary<string, double[]> RocBandOutputs(IReadOnlyList<Bar> bars, int length, int smoothLength, int kind)
    {
        var returns = bars.Select((b, i) => i < length || bars[i - length].Close == 0 ? new ReferenceFraction(0) :
            (new ReferenceFraction(100) * (ReferenceFraction.FromDouble(b.Close) - ReferenceFraction.FromDouble(bars[i - length].Close)) / ReferenceFraction.FromDouble(bars[i - length].Close)).RoundExtendedBinary64()).ToArray();
        var upper = returns.Select((_, i) =>
        {
            var count = Math.Min(length, i + 1); var squares = new ReferenceFraction(0);
            foreach (var value in returns.Skip(i - count + 1).Take(count)) squares += value * value;
            return (squares / new ReferenceFraction(count)).SqrtToDouble();
        }).ToArray();
        return Outputs(("UpperBand", upper), ("MiddleBand", new double[bars.Count]), ("LowerBand", upper.Select(v => -v).ToArray()),
            ("Roc", SmoothRocBankStage(returns, smoothLength, kind).Select(v => v.ToDouble()).ToArray()));
    }
}
