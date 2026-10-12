#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Helpers;

internal static class ExactIntervalLogRatio
{
    // Positive exact operands. Match JrcWindow's stable logarithm without
    // converting ordinary interval sums to large grid-aligned BigIntegers.
    internal static double Calculate(JurikDyadic numerator, JurikDyadic denominator)
    {
        var difference = numerator; difference.Subtract(denominator);
        if (difference.Sign == 0) return 0;
        var twice = numerator; twice.Multiply(2); twice.Subtract(denominator);
        var upper = denominator; upper.Multiply(2); upper.Subtract(numerator);
        if (twice.Sign >= 0 && upper.Sign >= 0)
        {
            double relative = difference.Ratio(denominator);
            double argument = 1 + relative;
            if (argument == 1) return relative;
            // Two binary64 factors require at most 106 significant bits.
            // Keep their product exact without the general accumulator's heap storage.
            var top = JurikDyadic.FromDouble(Math.Log(argument)); top.Multiply(relative);
            return top.Ratio(JurikDyadic.FromDouble(argument - 1));
        }
        double estimate = numerator.Ratio(denominator);
        int exponent = double.IsInfinity(estimate) ? 1024 : estimate == 0 ? -1074 : Math.ILogB(estimate);
        if (exponent >= 0) denominator.ScaleByPowerOfTwo(exponent);
        else numerator.ScaleByPowerOfTwo(-exponent);
        while (Compare(numerator, denominator) < 0) { numerator.ScaleByPowerOfTwo(1); exponent--; }
        while (true)
        {
            var limit = denominator; limit.ScaleByPowerOfTwo(1);
            if (Compare(numerator, limit) < 0) break;
            denominator = limit; exponent++;
        }
        double mantissa = numerator.Ratio(denominator);
        var result = new ExactMeanAccumulator(); result.Add(Math.Log(mantissa)); result.Add(Math.Log(2), exponent);
        return result.Mean(1);
    }

    private static int Compare(JurikDyadic a, JurikDyadic b) { a.Subtract(b); return a.Sign; }
}
#endif
