using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoofingValues(IReadOnlyList<Bar> bars, int upper, int lower, bool original)
    {
        ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        ReferenceFraction Round(ReferenceFraction v) => RoundRocBankStage(v);
        var angle = Math.Min(Math.Sqrt(2) * Math.PI / Math.Max(1, upper), .99); var pole = Math.Cos(angle) / (1 + Math.Sin(angle));
        var driveGain = original ? ((1 + pole) / 2) * ((1 + pole) / 2) : pole * pole / 4;
        var lowAngle = Math.Sqrt(2) * Math.PI / Math.Max(1, lower); var radius = Math.Exp(-lowAngle);
        var feedback = R(2 * radius * Math.Cos(Math.Min(lowAngle, .99))); var decay = R(-radius * radius);
        var gap = R(2 * Math.Exp(-lowAngle / 2) * Math.Sinh(lowAngle / 2)); var sine = R(Math.Sin(Math.Min(lowAngle, .99) / 2));
        var gain = R((gap * gap + new ReferenceFraction(4) * R(radius) * sine * sine).ToDouble());
        var first = new ReferenceFraction[bars.Count]; var second = new ReferenceFraction[bars.Count]; var output = new ReferenceFraction[bars.Count];
        ReferenceFraction Price(int i) => i < 0 ? new ReferenceFraction(0) : R(bars[i].Close);
        ReferenceFraction At(ReferenceFraction[] values, int i) => i < 0 ? new ReferenceFraction(0) : values[i];
        for (var i = 0; i < bars.Count; i++)
        {
            var drive = Round(((Price(i) - Price(i - 1)) - (Price(i - 2) - Price(i - 3))) / new ReferenceFraction(2));
            first[i] = Round(R(driveGain) * drive + R(pole) * At(first, i - 1));
            second[i] = Round(first[i] + R(pole) * At(second, i - 1));
            output[i] = Round(gain * second[i] + feedback * At(output, i - 1) + decay * At(output, i - 2));
        }
        return output.Select(v => v.ToDouble()).ToArray();
    }
}
