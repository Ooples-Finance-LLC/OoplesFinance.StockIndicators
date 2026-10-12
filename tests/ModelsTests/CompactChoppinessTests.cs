using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactChoppinessTests
{
    [Fact]
    public void LogRatioMatchesExistingExactGridAlgorithm()
    {
        double[] values = [double.Epsilon, 3 * double.Epsilon, Math.ScaleB(1, -1022),
            .5, 1, Math.BitIncrement(1d), Math.BitDecrement(1d), 2, 100, double.MaxValue];
        foreach (double a in values)
        foreach (double b in values)
        {
            double expected = JrcWindow.LogRatio(ExactVarianceWindow.Units(a), ExactVarianceWindow.Units(b));
            double actual = ExactIntervalLogRatio.Calculate(JurikDyadic.FromDouble(a), JurikDyadic.FromDouble(b));
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
        }
    }

    [Fact]
    public void NearOneCorrectionMatchesExactGridWithoutAllocations()
    {
        var random = new Random(219);
        var inputs = Enumerable.Range(0, 1000).Select(_ =>
        {
            double denominator = 1 + random.NextDouble();
            double numerator = denominator * (.5 + random.NextDouble());
            return (numerator, denominator);
        }).ToArray();
        var expected = inputs.Select(p => JrcWindow.LogRatio(
            ExactVarianceWindow.Units(p.numerator), ExactVarianceWindow.Units(p.denominator))).ToArray();
        var actual = new double[inputs.Length];
        for (int replay = 0; replay < 2; replay++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < inputs.Length; i++)
                actual[i] = ExactIntervalLogRatio.Calculate(JurikDyadic.FromDouble(inputs[i].numerator),
                    JurikDyadic.FromDouble(inputs[i].denominator));
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            if (replay > 0) Assert.Equal(0, allocated);
            for (int i = 0; i < inputs.Length; i++)
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i]), BitConverter.DoubleToInt64Bits(actual[i]));
        }
    }

    [Theory]
    [InlineData(2)] [InlineData(14)] [InlineData(int.MaxValue)]
    public void CompactWindowsPreserveExtremesPresenceAndReset(int period)
    {
        double[] values = [0, double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue,
            1, Math.BitIncrement(1d), -1];
        var bars = Enumerable.Range(0, 50).Select(i => new Bar(default, 0,
            values[i % values.Length], values[(i + 3) % values.Length], values[(i + 5) % values.Length], 0))
            .Concat(Enumerable.Repeat(new Bar(default, 1, 1, 1, 1, 0), 30)).ToArray();
        var expected = WindowChoppinessReference.Calculate(bars, period);
        var state = (IMultiOutputState)new WindowChoppinessIndex(period).CreateState();
        Span<double> output = stackalloc double[2];
        for (int replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (int i = 0; i < bars.Length; i++)
            {
                state.Update(in bars[i], output);
                double value = expected[i] ?? 0;
                Assert.True(value.Equals(output[0]) || Math.Abs(value - output[0]) <= 4e-15 * Math.Abs(value));
                Assert.Equal(expected[i].HasValue ? 1d : 0d, output[1]);
            }
        }
    }

    [Fact]
    public void WarmedOrdinaryWindowsAllocateNoPerBarObjects()
    {
        var state = (IMultiOutputState)new WindowChoppinessIndex(14).CreateState();
        Span<double> output = stackalloc double[2];
        for (int replay = 0; replay < 2; replay++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            state.Reset();
            for (int i = 0; i < 1000; i++)
            {
                double value = 10 + (i * 13 % 101) / 8d;
                var bar = new Bar(default, value, value + 1, value - 1, value, 1);
                state.Update(in bar, output);
            }
            if (replay > 0) Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        Assert.True(double.IsFinite(output[0])); Assert.Equal(1d, output[1]);
    }
}
