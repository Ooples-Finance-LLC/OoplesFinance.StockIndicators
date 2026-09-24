using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class OvershootFormulaTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(14)]
    public void ReductionStaysBetweenMeanAndUnreducedRegression(int period)
    {
        var prices = Enumerable.Range(0, 300)
            .Select(i => 20 * Math.Sin(i * .31) + (i % 17 == 0 ? 90 : -3)).ToArray();
        using var state = new OvershootReductionMovingAverageState(length: period);
        for (var i = 0; i < prices.Length; i++)
        {
            var output = state.NextValue(prices[i], true);
            if (i + 1 < period) continue;
            var window = prices.Skip(i - period + 1).Take(period).ToArray();
            var mean = window.Average();
            var center = (period - 1d) / 2;
            var slope = window.Select((p, j) => p * (j - center)).Sum()
                / Enumerable.Range(0, period).Sum(j => (j - center) * (j - center));
            var unreduced = mean + slope * center;
            Assert.InRange(output, Math.Min(mean, unreduced) - 1e-9, Math.Max(mean, unreduced) + 1e-9);
        }
    }

    [Fact]
    public void SmoothedErrorGainMatchesHandVectorAcrossCorePreviewAndReset()
    {
        double[] prices = [1, 2, 4, 8, 3, 6];
        // Period four: first mature slope=23/10, mean=15/4, gain=1.
        // Next gain is still 1; final gain=(53/20)/(41/10)=53/82.
        double[] expected = [0, 0, 0, 36d / 5, 53d / 10, 8757d / 1640];
        var core = new double[prices.Length];
        MovingAverageCore.OvershootReductionMovingAverage(prices, core, 4);
        using var state = new OvershootReductionMovingAverageState(length: 4);
        for (var replay = 0; replay < 2; replay++)
        {
            for (var i = 0; i < prices.Length; i++)
            {
                state.NextValue(-17, false);
                Assert.Equal(expected[i], state.NextValue(prices[i], false), 11);
                Assert.Equal(expected[i], state.NextValue(prices[i], false), 11);
                Assert.Equal(expected[i], state.NextValue(prices[i], true), 11);
                Assert.Equal(expected[i], core[i], 11);
            }
            state.Reset();
        }
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new OvershootReductionMovingAverage(4))).Check(
            new IndicatorValidationContext("source-derived-hand-vector", bars, new[] { expected }, 0));
    }
}
