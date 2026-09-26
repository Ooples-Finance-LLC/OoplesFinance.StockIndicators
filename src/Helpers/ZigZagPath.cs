namespace OoplesFinance.StockIndicators.Helpers;

/// <summary>Repainting piecewise-linear path through alternating price extrema.</summary>
internal static class ZigZagPath
{
    internal static void Compute(ReadOnlySpan<double> highs, ReadOnlySpan<double> lows, Span<double> zigZag, double deviation)
    {
        var count = highs.Length;
        if (double.IsNaN(deviation) || double.IsInfinity(deviation) || deviation < 0)
            throw new ArgumentOutOfRangeException(nameof(deviation));
        if (count > 0)
        {
            var knots = new List<(int Index, double Value)> { (0, (highs[0]+lows[0])/2) };
            var highLeg = true;
            for (var i = 1; i < count; i++)
            {
                var pivot = knots[knots.Count-1].Value;
                var threshold = Math.Abs(pivot)*deviation/100;
                var reversal = highLeg ? lows[i] < pivot-threshold : highs[i] > pivot+threshold;
                if (reversal)
                {
                    knots.Add((i, highLeg ? lows[i] : highs[i]));
                    highLeg = !highLeg;
                }
                else if (highLeg ? highs[i] > pivot : lows[i] < pivot)
                {
                    var extreme = (i, highLeg ? highs[i] : lows[i]);
                    if (knots.Count == 1) knots.Add(extreme);
                    else knots[knots.Count-1] = extreme;
                }
            }
            // Interpolate only after the final extrema are known. Extending a provisional
            // endpoint must redraw its whole segment, including a still-unconfirmed tail.
            for (var segment = 1; segment < knots.Count; segment++)
            {
                var left = knots[segment-1]; var right = knots[segment];
                for (var j = left.Index; j <= right.Index; j++)
                    zigZag[j] = left.Value+(right.Value-left.Value)*(j-left.Index)/(right.Index-left.Index);
            }
            var final = knots[knots.Count-1];
            for (var j = final.Index; j < count; j++) zigZag[j] = final.Value;
        }

    }
}
