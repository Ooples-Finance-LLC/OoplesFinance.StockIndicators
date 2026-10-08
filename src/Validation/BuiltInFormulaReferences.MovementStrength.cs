using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] MovementStrengthOutputs(IReadOnlyList<Bar> bars, int length = 10, int movement = 3, int smoothing = 3, int kind = 2)
    {
        length = Math.Max(1, length); var lag = Math.Max(1, movement) - 1; var zero = new ReferenceFraction(0);
        var moves = bars.Select((b, i) => lag == 0 || i < lag || bars[i - lag].Close == 0 ? zero : RoundRocBankStage(RoundRocBankStage(RoundRocBankStage(ReferenceFraction.FromDouble(b.Close) - ReferenceFraction.FromDouble(bars[i - lag].Close)) / new ReferenceFraction(lag)) / ReferenceFraction.FromDouble(bars[i - lag].Close))).ToArray();
        var averages = SmoothRocBankStage(moves, length, kind); var centered = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var window = Window(averages, i, Math.Max(2, length)).ToArray(); var high = window.Aggregate((a, b) => a.CompareTo(b) >= 0 ? a : b); var low = window.Aggregate((a, b) => a.CompareTo(b) <= 0 ? a : b);
            var delta = RoundRocBankStage(averages[i] - low); var range = RoundRocBankStage(high - low);
            var stochastic = range.Sign == 0 ? 0 : Math.Max(0, Math.Min(100, (delta / range).ToDouble() * 100)); centered[i] = ReferenceFraction.FromDouble(stochastic * 2 - 100);
        }
        return SmoothRocBankStage(centered, Math.Max(1, smoothing), kind).Select(v => v.ToDouble()).ToArray();
    }
}
