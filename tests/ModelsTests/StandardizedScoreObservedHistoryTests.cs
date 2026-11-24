using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class StandardizedScoreObservedHistoryTests
{
    public static IEnumerable<object[]> Cases =>
        from length in new[] { 1, 2, 14, 257 }
        from fast in new[] { false, true }
        from exact in new[] { false, true }
        select new object[] { length, fast, exact };

    [Theory, MemberData(nameof(Cases))]
    public void ObservedHistoryMatchesExistingStorageAcrossPreviewResetAndExpiration(int length, bool fast, bool exact)
    {
        using var expected = new StandardizedScoreWindow(length, fast);
        using var actual = new StandardizedScoreWindow(length, fast, observedHistory: true);
        double[] prefix = [-0d, 0d, double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue];
        var prices = prefix.Concat(Enumerable.Range(0, 300).Select(i => (i * 17 % 101 - 50) / 8d)).ToArray();
        for (var replay = 0; replay < 2; replay++)
        {
            expected.Reset(); actual.Reset();
            foreach (var price in prices)
            foreach (var commit in new[] { false, true })
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Next(price, price / 2, exact, commit)),
                    BitConverter.DoubleToInt64Bits(actual.Next(price, price / 2, exact, commit)));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MaximumPeriodRetainsLogicalWarmupWithObservedStorage(bool fast)
    {
        using var score = new StandardizedScoreWindow(int.MaxValue, fast, observedHistory: true);
        foreach (var value in new[] { double.MaxValue, -double.MaxValue, double.Epsilon, 0d })
            Assert.Equal(0L, BitConverter.DoubleToInt64Bits(score.Next(value, 0, true, true)));
    }
}
