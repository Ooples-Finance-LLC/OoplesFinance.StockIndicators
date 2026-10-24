using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.HelpersTests;

public sealed class AdaptiveRangeMaximumTests
{
    public static IEnumerable<object[]> Cases =>
        from period in new[] { int.MinValue, 1, 2, 3, 7, 20, 65, 4096, int.MaxValue }
        from smooth in new[] { 0d, 1.5, -2d }
        select new object[] { period, smooth };

    [Theory, MemberData(nameof(Cases))]
    public void MaximumCandidatesMatchFrozenScanAcrossExpiryPreviewResetAndRejectedUpdates(int period, double smooth)
    {
        using var expected = new PreviousAdaptiveFitWindow(period, smooth);
        using var actual = new AdaptiveFitWindow(period, smooth);
        void Check(double price, double high, double low, bool commit)
        {
            (double Value, Signal Signal) left = default, right = default;
            var leftError = Record.Exception(() => left = expected.Next(price, high, low, commit));
            var rightError = Record.Exception(() => right = actual.Next(price, high, low, commit));
            Assert.Equal(leftError?.GetType(), rightError?.GetType());
            Assert.Equal(leftError?.Message, rightError?.Message);
            if (leftError is null)
            {
                Assert.Equal(BitConverter.DoubleToInt64Bits(left.Value), BitConverter.DoubleToInt64Bits(right.Value));
                Assert.Equal(left.Signal, right.Signal);
            }
        }
        for (int replay = 0; replay < 2; replay++)
        {
            expected.Reset(); actual.Reset();
            for (int i = 0; i < 300; i++)
            {
                double price = i % 11 - 5;
                double range = i < 100 ? 101 - i : i < 200 ? i - 99 : i % 7 == 0 ? 200 : 3;
                Check(-price, double.MaxValue, -double.MaxValue, false);
                Check(price, range, -range, false);
                Check(price, range, -range, true);
            }
            // Expire an isolated peak, then grow a descending queue from a wrapped head.
            foreach (var range in new[] { 1000d }.Concat(Enumerable.Repeat(100d, 65)).Concat(Enumerable.Range(1, 99).Reverse().Select(i => (double)i)))
            {
                Check(0, range, -range, false);
                Check(0, range, -range, true);
            }
            foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            foreach (var commit in new[] { false, true })
            {
                Check(bad, 1, -1, commit);
                Check(0, bad, -1, commit);
                Check(0, 1, bad, commit);
                Check(3, 7, -2, true);
            }
            foreach (var value in new[] { 0d, -0d, double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue, 1d, 2, 3 })
            {
                Check(value, -value, value, false);
                Check(value, -value, value, true);
            }
        }
    }
}
