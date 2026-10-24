using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class CertifiedKarobeinWindowTests
{
    public static IEnumerable<object[]> Cases =>
        from kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage,
            MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod }
        from length in new[] { 1, 2, 3, 14, int.MaxValue }
        select new object[] { kind, length };

    private static void Equal((double Line, Signal Trade) expected, (double Line, Signal Trade) actual)
    {
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Line), BitConverter.DoubleToInt64Bits(actual.Line));
        Assert.Equal(expected.Trade, actual.Trade);
    }

    [Theory, MemberData(nameof(Cases))]
    public void BoundsPreserveExactValuesSignalsPreviewsAndReset(MovingAvgType kind, int length)
    {
        foreach (var prices in new double[][] {
            Enumerable.Range(0, 48).Select(i => 10d + (i * 13 % 101) / 8d).ToArray(),
            [2, 2, 2, 1, 0, -1, -2, 0, 1, -3, 8],
            [double.MaxValue, 1, -double.MaxValue, double.Epsilon, 2, -2, 0, 4] })
        {
            using var exact = new KarobeinWindow(kind, length, certify: false);
            using var bounded = new KarobeinWindow(kind, length);
            for (var replay = 0; replay < 2; replay++)
            {
                foreach (var price in prices)
                foreach (var final in new[] { false, false, true })
                {
                    (double Line, Signal Trade) expected = default, actual = default;
                    var expectedError = Record.Exception(() => expected = exact.Next(price, final));
                    var actualError = Record.Exception(() => actual = bounded.Next(price, final));
                    Assert.Equal(expectedError?.GetType(), actualError?.GetType());
                    Assert.Equal(expectedError?.Message, actualError?.Message);
                    if (expectedError is null) Equal(expected, actual);
                }
                exact.Reset(); bounded.Reset(); Assert.Equal(0, bounded.ExactReplayUpdates);
            }
        }
    }

    [Fact]
    public void PrecisionRefinementPreservesExactPreviewsAndCommits()
    {
        using var bounded = new CertifiedKarobeinWindow(MovingAvgType.ExponentialMovingAverage, 3, precision: 16);
        using var exact = new KarobeinWindow(MovingAvgType.ExponentialMovingAverage, 3, certify: false);
        for (var i = 0; i < 160; i++)
        {
            var price = 20 + Math.Sin(i * .17) + (i % 17) / 16d;
            Equal(exact.Next(price, false), bounded.Next(price, false));
            Equal(exact.Next(price, true), bounded.Next(price, true));
        }
        Assert.True(bounded.PrecisionRefinements > 0);
        bounded.Reset(); exact.Reset();
        Assert.Equal(0, bounded.PrecisionRefinements);
        Equal(exact.Next(20, true), bounded.Next(20, true));
    }

    [Theory]
    [InlineData(MovingAvgType.SimpleMovingAverage)]
    [InlineData(MovingAvgType.WeightedMovingAverage)]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WildersSmoothingMethod)]
    public void PeriodOnePreservesExactSignalsWithoutReplayOrRefinement(MovingAvgType kind)
    {
        double[] prices = [2, 1, 1, 0, -1, -2, -1, 0, double.Epsilon,
            double.MaxValue, -double.MaxValue, -double.Epsilon, 1e-300, 1e300];
        using var bounded = new CertifiedKarobeinWindow(kind, 1);
        using var exact = new KarobeinWindow(kind, 1, certify: false);
        for (var pass = 0; pass < 2; pass++)
        {
            for (var i = 0; i < 10000; i++)
            {
                var price = prices[i % prices.Length];
                Equal(exact.Next(-price, false), bounded.Next(-price, false));
                Equal(exact.Next(price, false), bounded.Next(price, false));
                Equal(exact.Next(price, true), bounded.Next(price, true));
            }
            Assert.Equal(0, bounded.ExactReplayUpdates);
            Assert.Equal(0, bounded.PrecisionRefinements);
            bounded.Reset(); exact.Reset();
        }
    }

    [Fact]
    public void ExtremeCancellationRefinesBoundsWithoutChangingOutputs()
    {
        using var exact = new KarobeinWindow(MovingAvgType.ExponentialMovingAverage, 14, certify: false);
        using var bounded = new CertifiedKarobeinWindow(MovingAvgType.ExponentialMovingAverage, 14);
        foreach (var price in new[] { double.MaxValue, 1, -double.MaxValue, 1, 2, 3 })
            Equal(exact.Next(price, true), bounded.Next(price, true));
        Assert.True(bounded.PrecisionRefinements > 0);
        Assert.Equal(0, bounded.ExactReplayUpdates);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactPoleStillReplaysAndLeavesRejectedUpdateUncommitted(bool final)
    {
        using var exact = new KarobeinWindow(MovingAvgType.ExponentialMovingAverage, 2, certify: false);
        using var bounded = new CertifiedKarobeinWindow(MovingAvgType.ExponentialMovingAverage, 2);
        foreach (var price in new[] { 1d, 3 }) Equal(exact.Next(price, true), bounded.Next(price, true));
        var expectedError = Assert.Throws<ArgumentException>(() => exact.Next(-2, final));
        var actualError = Assert.Throws<ArgumentException>(() => bounded.Next(-2, final));
        Assert.Equal(expectedError.Message, actualError.Message);
        Assert.True(bounded.ExactReplayUpdates > 0);
        Equal(exact.Next(4, true), bounded.Next(4, true));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void RejectedInputDoesNotAdvanceCertifiedOrExactState(double invalid)
    {
        using var clean = new KarobeinWindow(MovingAvgType.ExponentialMovingAverage, 3);
        using var probed = new KarobeinWindow(MovingAvgType.ExponentialMovingAverage, 3);
        foreach (var price in new[] { 2d, 3, 1, 4 })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => probed.Next(invalid, true));
            Equal(clean.Next(price, true), probed.Next(price, true));
        }
    }

    [Fact]
    public void BatchRetainsAverageRequestAccountingWithoutCallbacks()
    {
        double[] input = [2, 3, 1, 4];
        var data = new StockData(input, input, input, input, input,
            Enumerable.Range(0, input.Length).Select(i => DateTime.UnixEpoch.AddMinutes(i)));
        using var scope = OoplesFinance.StockIndicators.Builder.Compute.ComponentAverage.Arm(
            Array.Empty<Func<IReadOnlyList<double>, int, IReadOnlyList<double>>>());
        KarobeinWindow.Calculate(data, MovingAvgType.ExponentialMovingAverage, 3, callbacks: true);
        Assert.Equal(3, OoplesFinance.StockIndicators.Builder.Compute.ComponentAverage.Requests);
        Assert.Equal(3, OoplesFinance.StockIndicators.Builder.Compute.ComponentAverage.LengthAsked);
        Assert.Equal(0, OoplesFinance.StockIndicators.Builder.Compute.ComponentAverage.Substitutions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LongConstantAndRisingHistoriesCertifyFlatSignals(bool rising)
    {
        using var window = new KarobeinWindow(MovingAvgType.ExponentialMovingAverage, 14);
        for (var i = 0; i < 10000; i++)
        {
            var point = window.Next(rising ? 10 + i / 16d : 10, true);
            Assert.Equal(i == 0 ? 0d : 1d, point.Line);
            Assert.Equal(i == 0 ? Signal.None : i == 1 ? Signal.StrongBuy : Signal.None, point.Trade);
        }
        Assert.Equal(0, window.ExactReplayUpdates);
    }

    [Fact]
    public void TinyDistanceFromOneKeepsExactSignalsWithoutGrowingReplay()
    {
        using var bounded = new KarobeinWindow(MovingAvgType.ExponentialMovingAverage, 14);
        using var exact = new KarobeinWindow(MovingAvgType.ExponentialMovingAverage, 14, certify: false);
        for (var i = 0; i < 10000; i++)
        {
            var price = i % 2 == 0 ? 1e-300 : 1e300;
            var actual = bounded.Next(price, true);
            Assert.InRange(bounded.ExactReplayUpdates, 0, 256);
            if (i < 32) Equal(exact.Next(price, true), actual);
            Assert.InRange(actual.Line, 0, 1);
        }
        Assert.InRange(bounded.ExactReplayUpdates, 0, 256);
    }

    [Fact]
    public void LongOrdinaryHistoryDoesNotReplayGrowingExactFractions()
    {
        using var window = new KarobeinWindow(MovingAvgType.ExponentialMovingAverage, 14);
        for (var i = 0; i < 10000; i++)
        {
            var point = window.Next(20 + Math.Sin(i * .17) + (i % 17) / 16d, true);
            Assert.InRange(point.Line, 0, 1);
        }
        // A late fallback would replay the whole history. A small startup replay
        // is acceptable; growing exact arithmetic across the workload is not.
        Assert.InRange(window.ExactReplayUpdates, 0, 256);
    }
}
