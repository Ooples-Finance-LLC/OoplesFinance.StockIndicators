using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static int BoundedMeanKind(object options, int fallback)
    {
        var value = options.GetType().GetProperty("MaType")?.GetValue(options)
            ?? options.GetType().GetProperty("MovingAvgType")?.GetValue(options);
        if (value is MovingAvgType.ArnaudLegouxMovingAverage) return 20;
        if (value is MovingAvgType.VariableIndexDynamicAverage) return 19;
        if (value is MovingAvgType.EhlersHannMovingAverage) return 18;
        if (value is MovingAvgType.SineWeightedMovingAverage) return 16;
        if (value is MovingAvgType.NaturalMovingAverage) return 17;
        if (value is MovingAvgType.KaufmanAdaptiveMovingAverage) return 15;
        if (value is MovingAvgType.JsaMovingAverage) return 13;
        if (value is MovingAvgType.QuadraticMovingAverage) return 14;
        if (value is MovingAvgType.ParabolicWeightedMovingAverage) return 10;
        if (value is MovingAvgType.CubedWeightedMovingAverage) return 11;
        if (value is MovingAvgType.QuickMovingAverage) return 12;
        if (value is MovingAvgType.FibonacciWeightedMovingAverage) return 8;
        if (value is MovingAvgType.SquareRootWeightedMovingAverage) return 9;
        return value is MovingAvgType.SymmetricallyWeightedMovingAverage or MovingAvgType.EhlersTriangleMovingAverage
            ? 7 : AverageKind(options, fallback);
    }

    internal static bool HasBoundedTriangularMean(IBuiltInIndicator indicator) =>
        indicator.BatchName == IndicatorName.TriangularMovingAverage && BoundedMeanKind(indicator.CreateOptions(), 1) is 1 or 2 or 3 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 20;

    private static double[] RoundedBoundedStage(IReadOnlyList<double> values, int length, int kind)
    {
        if (kind is 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 20)
        {
            var bars = values.Select(value => new Bar(default, value, value, value, value, 0)).ToArray();
            return kind == 20 ? RoundedAlmaMean(bars, length) : kind == 19 ? RoundedVidya(bars, length) : kind == 18 ? RoundedHannMean(bars, length) : kind == 16 ? RoundedSineMean(bars, length) : kind == 17 ? RoundedNaturalMean(bars, length)
                : kind == 15 ? RoundedKaufmanTrajectory(bars, length)["Kama"]
                : kind == 13 ? RoundedJsaMean(bars, length) : kind == 14 ? RoundedRootMeanSquare(bars, length)
                : kind == 8 ? RoundedFibonacciMean(bars, length) : kind == 9 ? RoundedSquareRootMean(bars, length)
                : kind == 12 ? RoundedQuickMean(bars, length) : RoundedPowerMean(bars, length, kind - 8);
        }
        return kind == 7 ? RoundedSymmetricStage(values, length) : kind == 3
            ? RoundedEma(values, length) : kind == 6 ? RoundedWilderTrajectory(values, length) : ExactDeviationSignal(values, length, kind);
    }

    internal static double[] RoundedTriangularMean(IReadOnlyList<Bar> bars, int length, int kind)
    {
        double[] Stage(IReadOnlyList<double> values) => RoundedBoundedStage(values, length, kind);
        return Stage(Stage(Closes(bars)));
    }
}
