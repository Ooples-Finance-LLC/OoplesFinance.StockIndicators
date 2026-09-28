using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class CompositeStageParityTests
{
    [Theory]
    [InlineData(MovingAvgType.SimpleMovingAverage)]
    [InlineData(MovingAvgType.WeightedMovingAverage)]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WildersSmoothingMethod)]
    public void FastCompositesRetainTheirComponentsStageRounding(MovingAvgType kind)
    {
        var values = Enumerable.Range(0, 90).Select(i => 100 + Math.Sin(i * .31) * 7 + (i % 5) * .1).ToArray();
        StockData Data(bool selected)
        {
            var prices = selected ? values.Select(v => v + 30).ToArray() : values;
            var data = new StockData(prices, prices.Select(v => v + 2), prices.Select(v => v - 1), prices, prices.Select(_ => 1d), prices.Select((_, i) => DateTime.UnixEpoch.AddMinutes(i)));
            if (selected) data.SetCustomValues(values.ToList()); return data;
        }
        foreach (var selected in new[] { false, true }) foreach (var length in new[] { 3, 7, 34 })
        {
            using var context = new ComputeContext();
            var quadratic = Data(selected).CalculateLinearQuadraticConvergenceDivergenceOscillator(kind, length, 4).OutputValues["Lqcdo"];
            using var actualQuadratic = IndicatorCompute.ComputeLinearQuadraticConvergenceDivergenceOscillatorFast(Data(selected), context, length, 4, kind);
            Assert.Equal(quadratic, actualQuadratic.ToArray());
            var dynamic = Data(selected).CalculateTradersDynamicIndex(kind, length, 9, 2, 4).OutputValues;
            foreach (var series in new[] { IndicatorCompute.TradersDynamicSeries.UpperBand, IndicatorCompute.TradersDynamicSeries.MiddleBand, IndicatorCompute.TradersDynamicSeries.LowerBand })
            {
                using var actual = IndicatorCompute.ComputeTradersDynamicIndexFast(Data(selected), context, length, 2, kind, 9, 4, series);
                Assert.Equal(dynamic[series.ToString()], actual.ToArray());
            }
        }
    }
}
