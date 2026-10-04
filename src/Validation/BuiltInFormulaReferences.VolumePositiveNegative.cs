using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> VolumePositiveNegativeOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return VolumePositiveNegativeValues(bars, (MovingAvgType)options.GetType().GetProperty("MaType")!.GetValue(options)!,
            Integer(options, "Length", 30), Integer(options, "SmoothLength", 3), indicator is IIndicator value && value.Source is not null).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) VolumePositiveNegativeValues(IReadOnlyList<Bar> bars,
        MovingAvgType kind = MovingAvgType.ExponentialMovingAverage, int length = 30, int smooth = 3, bool selected = false)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var code = AverageKind(new { MaType = kind }, 3);
        ReferenceFraction[] Mean(ReferenceFraction[] values, int length) => RationalAverage(values, length, code);
        length = Math.Max(1, length); smooth = Math.Max(1, smooth);
        var typical = bars.Select(b => selected ? R(b.Close) : (R(b.High) + R(b.Low) + R(b.Close)) / R(3)).ToArray();
        var ranges = bars.Select((b, i) =>
        {
            var previous = i == 0 ? b.Close : bars[i - 1].Close; var high = b.High; var low = b.Low;
            if (selected)
            {
                var tolerance = 1e-12 * Math.Max(Math.Abs(high), Math.Abs(low));
                if (!(b.Close >= low - tolerance && b.Close <= high + tolerance)) { high = Math.Max(b.Close, previous); low = Math.Min(b.Close, previous); }
            }
            return new[] { R(high) - R(low), (R(high) - R(previous)).Abs(), (R(low) - R(previous)).Abs() }.Max();
        }).ToArray();
        var atr = Mean(ranges, length); var volume = Mean(bars.Select(b => R(b.Volume)).ToArray(), length);
        var votes = bars.Select((b, i) =>
        {
            var movement = typical[i] - (i == 0 ? R(0) : typical[i - 1]); var cutoff = atr[i] / R(10);
            return movement.CompareTo(cutoff) > 0 ? R(b.Volume) : movement.CompareTo(R(0) - cutoff) < 0 ? R(0) - R(b.Volume) : R(0);
        }).ToArray();
        var line = votes.Select((_, i) =>
        {
            var sum = R(0); for (var j = Math.Max(0, i - length + 1); j <= i; j++) sum += votes[j];
            return R(100) * sum / R(length) / (volume[i].Sign > 0 ? volume[i] : R(1));
        }).ToArray();
        var signal = Mean(line, smooth); var previous = R(0); var trades = new Signal[line.Length];
        for (var i = 0; i < line.Length; i++)
        {
            var value = signal[i]; trades[i] = value.Sign > 0 && value.CompareTo(previous) > 0 ? Signal.StrongBuy
                : value.Sign < 0 && value.CompareTo(previous) < 0 ? Signal.StrongSell : value.Sign > 0 ? Signal.Buy : value.Sign < 0 ? Signal.Sell : Signal.None; previous = value;
        }
        return (new() { ["Vpni"] = line.Select(v => v.ToDouble()).ToArray(), ["Signal"] = signal.Select(v => v.ToDouble()).ToArray() }, trades);
    }
}
