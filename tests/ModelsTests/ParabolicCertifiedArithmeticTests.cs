using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class ParabolicCertifiedArithmeticTests
{
    private static readonly BigInteger Grid = BigInteger.One << 1074;
    private static BigInteger U(double value) => ExactVarianceWindow.Units(value);
    private static double Publish(BigInteger units)
    {
        double value = ExactMeanAccumulator.UnitRatio(units, 1);
        return double.IsFinite(value) ? value : throw new OverflowException("Parabolic stop is not representable.");
    }
    private static void Equal(BigInteger expected, ParabolicStopValue actual)
    {
        double a = 0, b = 0;
        var first = Record.Exception(() => a = Publish(expected));
        var second = Record.Exception(() => b = actual.Publish());
        Assert.Equal(first?.GetType(), second?.GetType());
        Assert.Equal(first?.Message, second?.Message);
        if (first is null) Assert.Equal(BitConverter.DoubleToInt64Bits(a), BitConverter.DoubleToInt64Bits(b));
    }

    [Fact]
    public void ExactUpdatesOffsetsAndClampsRetainWideOracleResults()
    {
        double[] values = [0, -0d, 1, -1, .02, -.02, Math.BitIncrement(1), double.Epsilon,
            -double.Epsilon, Math.ScaleB(1, -1022), Math.ScaleB(1, -500), Math.ScaleB(1, 500), double.MaxValue, -double.MaxValue];
        foreach (double stop in values)
        foreach (double extreme in values)
        foreach (double factor in new[] { 0, .02, -.02, .5, 1, double.Epsilon, double.MaxValue })
        {
            var a = ParabolicStopValue.Of(stop); var b = ParabolicStopValue.Of(extreme); var f = ParabolicStopValue.Of(factor);
            Equal(RocBankValue.RoundUnits(U(stop) * Grid + U(factor) * (U(extreme) - U(stop)), Grid),
                ParabolicStopValue.Advance(a, b, f));
            Equal(RocBankValue.RoundUnits(BigInteger.Min(U(stop) + U(extreme), U(factor)), 1),
                ParabolicStopValue.Increase(a, b, f));
            foreach (bool rising in new[] { false, true })
                Equal(RocBankValue.RoundUnits(U(stop) * (Grid + (rising ? -U(factor) : U(factor))), Grid),
                    ParabolicStopValue.Offset(a, f, rising));
        }
    }

    [Fact]
    public void FiniteRandomBitPatternsRetainExactUpdateRounding()
    {
        var random = new Random(7232030);
        double Next()
        {
            double value = BitConverter.Int64BitsToDouble(random.NextInt64(long.MinValue, long.MaxValue));
            return double.IsFinite(value) ? value : 0;
        }
        for (int i = 0; i < 1000; i++)
        {
            double stop = Next(), extreme = Next(), factor = Next();
            Equal(RocBankValue.RoundUnits(U(stop) * Grid + U(factor) * (U(extreme) - U(stop)), Grid),
                ParabolicStopValue.Advance(ParabolicStopValue.Of(stop), ParabolicStopValue.Of(extreme), ParabolicStopValue.Of(factor)));
        }
    }

    [Fact]
    public void UnpublishedOverflowCanReturnToFiniteState()
    {
        var maximum = ParabolicStopValue.Of(double.MaxValue);
        var wide = ParabolicStopValue.Offset(maximum, ParabolicStopValue.Of(1), false);
        Assert.Throws<OverflowException>(() => wide.Publish());
        Equal(U(double.MaxValue), ParabolicStopValue.Min(wide, maximum));
        Equal(BigInteger.Zero, ParabolicStopValue.Advance(wide, -wide, ParabolicStopValue.Of(.5)));
        var tiny = ParabolicStopValue.Of(double.Epsilon);
        Assert.True(maximum + tiny > maximum);
        Assert.True(-maximum - tiny < -maximum);
    }

    [Fact]
    public void OrdinaryRecurrenceAllocatesNoManagedMemory()
    {
        var stop = ParabolicStopValue.Of(100); var extreme = ParabolicStopValue.Of(110);
        var factor = ParabolicStopValue.Of(.02); var step = ParabolicStopValue.Of(.01); var maximum = ParabolicStopValue.Of(.2);
        for (int i = 0; i < 64; i++) stop = ParabolicStopValue.Advance(stop, extreme, factor);
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            factor = ParabolicStopValue.Increase(factor, step, maximum);
            stop = ParabolicStopValue.Advance(stop, extreme, factor);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - start;
        Assert.InRange(stop.Publish(), 100d, 110d);
        Assert.Equal(0, allocated);
    }
}
