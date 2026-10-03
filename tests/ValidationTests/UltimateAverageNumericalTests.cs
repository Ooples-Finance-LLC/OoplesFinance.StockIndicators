using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class UltimateAverageNumericalTests
{
    private static StockData Data(double[] prices) => new(prices, prices, prices, prices, prices.Select(_ => 0d),
        prices.Select((_, i) => DateTime.UnixEpoch.AddMinutes(i)));
    private static OhlcvBar Bar(double price, int i) => new("UMA", BarTimeframe.Minutes(1), DateTime.UnixEpoch.AddMinutes(i),
        DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 0, true);

    [Fact]
    public void PublicRoutesMatchFifthPowerWarmupHand()
    {
        var prices = new[] { 1d, 2, 3, 4 };
        // MFI=100 at zero volume, so acc=1 gives p=5. Missing history is zero,
        // but the full denominator 3^5+2^5+1^5=276 applies from the first bar.
        var expected = new[] { 243d / 276, 518d / 276, 794d / 276, 1070d / 276 };
        var batch = Data(prices).CalculateUltimateMovingAverage(minLength: 3, maxLength: 3);
        Assert.Equal(expected, batch.CustomValuesList);
        using var context = new ComputeContext();
        using var fast = IndicatorCompute.ComputeUltimateMovingAverageFast(Data(prices), context, 3, 3);
        Assert.Equal(expected, fast.ToArray());
        using var state = new UltimateMovingAverageState(minLength: 3, maxLength: 3);
        for (var i = 0; i < prices.Length; i++)
        {
            var preview = state.Update(Bar(prices[i], i), false, true);
            Assert.Equal(expected[i], preview.Value); Assert.Equal(expected[i], preview.Outputs!["Uma"]);
            Assert.Equal(preview.Value, state.Update(Bar(prices[i], i), false, false).Value);
            Assert.Equal(expected[i], state.Update(Bar(prices[i], i), true, false).Value);
        }
        state.Reset(); Assert.Equal(expected[0], state.Update(Bar(1, 0), true, false).Value);
    }

    [Fact]
    public void StatePreservesSubnormalsAndSupportsHugePeriodsWithoutEagerHistory()
    {
        using var one = new UltimateMovingAverageState(minLength: 1, maxLength: 1);
        Assert.Equal(double.Epsilon, one.Update(Bar(double.Epsilon, 0), true, false).Value);
        Assert.Equal(double.MaxValue, one.Update(Bar(double.MaxValue, 1), true, false).Value);
        using var huge = new UltimateAverageWindow(MovingAvgType.SimpleMovingAverage, int.MaxValue, int.MaxValue, -4);
        // acc=-4 and MFI=100 gives uniform weights, including zero warmup.
        Assert.Equal(1d / int.MaxValue, huge.Next(1, 1, 1, 0, true));
        Assert.Equal(3d / int.MaxValue, huge.Next(2, 2, 2, 0, true));
    }

    [Fact]
    public void InvalidInputsDoNotAdvanceStreamingState()
    {
        using var state = new UltimateMovingAverageState(minLength: 3, maxLength: 3);
        Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Bar(double.NaN, 0), true, false));
        Assert.Equal(243d / 276, state.Update(Bar(1, 0), true, false).Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => new UltimateMovingAverageState(acc: double.PositiveInfinity));
    }

    [Fact]
    public void InvalidOpenCannotAdvanceEitherNativeState()
    {
        foreach (var bands in new[] { false, true })
        {
            using var average = new UltimateMovingAverageState(minLength: 3, maxLength: 3);
            using var channel = new UltimateMovingAverageBandsState(minLength: 3, maxLength: 3);
            IStreamingIndicatorState state = bands ? channel : average;
            state.Update(Bar(1, 0), true, false);
            var bad = new OhlcvBar("UMA", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch,
                double.NaN, 2, 2, 2, 0, true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, true, false));
            Assert.Equal(761d / 276, state.Update(Bar(3, 1), true, false).Value);
        }
    }

    [Fact]
    public void BandsMatchIndependentRationalWarmupAndWidthHands()
    {
        var prices = new[] { 1d, 3 };
        var data = Data(prices).CalculateUltimateMovingAverageBands(minLength: 2, maxLength: 2);
        Assert.Equal(new[] { 32d / 33, 97d / 33 }, data.OutputValues["MiddleBand"]);
        Assert.Equal(new[] { 32d / 33, 163d / 33 }, data.OutputValues["UpperBand"]);
        Assert.Equal(new[] { 32d / 33, 31d / 33 }, data.OutputValues["LowerBand"]);
        using var state = new UltimateMovingAverageBandsState(minLength: 2, maxLength: 2);
        for (var i = 0; i < prices.Length; i++)
        {
            var preview = state.Update(Bar(prices[i], i), false, true);
            var final = state.Update(Bar(prices[i], i), true, true);
            foreach (var name in new[] { "UpperBand", "MiddleBand", "LowerBand" })
            { Assert.Equal(data.OutputValues[name][i], preview.Outputs![name]); Assert.Equal(preview.Outputs[name], final.Outputs![name]); }
        }
        using var context = new ComputeContext();
        foreach (var band in new[] { IndicatorCompute.ChannelBand.Upper, IndicatorCompute.ChannelBand.Middle, IndicatorCompute.ChannelBand.Lower })
        {
            using var result = IndicatorCompute.ComputeUltimateMovingAverageBandsFast(Data(prices), context, 2, 2, 2, band: band);
            var name = band == IndicatorCompute.ChannelBand.Upper ? "UpperBand" : band == IndicatorCompute.ChannelBand.Lower ? "LowerBand" : "MiddleBand";
            Assert.Equal(data.OutputValues[name], result.ToArray());
        }
    }

    [Fact]
    public void BandsPreserveCancellationNeighborsAndSubnormalDeviation()
    {
        foreach (var last in new[] { Math.BitDecrement(34d), 34d, Math.BitIncrement(34d) })
        {
            // With prices [1,x], p=5, sigma=(x-1)/2 and multiplier=2,
            // lower=(32*x+1)/33-(x-1)=(34-x)/33. Sterbenz makes 34-x exact.
            var expected = (34 - last) / 33;
            var batch = Data(new[] { 1d, last }).CalculateUltimateMovingAverageBands(minLength: 2, maxLength: 2);
            Assert.Equal(expected, batch.OutputValues["LowerBand"][1]);
            using var native = new UltimateMovingAverageBandsState(minLength: 2, maxLength: 2);
            native.Update(Bar(1, 0), true, false);
            Assert.Equal(expected, native.Update(Bar(last, 1), true, true).Outputs!["LowerBand"]);
        }
        var tiny = Data(new[] { 0d, double.Epsilon }).CalculateUltimateMovingAverageBands(minLength: 2, maxLength: 2);
        Assert.Equal(2 * double.Epsilon, tiny.OutputValues["UpperBand"][1]);
        Assert.Equal(0, tiny.OutputValues["LowerBand"][1]);
    }
}
