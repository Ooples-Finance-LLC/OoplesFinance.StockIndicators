namespace OoplesFinance.StockIndicators.Indicators.Fast;

internal static partial class FastKernels
{
    /// <summary>
    /// Wilder directional movement, indicator, index, average index or rating, seeded by the sum of the bars
    /// before the period completes. Each stage keeps the running-sum form previous - previous / period +
    /// current, so the movement outputs are sums rather than averages.
    /// </summary>
    internal static void Directional(FastColumns columns, Span<double> value, Span<double> defined,
        PriorSeededDirectionalMeasure owner)
    {
        var measure = owner.Measure;
        var period = owner.Period;
        long unstable = owner.UnstablePeriod;
        var needsPositive = measure is not (PriorDirectionalMeasure.NegativeMovement or PriorDirectionalMeasure.NegativeIndicator);
        var needsNegative = measure is not (PriorDirectionalMeasure.PositiveMovement or PriorDirectionalMeasure.PositiveIndicator);
        var needsRange = measure >= PriorDirectionalMeasure.PositiveIndicator;
        var movementOnly = measure <= PriorDirectionalMeasure.NegativeMovement;
        var positiveSide = measure is PriorDirectionalMeasure.PositiveMovement or PriorDirectionalMeasure.PositiveIndicator;
        if (needsRange) columns.RequireHighLowClose();
        ReadOnlySpan<double> high = columns.High, low = columns.Low;
        // The movement measures never read the close, so it is neither gathered nor validated for them.
        var close = needsRange ? columns.Close : default(ReadOnlySpan<double>);
        var n = high.Length;
        value = value.Slice(0, n);
        defined = defined.Slice(0, n);
        value.Clear();
        defined.Clear();
        var weight = (period - 1.0) / period;
        var firstDx = period + unstable;
        var seedIndex = 2L * period - 1;
        double positive = 0, negative = 0, range = 0, positiveSeed = 0, negativeSeed = 0, rangeSeed = 0;
        double seedDx = 0, average = 0, lastDx = 0;
        // The rating pairs an average with the one period - 1 bars earlier; NaN marks one not yet available.
        var history = measure == PriorDirectionalMeasure.Rating ? new double[period - 1] : null;
        for (var i = 1; i < n; i++)
        {
            var up = high[i] - high[i - 1];
            var down = low[i - 1] - low[i];
            var plus = needsPositive && up > 0 && up > down ? up : 0;
            var minus = needsNegative && down > 0 && down > up ? down : 0;
            var trueRange = needsRange ? TrueRange(high[i], low[i], close[i - 1]) : 0;
            if (period == 1)
            {
                var movement = positiveSide ? plus : minus;
                value[i] = movementOnly ? movement : Quotient(movement, trueRange);
                defined[i] = 1;
                continue;
            }
            if (i < period)
            {
                positiveSeed += plus;
                negativeSeed += minus;
                rangeSeed += trueRange;
                if (i == period - 1)
                {
                    positive = positiveSeed;
                    negative = negativeSeed;
                    range = rangeSeed;
                }
            }
            else
            {
                positive = FastMath.Blend(weight, positive, plus);
                negative = FastMath.Blend(weight, negative, minus);
                range = FastMath.Blend(weight, range, trueRange);
            }
            if (movementOnly)
            {
                if (i >= period - 1 + unstable)
                {
                    value[i] = positiveSide ? positive : negative;
                    defined[i] = 1;
                }
                continue;
            }
            if (i < period) continue;
            // The index folds the bar's own range in a second time once it is past its first value. The range
            // cancels out of the index, so this only keeps the stage identical to the exact state.
            if (measure == PriorDirectionalMeasure.Index && i > firstDx)
                range = FastMath.Blend(weight, range, TrueRange(high[i], low[i], close[i]));
            if (measure <= PriorDirectionalMeasure.NegativeIndicator)
            {
                if (i >= firstDx)
                {
                    value[i] = Quotient(100 * (positiveSide ? positive : negative), range);
                    defined[i] = 1;
                }
                continue;
            }
            var plusIndicator = Quotient(100 * positive, range);
            var minusIndicator = Quotient(100 * negative, range);
            var total = plusIndicator + minusIndicator;
            var hasDirection = total != 0;
            var index = hasDirection ? 100 * Math.Abs(plusIndicator - minusIndicator) / total : 0;
            if (measure == PriorDirectionalMeasure.Index)
            {
                if (i < firstDx) continue;
                if (i == firstDx || hasDirection) lastDx = index;
                value[i] = lastDx;
                defined[i] = 1;
                continue;
            }
            if (i <= seedIndex)
            {
                seedDx += index;
                if (i == seedIndex) average = seedDx / period;
            }
            else if (hasDirection)
                average = (average * (period - 1) + index) / period;
            var available = i >= seedIndex + unstable;
            if (history is null)
            {
                if (available)
                {
                    value[i] = average;
                    defined[i] = 1;
                }
                continue;
            }
            var slot = (i - period) % history.Length;
            var prior = i - period >= history.Length ? history[slot] : double.NaN;
            history[slot] = available ? average : double.NaN;
            if (available && !double.IsNaN(prior))
            {
                value[i] = (average + prior) / 2;
                defined[i] = 1;
            }
        }
    }

    // A zero numerator or denominator gives zero, as the exact ratio does.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static double Quotient(double numerator, double denominator) =>
        numerator == 0 || denominator == 0 ? 0 : numerator / denominator;
}
