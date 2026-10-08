using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ResidualPressureOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var name = indicator.BatchName;
        var source = Closes(bars).Select(ReferenceFraction.FromDouble).ToArray();
        if (name == IndicatorName.EmaWaveIndicator)
        {
            double[] Wave(int period)
            {
                var mean = SmoothStrengthStage(source, period, 3);
                var residual = source.Select((v, i) => RoundStrengthStage(v - mean[i])).ToArray();
                return SmoothStrengthStage(residual, Integer(options, "SmoothLength", 4), 1).Select(v => v.ToDouble()).ToArray();
            }
            return Outputs(("Wa", Wave(Integer(options, "Length1", 5))), ("Wb", Wave(Integer(options, "Length2", 25))), ("Wc", Wave(Integer(options, "Length3", 50))));
        }
        var kind = AverageKind(options, name == IndicatorName.TraderPressureIndex ? 2 : 3);
        if (name == IndicatorName.ErgodicMeanDeviationIndicator)
        {
            var mean = SmoothStrengthStage(source, Integer(options, "Length1", 32), kind);
            var residual = source.Select((v, i) => RoundStrengthStage(v - mean[i])).ToArray();
            var first = SmoothStrengthStage(residual, Integer(options, "Length2", 5), kind);
            var line = SmoothStrengthStage(first, Integer(options, "Length3", 5), kind);
            var signal = SmoothStrengthStage(line, Integer(options, "SignalLength", 5), kind);
            return Outputs(("Emdi", line.Select(v => v.ToDouble()).ToArray()), ("Signal", signal.Select(v => v.ToDouble()).ToArray()));
        }
        var period = Integer(options, "Length1", 7); var rangePeriod = Integer(options, "Length2", 2);
        ReferenceFraction[] Pressure(bool bullish) => bars.Select((b, i) =>
        {
            var window = Window(bars, i, rangePeriod).ToArray();
            var range = ReferenceFraction.FromDouble(window.Max(v => v.High)) - ReferenceFraction.FromDouble(window.Min(v => v.Low));
            var h = ReferenceFraction.FromDouble(b.High) - ReferenceFraction.FromDouble(i == 0 ? 0 : bars[i - 1].High);
            var l = ReferenceFraction.FromDouble(b.Low) - ReferenceFraction.FromDouble(i == 0 ? 0 : bars[i - 1].Low);
            var magnitude = new ReferenceFraction(0);
            foreach (var v in new[] { h, l }) if (bullish ? v.Sign > 0 : v.Sign < 0) magnitude += v.Abs();
            var ratio = range.Sign == 0 ? 0 : Math.Min(1, (magnitude / range).ToDouble());
            return ReferenceFraction.FromDouble((ReferenceFraction.FromDouble(ratio) * new ReferenceFraction(100)).ToDouble());
        }).ToArray();
        var bulls = SmoothStrengthStage(Pressure(true), period, kind); var bears = SmoothStrengthStage(Pressure(false), period, kind);
        var net = bulls.Select((v, i) => RoundStrengthStage(v - bears[i])).ToArray();
        var smoothed = SmoothStrengthStage(net, Integer(options, "SmoothLength", 3), kind);
        return Outputs(("Tpx", smoothed.Select(v => v.ToDouble()).ToArray()), ("Bulls", bulls.Select(v => v.ToDouble()).ToArray()), ("Bears", bears.Select(v => v.ToDouble()).ToArray()));
    }
}
