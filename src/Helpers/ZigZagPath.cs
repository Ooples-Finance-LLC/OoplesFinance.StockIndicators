using OoplesFinance.StockIndicators.Streaming;
using Number = OoplesFinance.StockIndicators.Helpers.PeriodicChannelWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;

/// <summary>Repainting piecewise-linear path through alternating price extrema.</summary>
internal static class ZigZagPath
{
    internal static void ValidateDeviation(double deviation)
    {
        StreamingInputValidation.Finite(deviation, nameof(deviation));
        if (deviation < 0) throw new ArgumentOutOfRangeException(nameof(deviation));
    }
    internal static void Validate(StockData data, double deviation)
    {
        ValidateDeviation(deviation);
        foreach (var series in new[] { data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices, data.Volumes, data.ChainedValues })
            foreach (var value in series) StreamingInputValidation.Finite(value, nameof(data));
    }
    private static Signal Trade(Number slope, Number prior)
        => slope.Sign > 0 && (slope - prior).Sign > 0 ? Signal.StrongBuy : slope.Sign < 0 && (slope - prior).Sign < 0 ? Signal.StrongSell
            : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
    internal static void Compute(ReadOnlySpan<double> highs, ReadOnlySpan<double> lows, Span<double> zigZag, double deviation, Span<Signal> signals = default)
    {
        ValidateDeviation(deviation);
        if (lows.Length != highs.Length) throw new ArgumentException("High and low spans must have equal lengths.", nameof(lows));
        if (zigZag.Length < highs.Length) throw new ArgumentException("Output span must be at least input length.", nameof(zigZag));
        if (!signals.IsEmpty && signals.Length < highs.Length) throw new ArgumentException("Signal span must be at least input length.", nameof(signals));
        for (var i = 0; i < highs.Length; i++) { StreamingInputValidation.Finite(highs[i], nameof(highs)); StreamingInputValidation.Finite(lows[i], nameof(lows)); }
        var count = highs.Length; if (count == 0) return;
        var fraction = Number.Of(deviation).Divide(100);
        var knots = new List<(int Index, Number Value)> { (0, (Number.Of(highs[0]) + Number.Of(lows[0])).Divide(2)) };
        var highLeg = true;
        for (var i = 1; i < count; i++)
        {
            var pivot = knots[knots.Count - 1].Value; var magnitude = pivot.Sign < 0 ? pivot.Times(-1) : pivot;
            var threshold = magnitude * fraction; var high = Number.Of(highs[i]); var low = Number.Of(lows[i]);
            var reversal = highLeg ? (low - pivot + threshold).Sign < 0 : (high - pivot - threshold).Sign > 0;
            if (reversal)
            {
                knots.Add((i, highLeg ? low : high)); highLeg = !highLeg;
            }
            else if (highLeg ? (high - pivot).Sign > 0 : (low - pivot).Sign < 0)
            {
                var extreme = (i, highLeg ? high : low);
                if (knots.Count == 1) knots.Add(extreme); else knots[knots.Count - 1] = extreme;
            }
        }
        // Resolve all extrema before publishing. This also permits overlapping spans.
        Number previous = default, priorSlope = default;
        for (var segment = 1; segment < knots.Count; segment++)
        {
            var left = knots[segment - 1]; var right = knots[segment];
            for (var j = segment == 1 ? left.Index : left.Index + 1; j <= right.Index; j++)
            {
                var point = left.Value + (right.Value - left.Value).Times(j - left.Index).Divide(right.Index - left.Index);
                zigZag[j] = point.Publish();
                if (!signals.IsEmpty) { var slope = point - previous; signals[j] = Trade(slope, priorSlope); previous = point; priorSlope = slope; }
            }
        }
        var final = knots[knots.Count - 1];
        for (var j = knots.Count == 1 ? final.Index : final.Index + 1; j < count; j++)
        {
            zigZag[j] = final.Value.Publish();
            if (!signals.IsEmpty) { var slope = final.Value - previous; signals[j] = Trade(slope, priorSlope); previous = final.Value; priorSlope = slope; }
        }
    }
}
