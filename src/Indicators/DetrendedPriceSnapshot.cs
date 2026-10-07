using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Retrospective DPO and the future-ending simple average used at this row.</summary>
public sealed record DetrendedPriceValue(double? Dpo, double? Sma);

/// <summary>Batch detrended prices aligned to the earlier price, including an unconfirmed trailing region.</summary>
/// <remarks>At index i, subtract the period SMA ending at i + floor(period/2) + 1
/// from close[i]. Appending data can fill previously absent trailing values.
/// This is a retrospective snapshot, not a causal streaming oscillator. Each average
/// and difference rounds once; genuine final overflow is rejected.</remarks>
public static class DetrendedPriceSnapshot
{
    /// <summary>Calculates DPO in supplied order without changing or sorting the input.</summary>
    public static IReadOnlyList<DetrendedPriceValue> Calculate(
        IReadOnlyList<Bar> bars,
        int period = 20
    )
    {
        if (bars is null) throw new ArgumentNullException(nameof(bars));
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        foreach (var bar in bars)
            if (!FrameworkCompatibility.IsFinite(bar.Close))
                throw new ArgumentOutOfRangeException(nameof(bars));
        var result = Enumerable
            .Range(0, bars.Count)
            .Select(_ => new DetrendedPriceValue(null, null))
            .ToArray();
        var offset = period / 2 + 1;
        var sum = new ExactMeanAccumulator();
        for (var end = 0; end < bars.Count; end++)
        {
            sum.Add(bars[end].Close);
            if (end >= period)
                sum.Add(bars[end - period].Close, -1);
            if (end < period - 1 || end < offset)
                continue;
            var mean = sum.Mean(period);
            var difference = new ExactMeanAccumulator();
            difference.Add(bars[end - offset].Close);
            difference.Add(mean, -1);
            var value = difference.Mean(1);
            if (!FrameworkCompatibility.IsFinite(value))
                throw new OverflowException("Detrended price is not representable.");
            result[end - offset] = new(value, mean);
        }
        return result;
    }
}
