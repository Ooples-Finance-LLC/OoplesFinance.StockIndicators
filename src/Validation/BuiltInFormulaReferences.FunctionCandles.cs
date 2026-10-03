using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> FunctionCandlesOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return FunctionCandlesValues(bars, Integer(options, "Length", 14), AverageKind(options, 6)); }
    internal static IReadOnlyDictionary<string, double[]> FunctionCandlesValues(IReadOnlyList<Bar> bars, int length, int kind)
    {
        double[] Transform(Func<Bar, double> select) => RoundedPriceRsi(bars.Select(b => new Bar(b.Time, select(b), select(b), select(b), select(b), b.Volume)).ToArray(), Math.Max(1, length), kind);
        return Outputs(("Close", Transform(b => b.Close)), ("Open", Transform(b => b.Open)), ("High", Transform(b => b.High)), ("Low", Transform(b => b.Low)));
    }
}
