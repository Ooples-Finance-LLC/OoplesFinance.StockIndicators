using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ApirineRsiOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var kind = AverageKind(options, 6);
        var length = Integer(options, "Length", 14); var smoothing = Integer(options, "SmoothLength", 6);
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var mean = SmoothRocBankStage(prices, smoothing, kind);
        var residuals = new ReferenceFraction[bars.Count]; var zero = new ReferenceFraction(0);
        var retention = ReferenceFraction.FromDouble(1 - 1d / smoothing);
        for (var i = 0; i < residuals.Length; i++)
        {
            if (kind != 6) residuals[i] = RoundRocBankStage(prices[i] - mean[i]);
            else
            {
                var change = RoundRocBankStage(prices[i] - (i == 0 ? zero : prices[i - 1]));
                var combined = RoundRocBankStage(change + (i == 0 ? zero : residuals[i - 1]));
                residuals[i] = RoundRocBankStage(retention * combined);
            }
        }
        var gain = SmoothRocBankStage(residuals.Select(v => v.Sign > 0 ? v : zero).ToArray(), length, kind);
        var loss = SmoothRocBankStage(residuals.Select(v => v.Sign < 0 ? zero - v : zero).ToArray(), length, kind);
        var result = gain.Select((v, i) => (v + loss[i]).Sign == 0 ? 100 : (new ReferenceFraction(100) * v / (v + loss[i])).ToDouble()).ToArray();
        return Outputs(("Asrsi", result));
    }
}
