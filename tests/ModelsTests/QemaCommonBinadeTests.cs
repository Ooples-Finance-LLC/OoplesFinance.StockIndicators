using System.Reflection;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class QemaCommonBinadeTests
{
    private delegate bool Combination(double a, double b, double c, double d, out double value);
    private static readonly Combination Combine = typeof(MassNormalizedQuadrupleEma)
        .GetNestedType("State", BindingFlags.NonPublic)!
        .GetMethod("TryCombineCommonBinade", BindingFlags.NonPublic | BindingFlags.Static)!
        .CreateDelegate<Combination>();

    [Fact]
    public void SupportedRandomTuplesMatchIndependentFractions()
    {
        var random = new Random(261113);
        for (int i = 0; i < 4000; i++)
        {
            int exponent = i % 8 == 0 ? 2046 : random.Next(53, 2047);
            ulong top = (ulong)exponent << 52;
            if ((i & 1) != 0) top |= 1UL << 63;
            double Next() => BitConverter.Int64BitsToDouble(unchecked((long)(top | ((ulong)random.NextInt64() & ((1UL << 52) - 1)))));
            Check(Next(), Next(), Next(), Next());
        }
    }

    [Fact]
    public void CancellationTiesNormalBoundaryAndOverflowMatchIndependentFractions()
    {
        const long unit = 1L << 52;
        long[][] tuples =
        [
            [unit, unit + unit / 8, unit, unit + unit / 4],
            [unit, unit + unit / 8, unit, unit + unit / 4 - 1],
            [unit, unit + unit / 8, unit, unit + unit / 4 + 1],
            [unit + unit / 4, unit, unit + 1, unit + 3],
            [unit + unit / 4, unit, unit + 1, unit + 1],
            [2 * unit - 1, 2 * unit - 1, 2 * unit - 1, 2 * unit - 1]
        ];
        foreach (int exponent in new[] { 53, 1029, 2046 })
        foreach (bool negative in new[] { false, true })
        foreach (var tuple in tuples)
        {
            double Read(long mantissa) => BitConverter.Int64BitsToDouble(unchecked((long)(
                (negative ? 1UL << 63 : 0) | ((ulong)exponent << 52) | (ulong)(mantissa - unit))));
            Check(Read(tuple[0]), Read(tuple[1]), Read(tuple[2]), Read(tuple[3]));
        }
    }

    [Fact]
    public void UnsupportedSignsExponentsAndNonfiniteValuesRetainFallback()
    {
        double tiny = BitConverter.Int64BitsToDouble(52L << 52);
        foreach (var tuple in new double[][]
        {
            [0, 0, 0, 0], [-0d, -0d, -0d, -0d], [tiny, tiny, tiny, tiny],
            [double.Epsilon, double.Epsilon, double.Epsilon, double.Epsilon],
            [1, -1, 1, 1], [1, 1, 2, 1], [1, 1, Math.BitDecrement(1d), 1],
            [double.NaN, 1, 1, 1], [1, double.PositiveInfinity, 1, 1],
            [double.NegativeInfinity, -1, -1, -1]
        }) Assert.False(Combine(tuple[0], tuple[1], tuple[2], tuple[3], out _));
    }

    private static void Check(double a, double b, double c, double d)
    {
        bool accepted = Combine(a, b, c, d, out var actual);
        var exact = ReferenceFraction.FromDouble(a) * new ReferenceFraction(4)
            - ReferenceFraction.FromDouble(b) * new ReferenceFraction(6)
            + ReferenceFraction.FromDouble(c) * new ReferenceFraction(4)
            - ReferenceFraction.FromDouble(d);
        double expected;
        try { expected = exact.ToDouble(); }
        catch (OverflowException) { Assert.False(accepted); return; }
        if (!double.IsFinite(expected)) { Assert.False(accepted); return; }
        Assert.True(accepted);
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
    }
}
