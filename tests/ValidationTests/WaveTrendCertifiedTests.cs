using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Builder.Compute;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class WaveTrendCertifiedTests
{
    private static void Same((double Line, double SignalLine, Signal Trade) expected,
        (double Line, double SignalLine, Signal Trade) actual)
    {
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Line), BitConverter.DoubleToInt64Bits(actual.Line));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.SignalLine), BitConverter.DoubleToInt64Bits(actual.SignalLine));
        Assert.Equal(expected.Trade, actual.Trade);
    }

    private static Number Price(int index)
    {
        var price = 10 + (index * 13 % 101) / 8d;
        return WaveTrendWindow.Price(price, price + 1, price - 2, price);
    }

    [Theory]
    [InlineData(MovingAvgType.ExponentialMovingAverage, 10, 21, 4)]
    [InlineData(MovingAvgType.ExponentialMovingAverage, 3, 4, 2)]
    [InlineData(MovingAvgType.WildersSmoothingMethod, 10, 21, 4)]
    [InlineData(MovingAvgType.WildersSmoothingMethod, 3, 4, 2)]
    public void VaryingHistoryPreservesEveryOutputBitAndSignal(MovingAvgType kind, int channel, int average, int signal)
    {
        using var candidate = new WaveTrendWindow(kind, channel, average, signal);
        using var exact = new WaveTrendWindow(kind, channel, average, signal, false);
        for (var i = 0; i < 256; i++)
        {
            if (i % 19 == 0)
                Same(exact.Next(Price(i + 7), false), candidate.Next(Price(i + 7), false));
            Same(exact.Next(Price(i), true), candidate.Next(Price(i), true));
        }
        Assert.Equal(256, candidate.CertifiedCount);
        Assert.Equal(0, candidate.FallbackCount);
        candidate.Reset(); exact.Reset();
        for (var i = 0; i < 20; i++) Same(exact.Next(Price(i), true), candidate.Next(Price(i), true));
        Assert.Equal(20, candidate.CertifiedCount);
    }

    [Theory]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WildersSmoothingMethod)]
    public void TenThousandVaryingBarsRemainCertified(MovingAvgType kind)
    {
        using var candidate = new WaveTrendWindow(kind, 10, 21, 4);
        for (var i = 0; i < 10000; i++)
        {
            var value = candidate.Next(Price(i), true);
            Assert.Equal(0, candidate.FallbackCount);
            Assert.True(double.IsFinite(value.Line));
            Assert.True(double.IsFinite(value.SignalLine));
        }
        Assert.Equal(10000, candidate.CertifiedCount);
        Assert.Equal(0, candidate.FallbackCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(double.MaxValue)]
    [InlineData(double.Epsilon)]
    public void ConstantEmaPrefixesRetainExactZeroAndReset(double price)
    {
        using var candidate = new WaveTrendWindow(MovingAvgType.ExponentialMovingAverage, 10, 21, 4);
        using var exact = new WaveTrendWindow(MovingAvgType.ExponentialMovingAverage, 10, 21, 4, false);
        // HLC / 3 can be a non-dyadic rational even with finite inputs.
        var input = WaveTrendWindow.Price(price, price, 0);
        for (var i = 0; i < 1000; i++) Same(exact.Next(input, true), candidate.Next(input, true));
        Assert.Equal(1000, candidate.CertifiedCount);
        Assert.Equal(0, candidate.FallbackCount);
        candidate.Reset(); exact.Reset();
        Same(exact.Next(Number.Of(2), true), candidate.Next(Number.Of(2), true));
    }

    [Fact]
    public void AmbiguousPreviewReplaysOnlyCommittedInputs()
    {
        using var candidate = new WaveTrendWindow(MovingAvgType.ExponentialMovingAverage, 3, 4, 2);
        using var exact = new WaveTrendWindow(MovingAvgType.ExponentialMovingAverage, 3, 4, 2, false);
        for (var i = 0; i < 8; i++) Same(exact.Next(Number.Of(1), true), candidate.Next(Number.Of(1), true));
        var ambiguous = Number.Of(1) + Number.Of(double.Epsilon);
        Same(exact.Next(ambiguous, false), candidate.Next(ambiguous, false));
        Assert.Equal(1, candidate.FallbackCount);
        for (var i = 0; i < 40; i++) Same(exact.Next(Price(i), true), candidate.Next(Price(i), true));
        candidate.Reset(); exact.Reset();
        Same(exact.Next(Price(0), true), candidate.Next(Price(0), true));
        Assert.Equal(1, candidate.CertifiedCount);
        Assert.Equal(0, candidate.FallbackCount);
    }

    [Theory]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WildersSmoothingMethod)]
    public void ExtremeInputsPreserveBitsSignalsAndPreview(MovingAvgType kind)
    {
        foreach (var scale in new[] { double.Epsilon, double.MaxValue / 16 })
        {
            using var candidate = new WaveTrendWindow(kind, 3, 4, 2);
            using var exact = new WaveTrendWindow(kind, 3, 4, 2, false);
            for (var i = 0; i < 24; i++)
            {
                var price = Number.Of((i * 7 % 13 - 6) * scale);
                Same(exact.Next(price, false), candidate.Next(price, false));
                Same(exact.Next(price, true), candidate.Next(price, true));
            }
        }
    }

    [Theory]
    [InlineData(true, 4)]
    [InlineData(false, 3)]
    public void FastBatchPreservesUnsubstitutedAverageRequests(bool includeSignal, int requests)
    {
        var prices = new[] { 1d, 3, 2, 4 };
        var data = new StockData(prices, prices, prices, prices, prices.Select(_ => 1d),
            prices.Select((_, i) => DateTime.UnixEpoch.AddMinutes(i)));
        using var scope = ComponentAverage.Arm(Array.Empty<Func<IReadOnlyList<double>, int, IReadOnlyList<double>>>());
        _ = WaveTrendWindow.Calculate(data, MovingAvgType.ExponentialMovingAverage, 3, 4, 2, true, includeSignal);
        Assert.Equal(requests, ComponentAverage.Requests);
        Assert.Equal(0, ComponentAverage.Substitutions);
        Assert.True(ComponentAverage.MixedLengths);
        Assert.Equal(includeSignal ? 2 : 4, ComponentAverage.LengthAsked);
    }
}
