using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? SwissArmy(IBuiltInIndicator indicator)
    {
        if (indicator.BatchName != IndicatorName.EhlersSwissArmyKnifeIndicator) return null;
        var options = indicator.CreateOptions(); return new("SmaFilter", new[] { "EmaFilter", "SmaFilter", "GaussFilter", "ButterFilter", "SmoothFilter", "HpFilter", "PhpFilter", "BpFilter", "BsFilter" }, bars => SwissArmyValues(bars, Integer(options, "Length", 20), Number(options, .1, "Delta")).Outputs);
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) SwissArmyValues(IReadOnlyList<Bar> bars, int length, double delta)
    {
        if (double.IsNaN(delta) || double.IsInfinity(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        length = Math.Max(1, length); ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var angle = Math.Max(.01, Math.Min(.99, 2 * Math.PI / length)); var bandwidth = Math.Max(.01, Math.Min(.99, 4 * Math.PI * (delta / length))); var cosine = Math.Cos(angle); var beta = 2.415 * (1 - cosine);
        var alpha = R(1 - cosine / (1 + Math.Sin(angle))); var gaussian = R(2 * beta / (Math.Sqrt(beta * beta + 2 * beta) + beta)); var pole = R(1) - gaussian; var band = R(Math.Cos(bandwidth) / (1 + Math.Sin(bandwidth))); var cos = R(cosine);
        var keys = new[] { "EmaFilter", "SmaFilter", "GaussFilter", "ButterFilter", "SmoothFilter", "HpFilter", "PhpFilter", "BpFilter", "BsFilter" }; var lines = keys.Select(_ => new ReferenceFraction[bars.Count]).ToArray(); var signals = new Signal[bars.Count];
        ReferenceFraction Price(int index) => index < 0 ? R(0) : R(bars[index].Close);
        ReferenceFraction Output(int slot, int index) => index < 0 ? R(0) : lines[slot][index];
        for (var i = 0; i < bars.Count; i++)
        {
            var x = Price(i); var p1 = Price(i - 1); var p2 = Price(i - 2); var binomial = (x + R(2) * p1 + p2) / R(4);
            lines[4][i] = RoundRocBankStage(binomial);
            if (i <= length)
            {
                foreach (var slot in new[] { 0, 1, 2, 3, 7, 8 }) lines[slot][i] = x; lines[5][i] = lines[6][i] = R(0);
            }
            else
            {
                ReferenceFraction Feedback(int slot) => R(2) * pole * Output(slot, i - 1) - pole * pole * Output(slot, i - 2);
                ReferenceFraction BandFeedback(int slot) => cos * (R(1) + band) * Output(slot, i - 1) - band * Output(slot, i - 2);
                lines[0][i] = RoundRocBankStage(alpha * x + (R(1) - alpha) * Output(0, i - 1));
                lines[1][i] = RoundRocBankStage(Output(1, i - 1) + (x - Price(i - length)) / R(length));
                lines[2][i] = RoundRocBankStage(gaussian * gaussian * x + Feedback(2));
                lines[3][i] = RoundRocBankStage(gaussian * gaussian * binomial + Feedback(3));
                lines[5][i] = RoundRocBankStage((R(1) - alpha / R(2)) * (x - p1) + (R(1) - alpha) * Output(5, i - 1));
                var highGain = R(1) - gaussian / R(2); lines[6][i] = RoundRocBankStage(highGain * highGain * (x - R(2) * p1 + p2) + Feedback(6));
                lines[7][i] = RoundRocBankStage((R(1) - band) / R(2) * (x - p2) + BandFeedback(7));
                lines[8][i] = RoundRocBankStage((R(1) + band) / R(2) * (x - R(2) * cos * p1 + p2) + BandFeedback(8));
            }
            var difference = lines[1][i] - Output(1, i - 1); var before = Output(1, i - 1) - Output(1, i - 2); var direction = difference.CompareTo(before);
            signals[i] = difference.Sign > 0 && direction > 0 ? Signal.StrongBuy : difference.Sign < 0 && direction < 0 ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None;
        }
        return (keys.Select((key, slot) => new { key, slot }).ToDictionary(x => x.key, x => lines[x.slot].Select(v => v.ToDouble()).ToArray()), signals);
    }
}
