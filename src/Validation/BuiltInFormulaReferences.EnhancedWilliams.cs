using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> EnhancedWilliamsOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
        => EnhancedWilliamsValues(bars, Integer(indicator.CreateOptions(), "Length", 14), Integer(indicator.CreateOptions(), "SignalLength", 5), AverageKind(indicator.CreateOptions(), 1)).Outputs;
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals, bool[] Aligned) EnhancedWilliamsValues(IReadOnlyList<Bar> bars, int length, int signalLength, int kind, double[]? externalPriceMean = null, double[]? externalVolumeMean = null)
    {
        var rangeLength = Math.Max(2, length); var meanLength = (int)Math.Max(2L, Math.Min(530L, ((long)Math.Max(1, length) + 1) / 2)); signalLength = Math.Max(1, signalLength);
        ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        ReferenceFraction[] Mean(ReferenceFraction[] values, int period) => kind is 1 or 2 or 3 or 6 ? SmoothRocBankStage(values, period, kind)
            : Average(values.Select(v => v.ToDouble()).ToArray(), period, kind).Select(R).ToArray();
        var priceMean = externalPriceMean is null ? Mean(bars.Select(b => R(b.Close)).ToArray(), meanLength) : externalPriceMean.Select(R).ToArray();
        var volumeMean = externalVolumeMean is null ? Mean(bars.Select(b => R(b.Volume)).ToArray(), meanLength) : externalVolumeMean.Select(R).ToArray();
        var acceleration = R(length < 10 ? .25 : length / 32d - .0625); var line = new ReferenceFraction[bars.Count]; var aligned = new bool[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var start = (int)Math.Max(0L, i - (long)rangeLength + 1); var observations = bars.Skip(start).Take(i-start+1).ToArray();
            var priceRange = R(observations.Max(b => b.Close)) - R(observations.Min(b => b.Close));
            var volumeRange = R(observations.Max(b => b.Volume)) - R(observations.Min(b => b.Volume));
            var price = R(bars[i].Close); var previous = i == 0 ? R(0) : R(bars[i-1].Close);
            var p = priceRange.Sign == 0 ? R(0) : R(2) * (price - priceMean[i]) / priceRange;
            var v = volumeRange.Sign == 0 ? R(0) : R(2) * (R(bars[i].Volume) - volumeMean[i]) / volumeRange;
            var step = i == 0 || priceRange.Sign == 0 ? R(0) : R(2) * (price - previous) / priceRange;
            var factor = step + acceleration;
            aligned[i] = v.Sign > 0 && (p * (price - previous)).Sign > 0 && factor.Sign != 0;
            // Evaluate the unsimplified expression over fractions independently
            // of production's cancelled integer polynomial.
            line[i] = (aligned[i] ? (R(50) * p * factor * v + factor) / factor : R(25) * (p * (v + R(1)) + R(2))).RoundExtendedBinary64();
        }
        var signal = Mean(line, signalLength); var trades = new Signal[bars.Count]; var oldSpread = R(0); var oldLine = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var spread = line[i] - signal[i];
            trades[i] = spread.Sign > 0 && spread.CompareTo(oldSpread) > 0 ? Signal.StrongBuy : spread.Sign < 0 && spread.CompareTo(oldSpread) < 0 ? Signal.StrongSell
                : spread.Sign > 0 || oldLine.CompareTo(R(-100)) < 0 && line[i].CompareTo(R(-100)) > 0 ? Signal.Buy
                : spread.Sign < 0 || oldLine.CompareTo(R(100)) > 0 && line[i].CompareTo(R(100)) < 0 ? Signal.Sell : Signal.None;
            oldSpread = spread; oldLine = line[i];
        }
        return (new Dictionary<string, double[]> { ["Ewr"] = line.Select(v => v.ToDouble()).ToArray(), ["Signal"] = signal.Select(v => v.ToDouble()).ToArray() }, trades, aligned);
    }
}
