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
}
