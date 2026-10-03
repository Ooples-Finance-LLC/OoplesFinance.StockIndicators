using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static (int High, int Low, int Kind) RoofingV1Options(IIndicatorSpecOptions options)
    {
        var kind = options.GetType().GetProperty("MaType")?.GetValue(options);
        return (Integer(options, "Length1", Integer(options, "HpLength", 48)), Integer(options, "Length2", Integer(options, "LpLength", 10)),
            kind is null || kind is MovingAvgType.Ehlers2PoleSuperSmootherFilterV1 ? 7 : AverageKind(options, 0));
    }
    internal static double[] RoofingV1Values(IReadOnlyList<Bar> bars, int high, int low, int kind)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        ReferenceFraction Round(ReferenceFraction value) => RoundRocBankStage(value);
        var period = Math.Max(1, high) * Math.Sqrt(2); var tangent = Math.Tan(Math.PI / period);
        var alpha = period <= 2 ? 2 : 2 * tangent / (1 + tangent); var pole = R(1 - alpha); var gain = R(Math.Pow(1 - alpha / 2, 2));
        var first = new ReferenceFraction[bars.Count]; var raw = new ReferenceFraction[bars.Count];
        ReferenceFraction At(ReferenceFraction[] values, int i) => i < 0 ? new ReferenceFraction(0) : values[i];
        ReferenceFraction Price(int i) => i < 0 ? new ReferenceFraction(0) : R(bars[i].Close);
        for (var i = 0; i < bars.Count; i++)
        {
            var drive = Round((Price(i) - Price(i - 1) - Price(i - 2) + Price(i - 3)) / new ReferenceFraction(2));
            first[i] = Round(gain * drive + pole * At(first, i - 1)); raw[i] = Round(first[i] + pole * At(raw, i - 1));
        }
        if (kind != 7) return SmoothRocBankStage(raw, Math.Max(1, low), kind).Select(v => v.ToDouble()).ToArray();
        var angle = Math.Sqrt(2) * Math.PI / Math.Max(2, low); var radius = R(Math.Exp(-angle)); var cosine = R(Math.Cos(angle));
        var one = new ReferenceFraction(1); var two = new ReferenceFraction(2);
        var feed = (one - radius) * (one - radius) + two * radius * (one - cosine);
        var output = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++) output[i] = i < 3 ? raw[i] : Round(feed * raw[i] + two * radius * cosine * output[i - 1] - radius * radius * output[i - 2]);
        return output.Select(v => v.ToDouble()).ToArray();
    }
}
