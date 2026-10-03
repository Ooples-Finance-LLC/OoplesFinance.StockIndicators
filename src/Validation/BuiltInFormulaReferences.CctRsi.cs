using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> CctRsiOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, int? signalLength = null)
    {
        var options = indicator.CreateOptions(); var kind = AverageKind(options, 3);
        var periods = new[] { Integer(options, "Length1", 5), Integer(options, "Length2", 8), Integer(options, "Length3", 13), Integer(options, "Length4", 14), Integer(options, "Length5", 21) };
        var strengths = periods.Select(n => RoundedPriceRsi(bars, n, kind)).ToArray();
        double[] Ratio(int bank, int numerator, int low, int high)
        {
            var values = strengths[bank]; var result = new double[values.Length];
            for (var i = 0; i < result.Length; i++)
            {
                var bottom = ReferenceFraction.FromDouble(Window(values, i, periods[high]).Max()) - ReferenceFraction.FromDouble(Window(values, i, periods[low]).Min());
                var top = new ReferenceFraction(100) * (ReferenceFraction.FromDouble(values[i]) - ReferenceFraction.FromDouble(Window(values, i, periods[numerator]).Min()));
                result[i] = bottom.Sign == 0 ? 0 : (top / bottom).ToDouble();
            }
            return result;
        }
        double[] Smooth(double[] values, int length) => SmoothStrengthStage(values.Select(ReferenceFraction.FromDouble).ToArray(), length, kind).Select(v => v.ToDouble()).ToArray();
        var first = Ratio(4, 1, 2, 2); var fast = Integer(options, "SmoothLength1", 3);
        return Outputs(("Type1", first), ("Type2", Ratio(4, 4, 4, 4)), ("Type3", Ratio(3, 3, 3, 3)),
            ("Type4", Smooth(Ratio(4, 2, 2, 1), Integer(options, "SmoothLength2", 8))),
            ("Type5", Smooth(Ratio(0, 0, 0, 0), fast)), ("Type6", Smooth(Ratio(2, 2, 2, 2), fast)),
            ("TypeCustom", Smooth(Ratio(1, 1, 1, 1), fast)), ("Signal", Smooth(first, signalLength ?? 9)));
    }
}
