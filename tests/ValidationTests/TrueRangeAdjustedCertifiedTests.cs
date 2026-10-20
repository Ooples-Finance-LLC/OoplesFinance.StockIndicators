using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class TrueRangeAdjustedCertifiedTests
{
    private static void Same((double Line, Signal Trade) expected, (double Line, Signal Trade) actual)
    { Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Line), BitConverter.DoubleToInt64Bits(actual.Line)); Assert.Equal(expected.Trade, actual.Trade); }

    [Theory]
    [InlineData(3, 1.5)] [InlineData(14, 1.5)] [InlineData(200, .25)]
    [InlineData(3, 0)] [InlineData(1, 1.5)] [InlineData(2, 1.5)] [InlineData(14, -1)]
    public void PreviewAndResetPreserveExactRecurrence(int length, double multiplier)
    {
        var candidate = new TrueRangeAdjustedWindow(length, multiplier);
        var exact = new TrueRangeAdjustedWindow(length, multiplier, false);
        for (var pass = 0; pass < 2; pass++)
        {
            for (var i = 0; i < 160; i++)
            {
                var price = 10 + (i * 13 % 101) / 8d;
                Same(exact.Next(-price, price + 3, -price - 2, false), candidate.Next(-price, price + 3, -price - 2, false));
                Same(exact.Next(price, price + 1, price - 2, false), candidate.Next(price, price + 1, price - 2, false));
                Same(exact.Next(price, price + 1, price - 2, true), candidate.Next(price, price + 1, price - 2, true));
            }
            candidate.Reset(); exact.Reset(); Assert.Equal(0, candidate.CertifiedCount); Assert.Equal(0, candidate.FallbackCount);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(-1)] [InlineData(double.MaxValue)] [InlineData(double.Epsilon)]
    public void ConstantLongHistoriesPreserveBitsAndSignals(double price)
    {
        var candidate = new TrueRangeAdjustedWindow(14, 1.5);
        var exact = new TrueRangeAdjustedWindow(14, 1.5, false);
        for (var i = 0; i < 10000; i++) Same(exact.Next(price, price, price, true), candidate.Next(price, price, price, true));
        Assert.Equal(10000, candidate.CertifiedCount); Assert.Equal(0, candidate.FallbackCount);
    }

    [Fact]
    public void LongVaryingHistoryCertifiesEveryBar()
    {
        var candidate = new TrueRangeAdjustedWindow(14, 1.5);
        for (var i = 0; i < 10000; i++)
        {
            var price = 10 + (i * 13 % 101) / 8d;
            var value = candidate.Next(price, price + 1, price - 2, true);
            Assert.True(double.IsFinite(value.Line));
        }
        Assert.Equal(10000, candidate.CertifiedCount); Assert.Equal(0, candidate.FallbackCount);
    }

    [Fact]
    public void VaryingHistoryMatchesExactBitsAndSignals()
    {
        var candidate = new TrueRangeAdjustedWindow(14, 1.5);
        var exact = new TrueRangeAdjustedWindow(14, 1.5, false);
        for (var i = 0; i < 1000; i++)
        {
            var price = 10 + (i * 13 % 101) / 8d;
            Same(exact.Next(price, price + 1, price - 2, true), candidate.Next(price, price + 1, price - 2, true));
        }
        Assert.Equal(1000, candidate.CertifiedCount); Assert.Equal(0, candidate.FallbackCount);
    }

    [Fact]
    public void AmbiguousPreviewReplaysOnlyCommittedBars()
    {
        var candidate = new TrueRangeAdjustedWindow(14, double.Epsilon);
        var exact = new TrueRangeAdjustedWindow(14, double.Epsilon, false);
        Same(exact.Next(1, 2, 0, true), candidate.Next(1, 2, 0, true));
        // The exact increment is smaller than the retained center precision.
        // Its sign is ambiguous in the enclosure, forcing replay on a preview.
        Same(exact.Next(2, 3, 1, false), candidate.Next(2, 3, 1, false));
        Assert.Equal(1, candidate.FallbackCount);
        for (var i = 0; i < 70; i++)
        {
            var price = 10 + i % 7;
            Same(exact.Next(price, price + 1, price - 2, true), candidate.Next(price, price + 1, price - 2, true));
        }
        // No preview, including extreme values, may enter the committed replay.
        foreach (var price in new[] { double.MaxValue, -double.MaxValue, double.Epsilon, 0d })
            Same(exact.Next(price, price, price, false), candidate.Next(price, price, price, false));
        Same(exact.Next(13, 14, 11, true), candidate.Next(13, 14, 11, true));
        candidate.Reset(); exact.Reset();
        Same(exact.Next(2, 3, 1, true), candidate.Next(2, 3, 1, true));
        Assert.Equal(1, candidate.CertifiedCount);
    }

    [Fact]
    public void InvalidPreviewDoesNotChangeState()
    {
        var candidate = new TrueRangeAdjustedWindow(14, 1.5);
        var exact = new TrueRangeAdjustedWindow(14, 1.5, false);
        Same(exact.Next(1, 2, 0, true), candidate.Next(1, 2, 0, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => candidate.Next(double.NaN, 2, 0, false));
        Same(exact.Next(2, 3, 1, true), candidate.Next(2, 3, 1, true));
    }
}
