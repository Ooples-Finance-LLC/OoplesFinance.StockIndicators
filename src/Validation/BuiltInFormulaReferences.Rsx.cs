using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RsxOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator) =>
        RsxValues(bars, Integer(indicator.CreateOptions(), "Length", 14)).Outputs;
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) RsxValues(IReadOnlyList<Bar> bars, int length)
    {
        length = Math.Max(1, length);
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        ReferenceFraction Round(ReferenceFraction value) => value.RoundExtendedBinary64();
        var zero = R(0); var gain = R(3d / (length + 2d)); var memory = R(1 - 3d / (length + 2d));
        var scaled = bars.Select(b => Round(R(100) * R(b.Close))).ToArray();
        var changes = scaled.Select((v, i) => Round(v - (i == 0 ? zero : scaled[i - 1]))).ToArray();
        ReferenceFraction[] Smooth(ReferenceFraction[] input)
        {
            var result = new ReferenceFraction[input.Length];
            for (var i = 0; i < input.Length; i++) result[i] = Round(gain * input[i] + memory * (i == 0 ? zero : result[i - 1]));
            return result;
        }
        ReferenceFraction[] Cascade(ReferenceFraction[] input)
        {
            for (var stage = 0; stage < 3; stage++)
            {
                var first = Smooth(input); var second = Smooth(first);
                input = first.Select((v, i) => Round(R(1.5) * v - R(.5) * second[i])).ToArray();
            }
            return input;
        }
        var signed = Cascade(changes); var absolute = Cascade(changes.Select(v => v.Abs()).ToArray());
        var values = Enumerable.Range(0, bars.Count).Select(i =>
        {
            if (i < 5 || absolute[i].Sign <= 0) return 50d;
            var ratio = R(50) + R(50) * signed[i] / absolute[i];
            return Math.Max(0, Math.Min(100, ratio.ToDouble()));
        }).ToArray();
        var trades = values.Select((v, i) =>
        {
            var prior = i == 0 ? 0 : values[i - 1]; var before = i < 2 ? 0 : values[i - 2];
            var slope = v - prior; var priorSlope = prior - before;
            return slope > 0 && slope > priorSlope ? Signal.StrongBuy : slope < 0 && slope < priorSlope ? Signal.StrongSell
                : slope > 0 || prior < 30 && v > 30 ? Signal.Buy : slope < 0 || prior > 70 && v < 70 ? Signal.Sell : Signal.None;
        }).ToArray();
        return (new Dictionary<string, double[]> { ["Rsx"] = values }, trades);
    }
}
