using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IndicatorErrorBudget LogReturnsBudget { get; } = new(0, 8e-15, true);

    private static double ReferenceSameSignLogRatio(double current, double previous) =>
        current == 0 || previous == 0 || Math.Sign(current) != Math.Sign(previous) ? 0
            : (ReferenceFraction.FromDouble(Math.Abs(current)) / ReferenceFraction.FromDouble(Math.Abs(previous))).LogToDouble();

    internal static double[] ReferenceLogReturns(IReadOnlyList<Bar> bars, int length)
        => bars.Select((b, i) => i < length || b.Close <= 0 || bars[i - length].Close <= 0 ? 0
            : (ReferenceFraction.FromDouble(b.Close) / ReferenceFraction.FromDouble(bars[i - length].Close)).LogToDouble()).ToArray();
}
