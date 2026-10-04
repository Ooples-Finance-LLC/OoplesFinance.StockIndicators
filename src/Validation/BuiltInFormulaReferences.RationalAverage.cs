namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    // Independent reference averaging: exact recurrence, cumulative EMA startup,
    // zero SMA warmup and zero-padded weighted history.
    private static ReferenceFraction[] RationalAverage(ReferenceFraction[] values, int length, int code)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        length = Math.Max(1, length);
        if (code is 3 or 6)
        {
            // Recursive means do not need cumulative sums of normalized ratios.
            // Expand the update as previous + alpha*(input-previous), independently of production's weighted numerator.
            var output = new ReferenceFraction[values.Length]; var seed = R(0); var previous = R(0);
            var alpha = R(code == 3 ? 2 : 1) / R(code == 3 ? length + 1L : length);
            for (var i = 0; i < values.Length; i++)
            {
                if (code == 3 && i < length) { seed += values[i]; previous = seed / R(i + 1L); }
                else previous += alpha * (values[i] - previous);
                output[i] = previous;
            }
            return output;
        }
        if (code is not (1 or 2)) return Average(values.Select(v => v.ToDouble()).ToArray(), length, code).Select(R).ToArray();
        return values.Select((_, i) =>
        {
            if (code == 1 && i + 1 < length) return R(0);
            var sum = R(0);
            for (var j = Math.Max(0, i - length + 1); j <= i; j++) sum += values[j] * R(code == 2 ? length - (long)i + j : 1);
            return sum / (code == 2 ? R(length) * R(length + 1L) / R(2) : R(length));
        }).ToArray();
    }

    private static ReferenceFraction[] RationalAverage(ReferenceFraction[] values, int length, MovingAvgType kind)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var zero = R(0);
        if (kind is not (MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage or MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod))
            return Average(values.Select(v => v.ToDouble()).ToArray(), length, kind == MovingAvgType.DoubleExponentialMovingAverage ? 4 : kind == MovingAvgType.TripleExponentialMovingAverage ? 5 : (int)kind).Select(R).ToArray();
        var result = new ReferenceFraction[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            var sum = zero;
            if (kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage)
            {
                for (var j = Math.Max(0, i - length + 1); j <= i; j++) sum += values[j] * R(kind == MovingAvgType.WeightedMovingAverage ? (long)length - i + j : 1);
                result[i] = kind == MovingAvgType.WeightedMovingAverage ? R(2) * sum / (R(length) * R(length + 1L)) : i + 1 < length ? zero : sum / R(length);
            }
            else if (kind == MovingAvgType.ExponentialMovingAverage && i < length)
            { for (var j = 0; j <= i; j++) sum += values[j]; result[i] = sum / R(i + 1); }
            else
            { var ema = kind == MovingAvgType.ExponentialMovingAverage; result[i] = ((i == 0 ? zero : result[i - 1]) * R(length - 1) + values[i] * R(ema ? 2 : 1)) / R(ema ? length + 1L : length); }
        }
        return result;
    }
}
