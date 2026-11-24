using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TsiWordEmaRecurrenceTests
{
    [Fact]
    public void MixedSignsExponentsAndFallbackPeriodsMatchIndependentRationalStages()
    {
        double tiny = Math.ScaleB(1, -1022);
        double[] values = [0, -0d, double.Epsilon, -double.Epsilon, tiny, -tiny,
            1, Math.BitIncrement(1), Math.BitDecrement(2), 2, -1, -2, double.MaxValue, -double.MaxValue];
        foreach (int period in new[] { 1, 2, 3, 13, 20, 55, 2047, 2048, int.MaxValue })
        {
            foreach (double previous in values)
            foreach (double input in values) Check(previous, input, period);
            var random = new Random(1110);
            for (int i = 0; i < 128; i++)
            {
                int exponent = random.Next(-900, 901);
                double previous = Math.ScaleB(1 + random.NextDouble(), exponent);
                double input = Math.ScaleB(1 + random.NextDouble(), exponent + random.Next(-9, 10));
                Check(i % 2 == 0 ? previous : -previous, i % 3 == 0 ? -input : input, period);
            }
        }
    }

    [Fact]
    public void CancellationGridShiftTiesCarryAndUnsupportedScalesKeepExactBoundaries()
    {
        long a = (1L << 52) + 341, b = (1 - 2045 * a) / 1024;
        double previous = Math.ScaleB(a, -52), input = Math.ScaleB(b, -43);
        var recurrence = new TsiWordEmaRecurrence(2046);
        Assert.True(recurrence.TryAdvance(previous, input, out var positive));
        Bits(Expected(previous, input, 2046), positive);
        Assert.True(recurrence.TryAdvance(-previous, -input, out var negative));
        Bits(Expected(-previous, -input, 2046), negative);
        Check(previous, input, 2046); // Requires a 63-bit left grid shift.
        Check(1, Math.BitIncrement(1), 3);
        Check(Math.BitIncrement(1), Math.BitIncrement(Math.BitIncrement(1)), 3);
        Check(Math.BitDecrement(2), 2, 2);
        Check(1, -1, 3);
        double normal = Math.ScaleB(1, -1022);
        Assert.False(new TsiWordEmaRecurrence(2).TryAdvance(Math.BitDecrement(normal), normal, out _));
        Assert.False(new TsiWordEmaRecurrence(2).TryAdvance(normal, -normal, out _));
        Assert.False(new TsiWordEmaRecurrence(2048).TryAdvance(1, 2, out _));
    }

    [Fact]
    public void DefinedNegativeZeroPublicationAndRecoverySurviveReset()
    {
        double[] prices = [0, -double.MaxValue / 2, 0, -double.Epsilon, -double.Epsilon, 0];
        var bars = prices.Select(v => new Bar(default, v, v, v, v, 1)).ToArray();
        var expected = SeededMomentumReference.Tsi(bars, 2, 1, 0);
        var state = (IMultiOutputState)new SeededTrueStrength(2, 1, 0).CreateState();
        var output = new double[4];
        for (int pass = 0; pass < 2; pass++)
        {
            state.Reset();
            for (int i = 0; i < bars.Length; i++)
            {
                state.Update(in bars[i], output);
                for (int slot = 0; slot < 2; slot++)
                {
                    Bits(expected[slot][i] ?? 0, output[slot]);
                    Assert.Equal(expected[slot][i].HasValue ? 1d : 0, output[slot + 2]);
                }
                if (i == 3) { Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(output[0])); Assert.Equal(1d, output[2]); }
                if (i == 4) { Assert.Equal(0L, BitConverter.DoubleToInt64Bits(output[0])); Assert.Equal(1d, output[2]); }
            }
        }
    }

    [Fact]
    public void GeneralWordUpdatesAllocateNoManagedScratch()
    {
        var recurrence = new TsiWordEmaRecurrence(20);
        CompactMacdValue[] previous = [CompactMacdValue.Of(1.4), CompactMacdValue.Of(1), CompactMacdValue.Of(-.5)];
        CompactMacdValue[] input = [CompactMacdValue.Of(-.2), CompactMacdValue.Of(2.4), CompactMacdValue.Of(.75)];
        for (int i = 0; i < 1000; i++) recurrence.Advance(previous[i % 3], input[i % 3]);
        long result = 0, before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 4000; i++)
            result |= BitConverter.DoubleToInt64Bits(recurrence.Advance(previous[i % 3], input[i % 3]).Publish());
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.NotEqual(0L, result);
    }

    private static double Expected(double previous, double input, int period) =>
        ((ReferenceFraction.FromDouble(previous) * new ReferenceFraction(period - 1)
            + ReferenceFraction.FromDouble(input) * new ReferenceFraction(2))
            / new ReferenceFraction((long)period + 1)).RoundExtendedBinary64().ToDouble();

    private static void Check(double previous, double input, int period)
    {
        var recurrence = new TsiWordEmaRecurrence(period);
        double expected = Expected(previous, input, period);
        if (recurrence.TryAdvance(previous, input, out var direct)) Bits(expected, direct);
        Bits(expected, recurrence.Advance(CompactMacdValue.Of(previous), CompactMacdValue.Of(input)).Publish());
    }

    private static void Bits(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
}
