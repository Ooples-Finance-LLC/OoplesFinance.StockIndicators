using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class VolumePositiveNegativeCertifiedTests
{
    private static void Same((double Line, double Signal) expected, (double Line, double Signal) actual)
    {
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Line), BitConverter.DoubleToInt64Bits(actual.Line));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Signal), BitConverter.DoubleToInt64Bits(actual.Signal));
    }
    [Theory]
    [InlineData(MovingAvgType.ExponentialMovingAverage, 7, 3)]
    [InlineData(MovingAvgType.WildersSmoothingMethod, 7, 3)]
    [InlineData(MovingAvgType.ExponentialMovingAverage, 1, 1)]
    [InlineData(MovingAvgType.WildersSmoothingMethod, 1, 1)]
    [InlineData(MovingAvgType.SimpleMovingAverage, 7, 3)]
    public void PreviewsAndResetMatchExact(MovingAvgType kind, int length, int smooth)
    {
        using var candidate = new VolumePositiveNegativeWindow(kind, length, smooth);
        using var exact = new VolumePositiveNegativeWindow(kind, length, smooth, false);
        for (var pass = 0; pass < 2; pass++)
        {
            for (var i = 0; i < 80; i++)
            {
                var p = 10 + (i * 13 % 97) / 8d; var v = 100 + i * 17 % 53;
                Same(exact.Next(2, -1, -p, v, false, true), candidate.Next(2, -1, -p, v, false, true));
                Same(exact.Next(p + 2, p - 1, p, v, false), candidate.Next(p + 2, p - 1, p, v, false));
                Same(exact.Next(p + 2, p - 1, p, v, true), candidate.Next(p + 2, p - 1, p, v, true));
            }
            candidate.Reset(); exact.Reset();
            Assert.Equal(0, candidate.CertifiedCount); Assert.Equal(0, candidate.FallbackCount);
        }
    }
    [Theory]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WildersSmoothingMethod)]
    public void BatchBitsTradesAndRequestsMatchExact(MovingAvgType kind)
    {
        var prices = Enumerable.Range(0, 80).Select(i => 10 + (i * 13 % 97) / 8d).ToArray();
        StockData Data() => new(prices, prices.Select(p => p + 2), prices.Select(p => p - 1), prices,
            prices.Select((_, i) => (double)(100 + i * 17 % 53)), prices.Select((_, i) => DateTime.UnixEpoch.AddMinutes(i)));
        var expected = VolumePositiveNegativeWindow.Calculate(Data(), kind, 7, 3, false);
        using var scope = ComponentAverage.Arm(Array.Empty<Func<IReadOnlyList<double>, int, IReadOnlyList<double>>>());
        var actual = VolumePositiveNegativeWindow.Calculate(Data(), kind, 7, 3);
        foreach (var key in expected.Outputs.Keys)
            Assert.Equal(expected.Outputs[key].Select(BitConverter.DoubleToInt64Bits), actual.Outputs[key].Select(BitConverter.DoubleToInt64Bits));
        Assert.Equal(expected.Signals, actual.Signals); Assert.Equal(2, ComponentAverage.Requests);
    }
    [Theory]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WildersSmoothingMethod)]
    public void LongVaryingHistoryCertifiesOutputsAndTrades(MovingAvgType kind)
    {
        var candidate = new CertifiedVolumePositiveNegative(kind, 30, 3);
        for (var i = 0; i < 10000; i++)
        {
            var p = 10 + (i * 13 % 101) / 8d;
            Assert.True(candidate.TryNext(p + 1, p - 2, p, 100 + i * 17 % 53, true, false, true, out _), $"Uncertified bar {i}");
        }
    }
    [Fact]
    public void AmbiguousVotePreviewReplaysOnlyCommittedBars()
    {
        using var candidate = new VolumePositiveNegativeWindow(MovingAvgType.WildersSmoothingMethod, 1, 1);
        using var exact = new VolumePositiveNegativeWindow(MovingAvgType.WildersSmoothingMethod, 1, 1, false);
        Same(exact.Next(2, 0, 1, 1, true, true), candidate.Next(2, 0, 1, 1, true, true));
        var tiny = Math.Pow(2, -200);
        Same(exact.Next(10, -tiny, 2, 1, false, true), candidate.Next(10, -tiny, 2, 1, false, true));
        Assert.Equal(1, candidate.FallbackCount);
        Same(exact.Next(5, 1, 3, 2, true, true), candidate.Next(5, 1, 3, 2, true, true));
        candidate.Reset(); exact.Reset();
        Same(exact.Next(2, 0, 1, 1, true), candidate.Next(2, 0, 1, 1, true));
        Assert.Equal(1, candidate.CertifiedCount); Assert.Equal(0, candidate.FallbackCount);
    }
    [Fact]
    public void InvalidPreviewDoesNotMutateState()
    {
        using var candidate = new VolumePositiveNegativeWindow(MovingAvgType.ExponentialMovingAverage, 7, 3);
        using var exact = new VolumePositiveNegativeWindow(MovingAvgType.ExponentialMovingAverage, 7, 3, false);
        Same(exact.Next(2, 0, 1, 1, true), candidate.Next(2, 0, 1, 1, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => candidate.Next(2, 0, 1, double.NaN, false));
        Same(exact.Next(5, 1, 3, 2, true), candidate.Next(5, 1, 3, 2, true));
    }
}
