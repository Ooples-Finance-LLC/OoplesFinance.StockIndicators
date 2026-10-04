using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static double[] VariableAverageReference(double[] values,int length) => CertifiedVariableReference(values,length);

    private static FormulaDefinition? VariableAverages(IBuiltInIndicator indicator)
    {
        var name = indicator.BatchName;
        if (name is not (IndicatorName.VariableMovingAverage or IndicatorName.Svama or IndicatorName.VariableMovingAverageBands)) return null;
        var options = indicator.CreateOptions();
        var length = Integer(options, "Length", 6);
        if (name == IndicatorName.VariableMovingAverage)
            return new("Vma", new[] { "Vma" }, bars => Outputs(("Vma", VariableAverageReference(Closes(bars), length))));
        if (name == IndicatorName.Svama)
            return new("Svama", new[] { "Svama" }, bars => SvamaOutputs(bars));
        var variable = options.GetType().GetProperty("MaType")!.GetValue(options) is MovingAvgType.VariableMovingAverage;
        var kind = AverageKind(options, 0);
        if (!variable && kind == 0) return null;
        return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
            CertifiedVariableBandsReference(bars,length,(MovingAvgType)options.GetType().GetProperty("MaType")!.GetValue(options)!,
                Number(options,1.5,"Mult"),(indicator as IIndicator)?.Source is not null));
    }
}
