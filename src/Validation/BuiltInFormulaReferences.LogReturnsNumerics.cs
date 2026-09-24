using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IndicatorErrorBudget LogReturnsBudget { get; } = new(0, 8e-15, true);

    internal static double[] ReferenceLogReturns(IReadOnlyList<Bar> bars, int length)
        => bars.Select((b, i) => i < length || b.Close <= 0 || bars[i - length].Close <= 0 ? 0
            : (ReferenceFraction.FromDouble(b.Close) / ReferenceFraction.FromDouble(bars[i - length].Close)).LogToDouble()).ToArray();
}
