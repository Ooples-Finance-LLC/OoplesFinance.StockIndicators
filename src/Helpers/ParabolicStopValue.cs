using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Finite published stages are binary64; wide intermediates and comparisons
// retain the original fixed-grid integer representation when needed.
internal readonly struct ParabolicStopValue
{
    private static readonly BigInteger Grid = BigInteger.One << 1074;
    private readonly double _value;
    private readonly BigInteger _units;
    private readonly bool _wide;
    private ParabolicStopValue(double value) { _value = value == 0 ? 0 : value; _units = default; _wide = false; }
    private ParabolicStopValue(BigInteger units) { _value = 0; _units = units; _wide = true; }
    private BigInteger Units => _wide ? _units : ExactVarianceWindow.Units(_value);
    internal int Sign => _wide ? _units.Sign : Math.Sign(_value);

    internal static ParabolicStopValue Of(double value)
    {
#if NETFRAMEWORK
        return new(ExactVarianceWindow.Units(value));
#else
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
        return new(value);
#endif
    }

    private static ParabolicStopValue Rounded(BigInteger numerator, BigInteger denominator)
    {
        var units = RocBankValue.RoundUnits(numerator, denominator);
#if !NETFRAMEWORK
        double value = ExactMeanAccumulator.UnitRatio(units, 1);
        if (double.IsFinite(value)) return new(value);
#endif
        return new(units);
    }

    internal static ParabolicStopValue Advance(ParabolicStopValue stop, ParabolicStopValue extreme, ParabolicStopValue factor)
    {
#if !NETFRAMEWORK
        if (!stop._wide && !extreme._wide && !factor._wide &&
            Binary64ArithmeticCertificate.TryDifference(extreme._value, stop._value, out double difference))
        {
            // The certified difference is exact, so FMA applies the one and
            // only rounding to stop + factor * (extreme - stop).
            double value = Math.FusedMultiplyAdd(factor._value, difference, stop._value);
            if (double.IsFinite(value)) return new(value);
        }
#endif
        return Rounded(stop.Units * Grid + factor.Units * (extreme.Units - stop.Units), Grid);
    }

    internal static ParabolicStopValue Increase(ParabolicStopValue factor, ParabolicStopValue step, ParabolicStopValue maximum)
    {
#if !NETFRAMEWORK
        if (!factor._wide && !step._wide && !maximum._wide)
        {
            double sum = factor._value + step._value;
            // Rounding is monotone and maximum is exactly representable:
            // round(min(exact sum, maximum)) == min(round(sum), maximum).
            if (double.IsFinite(sum) || double.IsPositiveInfinity(sum))
                return new(Math.Min(sum, maximum._value));
        }
#endif
        return Rounded(BigInteger.Min(factor.Units + step.Units, maximum.Units), 1);
    }

    internal static ParabolicStopValue Offset(ParabolicStopValue stop, ParabolicStopValue offset, bool rising)
    {
#if !NETFRAMEWORK
        if (!stop._wide && !offset._wide)
        {
            double value = Math.FusedMultiplyAdd(stop._value, rising ? -offset._value : offset._value, stop._value);
            if (double.IsFinite(value)) return new(value);
        }
#endif
        return Rounded(stop.Units * (Grid + (rising ? -offset.Units : offset.Units)), Grid);
    }

    public static ParabolicStopValue operator -(ParabolicStopValue value) =>
        value._wide ? new(-value._units) : new(-value._value);

    public static ParabolicStopValue operator -(ParabolicStopValue left, ParabolicStopValue right)
    {
#if !NETFRAMEWORK
        if (!left._wide && !right._wide && Binary64ArithmeticCertificate.TryDifference(left._value, right._value, out double value))
            return new(value);
#endif
        return new(left.Units - right.Units);
    }

    public static ParabolicStopValue operator +(ParabolicStopValue left, ParabolicStopValue right) => left - (-right);

    private int CompareTo(ParabolicStopValue other) => !_wide && !other._wide
        ? _value.CompareTo(other._value) : Units.CompareTo(other.Units);
    public static bool operator <(ParabolicStopValue left, ParabolicStopValue right) => left.CompareTo(right) < 0;
    public static bool operator >(ParabolicStopValue left, ParabolicStopValue right) => left.CompareTo(right) > 0;
    public static bool operator <=(ParabolicStopValue left, ParabolicStopValue right) => left.CompareTo(right) <= 0;
    public static bool operator >=(ParabolicStopValue left, ParabolicStopValue right) => left.CompareTo(right) >= 0;
    internal static ParabolicStopValue Min(ParabolicStopValue left, ParabolicStopValue right) => left <= right ? left : right;
    internal static ParabolicStopValue Max(ParabolicStopValue left, ParabolicStopValue right) => left >= right ? left : right;

    internal double Publish()
    {
        if (!_wide) return _value;
        double value = ExactMeanAccumulator.UnitRatio(_units, 1);
        return FrameworkCompatibility.IsFinite(value) ? value : throw new OverflowException("Parabolic stop is not representable.");
    }
}
