using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit;

/// <summary>
/// Explicit crossings between the oracles' <see cref="BigInteger"/> (System.Numerics) and the library's
/// <see cref="ExactInteger"/>. Expected values are computed on System.Numerics.BigInteger only; values enter or leave
/// the code under test through these, so a defect in ExactInteger cannot change an oracle and its subject together.
/// </summary>
internal static class ExactConversions
{
    internal static BigInteger Big(this ExactInteger value) => value;

    internal static BigInteger? Big(this ExactInteger? value) => value is { } v ? v : null;

    internal static ExactInteger[] Exact(this BigInteger[] values) => Array.ConvertAll(values, v => (ExactInteger)v);

    internal static (ExactInteger Low, ExactInteger High)[] Exact(this (BigInteger Low, BigInteger High)[] values)
        => Array.ConvertAll(values, v => ((ExactInteger)v.Low, (ExactInteger)v.High));

    /// <summary>The value x 2^1074 as the oracles compute it, independent of the library's fixed-point conversion.</summary>
    internal static BigInteger Units(double value) => Validation.ReferenceFraction.Units(value);

    /// <summary>
    /// The oracle's rounding of numerator / denominator to units: the nearest double to the ratio x 2^-1074, rescaled by
    /// 2^32 steps until finite, computed on <see cref="Validation.ReferenceFraction"/> rather than the library's helper.
    /// </summary>
    internal static BigInteger RoundUnits(BigInteger numerator, BigInteger denominator)
    {
        for (var shift = 0; ; shift += 32)
        {
            var value = (new Validation.ReferenceFraction(numerator) / new Validation.ReferenceFraction((denominator << shift) << 1074)).ToDouble();
            if (!double.IsInfinity(value)) return Units(value) << shift;
        }
    }
}
