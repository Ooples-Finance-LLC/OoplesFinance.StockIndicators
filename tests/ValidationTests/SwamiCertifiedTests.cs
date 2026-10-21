using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class SwamiCertifiedTests
{
    private static void Same((double Line, Signal Trade) expected, (double Line, Signal Trade) actual)
    {
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Line), BitConverter.DoubleToInt64Bits(actual.Line));
        Assert.Equal(expected.Trade, actual.Trade);
    }
    [Theory]
    [InlineData(12, 48)]
    [InlineData(1, 1)]
    [InlineData(2, 7)]
    [InlineData(int.MinValue, int.MaxValue)]
    public void BitsTradesPreviewsClampsAndResetMatchExact(int fast, int slow)
    {
        using var candidate = new SwamiWindow(fast, slow); using var exact = new SwamiWindow(fast, slow, false);
        for (var pass = 0; pass < 2; pass++)
        {
            for (var i = 0; i < 80; i++)
            {
                var p = 10 + (i * 13 % 97) / 8d;
                Same(exact.Next(-100, p + 2, p - 1, false), candidate.Next(-100, p + 2, p - 1, false));
                Same(exact.Next(10000, p + 2, p - 1, false), candidate.Next(10000, p + 2, p - 1, false));
                Same(exact.Next(p, p + 2, p - 1, true), candidate.Next(p, p + 2, p - 1, true));
            }
            candidate.Reset(); exact.Reset(); Assert.Equal(0, candidate.CertifiedCount); Assert.Equal(0, candidate.FallbackCount);
        }
    }
    [Fact]
    public void LongVaryingHistoryCertifiesEveryBar()
    {
        using var candidate = new SwamiWindow(12, 48);
        for (var i = 0; i < 10000; i++)
        {
            var p = 10 + (i * 13 % 101) / 8d;
            var point = candidate.Next(p, p + 1, p - 2, true);
            Assert.InRange(point.Line, 0, 1);
            Assert.Equal(0, candidate.FallbackCount);
        }
        Assert.Equal(10000, candidate.CertifiedCount);
    }
    [Fact]
    public void AmbiguousClampPreviewReplaysOnlyCommittedHistory()
    {
        using var candidate = new SwamiWindow(1, 1); using var exact = new SwamiWindow(1, 1, false);
        Same(exact.Next(0, 0, 0, true), candidate.Next(0, 0, 0, true));
        var tiny = Math.Pow(2, -200);
        Same(exact.Next(5, 1, -tiny, false), candidate.Next(5, 1, -tiny, false));
        Assert.Equal(1, candidate.FallbackCount);
        Same(exact.Next(1, 2, 0, true), candidate.Next(1, 2, 0, true));
        candidate.Reset(); exact.Reset();
        Same(exact.Next(1, 2, 0, true), candidate.Next(1, 2, 0, true));
        Assert.Equal(0, candidate.FallbackCount); Assert.Equal(1, candidate.CertifiedCount);
    }
    [Fact]
    public void InvalidPreviewLeavesStateUnchanged()
    {
        using var candidate = new SwamiWindow(2, 7); using var exact = new SwamiWindow(2, 7, false);
        Same(exact.Next(1, 2, 0, true), candidate.Next(1, 2, 0, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => candidate.Next(double.NaN, 2, 0, false));
        Same(exact.Next(3, 4, 1, true), candidate.Next(3, 4, 1, true));
    }
}
