namespace OoplesFinance.StockIndicators.Helpers;

internal static class WilliamsFractalPattern
{
    internal static void Calculate(ReadOnlySpan<double> prices, Span<double> output, int delay, bool upper)
    {
        if (output.Length < prices.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        delay = Math.Max(2, delay);
        for (var i = 0; i < prices.Length; i++)
        {
            var center = i - delay;
            var older = Math.Min(6, center);
            output[i] = older < 2 ? 0 : IsFractal(prices.Slice(center - older, older + 3), upper) ? 1 : 0;
        }
    }

    // Real observations in chronological order: up to six older bars, center, two confirmations.
    internal static bool IsFractal(ReadOnlySpan<double> neighborhood, bool upper)
    {
        var older = neighborhood.Length - 3;
        if (older < 2) return false;
        var sign = upper ? 1d : -1d;
        var center = sign * neighborhood[older];
        if (sign * neighborhood[older + 1] >= center || sign * neighborhood[older + 2] >= center) return false;
        var first = sign * neighborhood[older - 1];
        var second = sign * neighborhood[older - 2];
        if (first < center && second < center) return true;
        if (older < 3) return false;
        var third = sign * neighborhood[older - 3];
        if (first == center && second < center && third < center) return true; // NOSONAR: S1244 - Plateau membership requires exact price ties.
        if (older < 4) return false;
        var fourth = sign * neighborhood[older - 4];
        if (first <= center && second == center && third < center && fourth < center) return true; // NOSONAR: S1244 - Plateau membership requires exact price ties.
        if (older < 5) return false;
        var fifth = sign * neighborhood[older - 5];
        if (first <= center && second == center && third == center && fourth < center && fifth < center) return true; // NOSONAR: S1244 - Plateau membership requires exact price ties.
        if (older < 6) return false;
        var sixth = sign * neighborhood[older - 6];
        return first <= center && second == center && third <= center && fourth == center && fifth < center && sixth < center; // NOSONAR: S1244 - Plateau membership requires exact price ties.
    }
}
