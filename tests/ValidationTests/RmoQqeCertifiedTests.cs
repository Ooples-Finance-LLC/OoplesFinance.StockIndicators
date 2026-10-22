using OoplesFinance.StockIndicators.Helpers;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class RmoQqeCertifiedTests
{
    private static void Same((double Rmo, double Swing1, double Swing2, double Swing3, Signal Trade) a,
        (double Rmo, double Swing1, double Swing2, double Swing3, Signal Trade) b)
    {
        Assert.Equal(new[] { a.Rmo, a.Swing1, a.Swing2, a.Swing3 }.Select(BitConverter.DoubleToInt64Bits),
            new[] { b.Rmo, b.Swing1, b.Swing2, b.Swing3 }.Select(BitConverter.DoubleToInt64Bits));
        Assert.Equal(a.Trade, b.Trade);
    }
    private static void Same((double Fast, double Slow, Signal Trade) a, (double Fast, double Slow, Signal Trade) b)
    {
        Assert.Equal(BitConverter.DoubleToInt64Bits(a.Fast), BitConverter.DoubleToInt64Bits(b.Fast));
        Assert.Equal(BitConverter.DoubleToInt64Bits(a.Slow), BitConverter.DoubleToInt64Bits(b.Slow)); Assert.Equal(a.Trade, b.Trade);
    }
    [Theory]
    [InlineData(2, 10, 30, 81)]
    [InlineData(1, 2, 1, 1)]
    [InlineData(3, 7, 5, 9)]
    public void RmoBitsTradesPreviewsAndResetMatchExact(int first, int range, int swing, int output)
    {
        using var candidate = new RmoWindow(first, range, swing, output); using var exact = new RmoWindow(first, range, swing, output, false);
        for (var pass = 0; pass < 2; pass++)
        {
            for (var i = 0; i < 80; i++)
            {
                var p = 10 + (i * 13 % 97) / 8d;
                Same(exact.Next(-p, false), candidate.Next(-p, false));
                Same(exact.Next(p, true), candidate.Next(p, true));
            }
            candidate.Reset(); exact.Reset(); Assert.Equal(0, candidate.CertifiedCount); Assert.Equal(0, candidate.FallbackCount);
        }
    }
    [Theory]
    [InlineData(MovingAvgType.ExponentialMovingAverage, 14, 5, 2.618, 4.236)]
    [InlineData(MovingAvgType.WildersSmoothingMethod, 14, 5, 2.618, 4.236)]
    [InlineData(MovingAvgType.ExponentialMovingAverage, 1, 1, 0, 0)]
    [InlineData(MovingAvgType.WildersSmoothingMethod, 3, 1, 1, 1)]
    [InlineData(MovingAvgType.ExponentialMovingAverage, 3, 2, 0.5, 0.25)]
    [InlineData(MovingAvgType.SimpleMovingAverage, 3, 2, 2, 3)]
    public void QqeBitsTradesPreviewsAndResetMatchExact(MovingAvgType kind, int length, int smooth, double fast, double slow)
    {
        using var candidate = new QqeWindow(kind, length, smooth, fast, slow);
        using var exact = new QqeWindow(kind, length, smooth, fast, slow, allowCertified: false);
        for (var pass = 0; pass < 2; pass++)
        {
            for (var i = 0; i < 80; i++)
            {
                var p = 10 + (i * 13 % 97) / 8d;
                Same(exact.Next(-p, false), candidate.Next(-p, false));
                Same(exact.Next(p, true), candidate.Next(p, true));
            }
            candidate.Reset(); exact.Reset(); Assert.Equal(0, candidate.CertifiedCount); Assert.Equal(0, candidate.FallbackCount);
        }
    }
    [Fact]
    public void LongRmoHistoryCertifiesAllOutputsAndTrades()
    {
        using var candidate = new RmoWindow(2, 10, 30, 81);
        for (var i = 0; i < 10000; i++)
        {
            var point = candidate.Next(10 + (i * 13 % 101) / 8d, true);
            Assert.True(double.IsFinite(point.Rmo)); Assert.Equal(0, candidate.FallbackCount);
        }
        Assert.Equal(10000, candidate.CertifiedCount);
    }
    [Theory]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WildersSmoothingMethod)]
    public void LongQqeHistoryCertifiesBothOutputsAndTrades(MovingAvgType kind)
    {
        using var candidate = new QqeWindow(kind, 14, 5, 2.618, 4.236);
        for (var i = 0; i < 10000; i++)
        {
            var point = candidate.Next(10 + (i * 13 % 101) / 8d, true);
            Assert.True(double.IsFinite(point.Fast)); Assert.True(double.IsFinite(point.Slow)); Assert.Equal(0, candidate.FallbackCount);
        }
        Assert.Equal(10000, candidate.CertifiedCount);
    }
    [Fact]
    public void UncertainRmoTradeProposalDoesNotCommit()
    {
        var candidate = new CertifiedRmoQqe.Rmo(1, 1); var control = new CertifiedRmoQqe.Rmo(1, 1);
        Assert.True(candidate.TryNext(Number.Of(1), true, out _)); Assert.True(control.TryNext(Number.Of(1), true, out _));
        Assert.False(candidate.TryNext(Number.Of(1) + Number.Of(Math.Pow(2, -200)), false, out _));
        Assert.True(candidate.TryNext(Number.Of(2), true, out var actual)); Assert.True(control.TryNext(Number.Of(2), true, out var expected));
        Same(expected, actual);
    }
    [Fact]
    public void InvalidPreviewsLeaveBothWindowsUnchanged()
    {
        using var rmo = new RmoWindow(2, 10, 30, 81); using var exactRmo = new RmoWindow(2, 10, 30, 81, false);
        using var qqe = new QqeWindow(MovingAvgType.ExponentialMovingAverage, 14, 5, 2.618, 4.236);
        using var exactQqe = new QqeWindow(MovingAvgType.ExponentialMovingAverage, 14, 5, 2.618, 4.236, allowCertified: false);
        Same(exactRmo.Next(1, true), rmo.Next(1, true)); Same(exactQqe.Next(1, true), qqe.Next(1, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => rmo.Next(double.NaN, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => qqe.Next(double.NaN, false));
        Same(exactRmo.Next(2, true), rmo.Next(2, true)); Same(exactQqe.Next(2, true), qqe.Next(2, true));
    }
}
