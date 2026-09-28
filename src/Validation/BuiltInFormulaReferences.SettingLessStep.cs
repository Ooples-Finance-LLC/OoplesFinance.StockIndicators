using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] SettingLessStepOutputs(IReadOnlyList<Bar> bars)
    {
        var sum = new ReferenceFraction(0); var width = sum; var held = sum; var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var price = ReferenceFraction.FromDouble(bars[i].Close); var previous = i == 0 ? price : held;
            var delta = RoundRocBankStage(price - previous); var distance = delta.Sign < 0 ? new ReferenceFraction(0) - delta : delta;
            var denominator = RoundRocBankStage(distance + width); var gain = denominator.Sign == 0 ? 0 : (distance / denominator).ToDouble();
            var line = RoundRocBankStage(RoundRocBankStage(price * ReferenceFraction.FromDouble(gain)) + RoundRocBankStage(previous * ReferenceFraction.FromDouble(1 - gain)));
            var change = RoundRocBankStage(line - previous); if (change.Sign < 0) change = new ReferenceFraction(0) - change;
            sum = RoundRocBankStage(sum + change); width = RoundRocBankStage(RoundRocBankStage(sum / new ReferenceFraction(i + 1L)) * ReferenceFraction.FromDouble(1 + gain));
            held = line.CompareTo(RoundRocBankStage(previous + width)) > 0 || line.CompareTo(RoundRocBankStage(previous - width)) < 0 ? line : previous; result[i] = held.ToDouble();
        }
        return result;
    }
}
