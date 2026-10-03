using OoplesFinance.StockIndicators.Core.Registry;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class PolynomialComponentNumericalTests
{
    [Fact]
    public void EveryComponentEntryPointUsesThePublishedCellIntegral()
    {
        var core = MovingAverageRegistry.Get(MovingAvgType.PolynomialLeastSquaresMovingAverage)!;
        foreach (var length in new[] { 1, 2, 3, 7, 100 })
        foreach (var prices in new[] { Array.Empty<double>(), new[] { 1d, 3, 2, -4, 0, 0 },
            new[] { double.MaxValue, -double.MaxValue, double.MaxValue / 2, 0, 0 },
            new[] { double.Epsilon, double.Epsilon, -double.Epsilon, 0, 0 } })
        {
            var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
            var expected = BuiltInFormulaReferences.PolynomialCellOutputs(bars, new PolynomialLeastSquaresMovingAverage(length))["Plsma"];
            var output = new double[prices.Length];
            core.Compute(prices, output, length); Assert.Equal(expected, output);
            core.Compute(prices, output, length, new[] { 999d }); Assert.Equal(expected, output);
            core.ComputeOhlc(prices, prices, prices, output, length); Assert.Equal(expected, output);
            core.ComputeOhlc(prices, prices, prices, output, length, new[] { 999d }); Assert.Equal(expected, output);
            core.ComputeWithVolume(prices, prices, output, length); Assert.Equal(expected, output);
        }
        Assert.Throws<ArgumentException>(() => core.Compute(new[] { 1d }, Array.Empty<double>(), 3));
    }
}
