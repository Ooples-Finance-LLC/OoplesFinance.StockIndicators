using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RsingOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var o = indicator.CreateOptions(); return RsingValues(bars, Integer(o, "Length", 20), (MovingAvgType)o.GetType().GetProperty("MaType")!.GetValue(o)!).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) RsingValues(IReadOnlyList<Bar> bars, int length, MovingAvgType kind)
    {
        length = Math.Max(1, length); ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v); var zero = R(0);
        ReferenceFraction[] Mean(ReferenceFraction[] values) => RationalAverage(values, length, kind, (int)kind);
        var volumes = bars.Select(b => R(b.Volume)).ToArray(); var means = Mean(volumes);
        var ranges = bars.Select(b => R(b.High) - R(b.Low)).ToArray(); var raw = new ReferenceFraction[bars.Count];
        var scale = new ReferenceFraction(BigInteger.One << 512);
        for (var i = 0; i < bars.Count; i++)
        {
            raw[i] = zero; if (i < length || means[i].Sign == 0) continue;
            var first = i - length + 1; var mean = zero;
            for (var j = first; j <= i; j++) mean += ranges[j]; mean /= R(length);
            var variance = zero;
            for (var j = first; j <= i; j++) { var residual = ranges[j] - mean; variance += residual * residual; }
            variance /= R(length); if (variance.Sign == 0) continue;
            var numerator = (R(bars[i].Close) - R(bars[i - length].Close)) * volumes[i] * ranges[i] / means[i];
            var square = numerator * numerator / variance; var root = square.SqrtToDouble(); var restore = R(1);
            // Independent binary64 search for sqrt, normalized only when its
            // exponent exceeds the public oscillator's representable range.
            while (double.IsInfinity(root)) { square /= scale * scale; restore *= scale; root = square.SqrtToDouble(); }
            raw[i] = R(numerator.Sign * root) * restore;
        }
        var signal = Mean(raw); var trades = new Signal[bars.Count]; var previousChange = zero;
        for (var i = 0; i < bars.Count; i++)
        {
            var change = signal[i] - (i == 0 ? zero : signal[i - 1]); var acceleration = change.CompareTo(previousChange);
            trades[i] = change.Sign > 0 && acceleration > 0 ? Signal.StrongBuy : change.Sign < 0 && acceleration < 0 ? Signal.StrongSell : change.Sign > 0 ? Signal.Buy : change.Sign < 0 ? Signal.Sell : Signal.None;
            previousChange = change;
        }
        return (new() { ["Rsing"] = raw.Select(v => v.ToDouble()).ToArray(), ["Signal"] = signal.Select(v => v.ToDouble()).ToArray() }, trades);
    }
}
