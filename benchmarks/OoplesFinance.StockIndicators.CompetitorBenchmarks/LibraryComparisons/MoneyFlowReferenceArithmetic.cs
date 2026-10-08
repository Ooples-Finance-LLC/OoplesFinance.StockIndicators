using System.Numerics;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Independent integer-grid arithmetic for the comparison oracle. No production
// accumulators or production validation references are called here.
internal static class MoneyFlowReferenceArithmetic
{
    internal static readonly BigInteger Grid = BigInteger.One << 1074;

    internal static BigInteger Units(double value) => PenetrationReferenceArithmetic.Units(value);

    internal static double Round(BigInteger numerator, BigInteger denominator)
    {
        if (denominator.IsZero)
            throw new DivideByZeroException();
        if (denominator.Sign < 0)
        {
            numerator = -numerator;
            denominator = -denominator;
        }
        var negative = numerator.Sign < 0;
        numerator = BigInteger.Abs(numerator);
        if (numerator.IsZero)
            return 0;
        var exponent = (int)(numerator.GetBitLength() - denominator.GetBitLength());
        if (
            exponent >= 0
                ? numerator < (denominator << exponent)
                : (numerator << -exponent) < denominator
        )
            exponent--;
        var quantum = Math.Max(-1074, exponent - 52);
        if (quantum < 0)
            numerator <<= -quantum;
        else
            denominator <<= quantum;
        var whole = BigInteger.DivRem(numerator, denominator, out var remainder);
        var half = (remainder * 2).CompareTo(denominator);
        if (half > 0 || half == 0 && !whole.IsEven)
            whole++;
        var value = Math.ScaleB((double)whole, quantum);
        return negative ? -value : value;
    }

    internal static double Add(double a, double b) => Round(Units(a) + Units(b), Grid);

    internal static double Subtract(double a, double b) => Round(Units(a) - Units(b), Grid);

    internal static double Multiply(double a, double b) => Round(Units(a) * Units(b), Grid * Grid);

    internal static double Divide(double a, double b) => Round(Units(a), Units(b));
}
