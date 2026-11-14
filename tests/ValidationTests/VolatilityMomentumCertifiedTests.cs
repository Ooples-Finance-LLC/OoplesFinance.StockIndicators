using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Builder.Compute;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class VolatilityMomentumCertifiedTests
{
    private static void Same((double Line, double Signal) expected, (double Line, double Signal) actual)
    {
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Line), BitConverter.DoubleToInt64Bits(actual.Line));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Signal), BitConverter.DoubleToInt64Bits(actual.Signal));
    }

    [Theory]
    [InlineData(MovingAvgType.ExponentialMovingAverage, 3, 7)]
    [InlineData(MovingAvgType.WildersSmoothingMethod, 3, 7)]
    [InlineData(MovingAvgType.ExponentialMovingAverage, 1, 1)]
    [InlineData(MovingAvgType.WildersSmoothingMethod, 1, 1)]
    [InlineData(MovingAvgType.SimpleMovingAverage, 3, 7)]
    public void PreviewsSelectedRangesAndResetMatchExact(MovingAvgType kind, int lag, int range)
    {
        using var candidate = new VolatilityMomentumWindow(kind, lag, range);
        using var exact = new VolatilityMomentumWindow(kind, lag, range, false);
        for (var pass = 0; pass < 2; pass++)
        {
            for (var i = 0; i < 100; i++)
            {
                var price = 10 + (i * 13 % 97) / 8d;
                Same(exact.Next(2, -1, -price, false, true), candidate.Next(2, -1, -price, false, true));
                Same(exact.Next(price + 2, price - 1, price, false), candidate.Next(price + 2, price - 1, price, false));
                Same(exact.Next(price + 2, price - 1, price, true), candidate.Next(price + 2, price - 1, price, true));
            }
            candidate.Reset(); exact.Reset();
            Assert.Equal(0, candidate.CertifiedCount); Assert.Equal(0, candidate.FallbackCount);
        }
    }

    [Theory]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WildersSmoothingMethod)]
    public void BatchBitsAndTradesMatchPreservedExactPath(MovingAvgType kind)
    {
        var prices = Enumerable.Range(0, 100).Select(i => 10 + (i * 13 % 97) / 8d).ToArray();
        StockData Data() => new(prices, prices.Select(p => p + 2), prices.Select(p => p - 1), prices,
            prices.Select(_ => 1d), prices.Select((_, i) => DateTime.UnixEpoch.AddMinutes(i)));
        foreach (var output in new string?[] { null, "Vbm", "Signal" })
        {
            var expected = VolatilityMomentumWindow.Calculate(Data(), kind, 3, 7, output, false);
            var actual = VolatilityMomentumWindow.Calculate(Data(), kind, 3, 7, output);
            Assert.Equal(expected.Outputs.Keys, actual.Outputs.Keys);
            foreach (var key in expected.Outputs.Keys)
                Assert.Equal(expected.Outputs[key].Select(BitConverter.DoubleToInt64Bits), actual.Outputs[key].Select(BitConverter.DoubleToInt64Bits));
            Assert.Equal(expected.Signals, actual.Signals);
        }
    }

    [Theory]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WildersSmoothingMethod)]
    public void DefaultLongVaryingHistoryCertifiesEveryBar(MovingAvgType kind)
    {
        using var candidate = new VolatilityMomentumWindow(kind, 22, 65);
        var certification = new CertifiedVolatilityMomentum(kind, 22, 65);
        for (var i = 0; i < 10000; i++)
        {
            var price = 10 + (i * 13 % 97) / 8d;
            Assert.True(certification.TryNext(price + 2, price - 1, price, true, false, false, false, out _), $"Uncertified bar {i}");
            var point = candidate.Next(price + 2, price - 1, price, true);
            Assert.True(double.IsFinite(point.Line)); Assert.True(double.IsFinite(point.Signal));
        }
        Assert.Equal(10000, candidate.CertifiedCount); Assert.Equal(0, candidate.FallbackCount);
    }

    [Theory]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WildersSmoothingMethod)]
    public void LongBenchmarkHistoryCertifiesOutputsAndTradeSigns(MovingAvgType kind)
    {
        var candidate = new CertifiedVolatilityMomentum(kind, 22, 65);
        for (var i = 0; i < 10000; i++)
        {
            var price = 10 + (i * 13 % 101) / 8d;
            Assert.True(candidate.TryNext(price + 1, price - 2, price, true, false, true, false, out _), $"Uncertified bar {i}");
        }
    }

    [Theory]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WildersSmoothingMethod)]
    public void LongAlternatingHistoryHasIndependentExactOutputs(MovingAvgType kind)
    {
        using var candidate = new VolatilityMomentumWindow(kind, 1, 1);
        for (var i = 0; i < 10000; i++)
        {
            var close = i % 2 == 0 ? -1d : 1d;
            var expected = i == 0 ? 0 : close;
            Same((expected, expected), candidate.Next(1, -1, close, true));
        }
        Assert.Equal(10000, candidate.CertifiedCount); Assert.Equal(0, candidate.FallbackCount);
    }

    [Fact]
    public void AmbiguousMidpointPreviewReplaysOnlyCommittedBars()
    {
        using var candidate = new VolatilityMomentumWindow(MovingAvgType.WildersSmoothingMethod, 1, 1);
        using var exact = new VolatilityMomentumWindow(MovingAvgType.WildersSmoothingMethod, 1, 1, false);
        var tiny = Math.Pow(2, -200); var previous = -Math.Pow(2, -53);
        Same(exact.Next(tiny, -1, previous, true), candidate.Next(tiny, -1, previous, true));
        // (1+2^-53)/(1+2^-200) lies just below a binary64 midpoint.
        // The 160-bit enclosure straddles that midpoint and must fall back.
        Same(exact.Next(tiny, -1, 1, false), candidate.Next(tiny, -1, 1, false));
        Assert.Equal(1, candidate.FallbackCount);
        Same(exact.Next(4, -1, 3, true), candidate.Next(4, -1, 3, true));
        candidate.Reset(); exact.Reset();
        Same(exact.Next(2, 0, 1, true), candidate.Next(2, 0, 1, true));
        Assert.Equal(1, candidate.CertifiedCount); Assert.Equal(0, candidate.FallbackCount);
    }

    [Fact]
    public void FastSignalRetainsUnsubstitutedComponentRequest()
    {
        double[] prices = [1, 2, 3, 1];
        StockData Data() => new(prices, prices.Select(p => p + 1), prices.Select(p => p - 1), prices,
            prices.Select(_ => 1d), prices.Select((_, i) => DateTime.UnixEpoch.AddMinutes(i)));
        foreach (var output in new string?[] { null, "Vbm", "Signal" })
        {
            using var scope = ComponentAverage.Arm(Array.Empty<Func<IReadOnlyList<double>, int, IReadOnlyList<double>>>());
            VolatilityMomentumWindow.Calculate(Data(), MovingAvgType.WildersSmoothingMethod, 2, 3, output);
            Assert.Equal(output == "Signal" ? 1 : 0, ComponentAverage.Requests);
            Assert.Equal(0, ComponentAverage.Substitutions);
        }
    }

    [Fact]
    public void InvalidPreviewLeavesCertifiedStateUnchanged()
    {
        using var candidate = new VolatilityMomentumWindow(MovingAvgType.WildersSmoothingMethod, 2, 3);
        using var exact = new VolatilityMomentumWindow(MovingAvgType.WildersSmoothingMethod, 2, 3, false);
        Same(exact.Next(2, 0, 1, true), candidate.Next(2, 0, 1, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => candidate.Next(2, 0, double.NaN, false));
        Same(exact.Next(4, 1, 3, true), candidate.Next(4, 1, 3, true));
    }
}
