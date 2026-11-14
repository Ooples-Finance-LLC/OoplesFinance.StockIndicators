using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class QuickWindowRecurrenceTests
{
    public static IEnumerable<object[]> Periods =>
        new[] { int.MinValue, -3, 1, 2, 3, 4, 7, 20, 529, 530, 531, 1587, 1588, 1590, 1591, 4096, int.MaxValue }
            .Select(period => new object[] { period });

    [Theory, MemberData(nameof(Periods))]
    public void RollingRecurrenceMatchesHistoricalTapCalculation(int period)
    {
        int count = period > 0 && period < 2000 ? Math.Max(65, period + 9) : 129;
        foreach (double[] values in new double[][]
        {
            Enumerable.Range(0, count).Select(i => (i * 13 % 101 - 50) / 8d).ToArray(),
            [-0d, 0d, double.Epsilon, -double.Epsilon, 0d, double.MaxValue, double.MaxValue, -double.MaxValue, 0d, -double.MaxValue],
            Enumerable.Range(0, 65).Select(i => Math.ScaleB(i % 13 - 6d, i % 2 == 0 ? -1022 : 900)).ToArray()
        })
        {
            // Compute retains the historical full weighted-tap loop unchanged.
            var expected = new double[values.Length];
            QuickWindowMean.Compute(values, expected, period);
            using var actual = new QuickWindowMean(period, values.Length);
            for (int replay = 0; replay < 2; replay++)
            {
                actual.Reset();
                for (int i = 0; i < values.Length; i++)
                {
                    actual.Next(-values[i], false);
                    Bits(expected[i], actual.Next(values[i], false));
                    Bits(expected[i], actual.Next(values[i], true));
                }
            }
        }
    }

    [Fact]
    public void RejectedNonfiniteUpdatesLeaveCommittedHistoryIntact()
    {
        double[] values = [1, 2, 4, 8];
        var expected = new double[values.Length];
        QuickWindowMean.Compute(values, expected, 3);
        using var actual = new QuickWindowMean(3);
        Bits(expected[0], actual.Next(1, true));
        Bits(expected[1], actual.Next(2, true));
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (bool commit in new[] { false, true })
            Assert.Throws<ArithmeticException>(() => actual.Next(invalid, commit));
        Bits(expected[2], actual.Next(4, true));
        Bits(expected[3], actual.Next(8, true));
        actual.Reset();
        Bits(expected[0], actual.Next(1, true));
    }

    private static void Bits(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
}
