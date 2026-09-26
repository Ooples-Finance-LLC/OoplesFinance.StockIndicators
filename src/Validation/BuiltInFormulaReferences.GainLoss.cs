using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> GainLossOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        var length = Integer(options, "Length", 14);
        var intraday = indicator.BatchName == IndicatorName.ChandeIntradayMomentumIndex;
        var lag = Integer(options, "Momentum", 3);
        var zero = new ReferenceFraction(0);
        var changes = bars.Select((b, i) => !intraday && i < lag ? zero : RoundStrengthStage(
            ReferenceFraction.FromDouble(b.Close) - ReferenceFraction.FromDouble(intraday ? b.Open : bars[i - lag].Close))).ToArray();
        var gains = changes.Select(v => v.Sign > 0 ? v : zero).ToArray();
        var losses = changes.Select(v => v.Sign < 0 ? zero - v : zero).ToArray();
        var kind = AverageKind(options, 6);
        if (!intraday)
        {
            gains = SmoothStrengthStage(gains, length, kind);
            losses = SmoothStrengthStage(losses, length, kind);
        }
        var line = new double[bars.Count];
        for (var i = 0; i < line.Length; i++)
        {
            var up = zero; var down = zero;
            for (var j = intraday ? Math.Max(0, i - length + 1) : i; j <= i; j++)
            {
                up += gains[j]; down += losses[j];
            }
            var total = up + down;
            line[i] = total.Sign == 0 ? intraday ? 0 : 100 : (new ReferenceFraction(100) * up / total).ToDouble();
        }
        if (intraday) return Outputs(("Cimi", line));
        var signal = SmoothStrengthStage(line.Select(ReferenceFraction.FromDouble).ToArray(), length, kind).Select(v => v.ToDouble()).ToArray();
        var histogram = line.Select((v, i) => (ReferenceFraction.FromDouble(v) - ReferenceFraction.FromDouble(signal[i])).ToDouble()).ToArray();
        return Outputs(("Rmi", line), ("Signal", signal), ("Histogram", histogram));
    }
}
