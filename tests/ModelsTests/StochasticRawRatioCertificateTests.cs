using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class StochasticRawRatioCertificateTests
{
    [Fact]
    public void OrdinaryExactRatioUsesCertificate()
    {
        Assert.True(ValuesBarExecution.TrySmoothedStochasticRawRatio(110, 90, 103.25, out double value));
        Assert.Equal(BitConverter.DoubleToInt64Bits(66.25), BitConverter.DoubleToInt64Bits(value));
    }

    [Theory]
    [InlineData(1)] [InlineData(-1)]
    public void MidpointTiesRequireExactFallback(int sign)
    {
        double close = sign * (1 + Math.ScaleB(1, -49));
        Assert.False(ValuesBarExecution.TrySmoothedStochasticRawRatio(4, 0, close, out _));
    }

    [Theory]
    [InlineData(double.MaxValue, -double.MaxValue, 1)]
    [InlineData(double.Epsilon, 0, double.Epsilon)]
    [InlineData(1, 1, 1)] [InlineData(0, 1, .5)]
    public void UncertifiedScalesAndNonpositiveRangesUseFallback(double high, double low, double close) =>
        Assert.False(ValuesBarExecution.TrySmoothedStochasticRawRatio(high, low, close, out _));

    [Fact]
    public void CertificatesMatchUnchangedRawOracleAcrossScalesAndAdjacentInputs()
    {
        var random = new Random(77139);
        var oracle = new FiftySeedStochastic.RawState(1, 0);
        int accepted = 0, rejected = 0;
        for (int i = 0; i < 10000; i++)
        {
            int exponent = i < 3000 ? 0 : random.Next(-1074, 1010);
            double low = Math.ScaleB(random.NextDouble() * 8 - 4, exponent);
            double high = Math.ScaleB(random.NextDouble() * 8 + 4, exponent);
            double close = i % 3 == 0 ? Math.BitIncrement(low)
                : Math.ScaleB(random.NextDouble() * 16 - 8, exponent);
            if (!double.IsFinite(high) || !double.IsFinite(low) || !double.IsFinite(close)) continue;
            if (!ValuesBarExecution.TrySmoothedStochasticRawRatio(high, low, close, out double actual))
            {
                rejected++;
                continue;
            }
            accepted++;
            oracle.Reset();
            var bar = new Bar(default, 0, high, low, close, 1);
            double expected = oracle.Next(in bar).Publish();
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
        }
        Assert.True(accepted > 1000);
        Assert.True(rejected > 1000);
    }
}
