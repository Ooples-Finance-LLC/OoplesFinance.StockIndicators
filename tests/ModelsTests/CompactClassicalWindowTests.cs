using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactClassicalWindowTests
{
    public static IEnumerable<object[]> Cases =>
        from method in new[] { ClassicAverageMethod.Sma, ClassicAverageMethod.Wma, ClassicAverageMethod.Trima }
        from period in new[] { 1, 2, 3, 4, 7, 26, int.MaxValue }
        select new object[] { method, period };

    [Theory, MemberData(nameof(Cases))]
    public void ExactWindowReferencePreservesBitsWideStagesAndReset(ClassicAverageMethod method, int period)
    {
        var state = new WideClassicalWindowState(period, method);
        var random = new Random(301026);
        var inputs = new List<BigInteger> { 0, -1, 0, 0, 1, -1, 3, -3 };
        inputs.AddRange(Enumerable.Range(0, 300).Select(i =>
            ExactVarianceWindow.Units(100 + random.NextDouble() * 20)));
        inputs.AddRange(new[] { BigInteger.One << 2400, -(BigInteger.One << 2400),
            (BigInteger.One << 127) - 1, -(BigInteger.One << 127),
            BigInteger.One << 1074, BigInteger.One, -BigInteger.One });
        inputs.AddRange(Enumerable.Repeat(BigInteger.Zero, 60));
        for (int replay = 0; replay < 2; replay++)
        {
            state.Reset();
            Assert.Equal(BigInteger.Zero, state.RoundedUnits);
            var history = new List<BigInteger>();
            var outputs = new double[2];
            foreach (var input in inputs)
            {
                history.Add(input);
                var (numerator, denominator, present) = Reference(history, method, period);
                double expected = 0;
                var expectedError = Record.Exception(() =>
                {
                    if (present) expected = ExactMeanAccumulator.UnitRatio(numerator, denominator);
                });
                outputs[0] = outputs[1] = 123;
                var actualError = Record.Exception(() => state.UpdateUnits(input, outputs));
                Assert.Equal(expectedError?.GetType(), actualError?.GetType());
                Assert.Equal(RocBankValue.RoundUnits(numerator, denominator), state.RoundedUnits);
                if (expectedError is not null) continue;
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(outputs[0]));
                Assert.Equal(present ? 1d : 0d, outputs[1]);
            }
        }
    }

    [Fact]
    public void RatioPublicationPreservesWideReferenceBitsAndExceptions()
    {
        var random = new Random(301027);
        for (int i = 0; i < 2000; i++)
        {
            BigInteger units = new BigInteger(random.NextInt64()) << random.Next(0, 2300);
            units += i % 3 - 1;
            if (i % 2 == 0) units = -units;
            long divisor = i % 3 == 0 ? 2 : random.NextInt64(1, long.MaxValue);
            var value = CompactMacdValue.FromUnits(units);
            double expected = 0, actual = 0;
            var expectedError = Record.Exception(() => expected = ExactMeanAccumulator.UnitRatio(units, divisor));
            var actualError = Record.Exception(() => actual = value.PublishRatio(divisor));
            Assert.Equal(expectedError?.GetType(), actualError?.GetType());
            if (expectedError is null)
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
        }
        foreach (long units in new long[] { -3, -1, 0, 1, 3 })
        foreach (long divisor in new long[] { 1, 2, 3, long.MaxValue })
            Assert.Equal(BitConverter.DoubleToInt64Bits(ExactMeanAccumulator.UnitRatio(units, divisor)),
                BitConverter.DoubleToInt64Bits(CompactMacdValue.FromUnits(units).PublishRatio(divisor)));
        Assert.Throws<ArgumentOutOfRangeException>(() => CompactMacdValue.Of(1).PublishRatio(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => CompactMacdValue.Of(1).PublishRatio(-1));
    }

    // Recompute each window directly; no rolling recurrence is shared with production.
    private static (BigInteger Numerator, long Denominator, bool Present) Reference(
        List<BigInteger> history, ClassicAverageMethod method, int period)
    {
        int count = Math.Min(history.Count, period);
        BigInteger numerator = 0;
        if (method == ClassicAverageMethod.Trima)
        {
            if (history.Count < period) return (0, 1, false);
            int first = (int)((period + 1L) / 2), second = period - first + 1;
            for (int outer = 0; outer < second; outer++)
                for (int inner = 0; inner < first; inner++)
                    numerator += history[history.Count - 1 - outer - inner];
            return (numerator, (long)first * second, true);
        }
        for (int age = 0; age < count; age++)
            numerator += history[history.Count - 1 - age] *
                (method == ClassicAverageMethod.Wma ? period - age : 1);
        long denominator = method == ClassicAverageMethod.Wma
            ? (long)count * (2L * period - count + 1) / 2 : count;
        return (numerator, denominator, true);
    }
}
