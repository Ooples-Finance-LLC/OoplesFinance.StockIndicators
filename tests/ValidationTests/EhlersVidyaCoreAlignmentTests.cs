using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Core.Registry;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class EhlersVidyaCoreAlignmentTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(14)]
    [InlineData(31)]
    [InlineData(int.MaxValue)]
    public void CoreRegistryAndSharedAverageUsePublicWeightedNineThirty(int legacyLength)
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 16 })
        {
            var prices = Enumerable.Range(0, 43)
                .Select(i => ((i * 7 % 19) - 9) * scale).ToArray();
            var bars = prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
            var expected = BuiltInFormulaReferences.EhlersVidyaOutputs(bars, 2, 9, 30)["Evidya"];
            var actual = Enumerable.Repeat(123d, prices.Length + 1).ToArray();
            var volume = Enumerable.Repeat(1d, prices.Length).ToArray();
            var registry = MovingAverageRegistry.GetRequired(MovingAvgType.EhlersVariableIndexDynamicAverage);

            void Check()
            {
                Assert.Equal(expected, actual.Take(prices.Length));
                Assert.Equal(123d, actual[^1]);
                Array.Fill(actual, 123d);
            }

            MovingAverageCore.EhlersVariableIndexDynamicAverage(prices, actual, legacyLength); Check();
            registry.Compute(prices, actual, legacyLength); Check();
            registry.Compute(prices, actual, legacyLength, new[] { 2d, 3d }); Check();
            registry.ComputeOhlc(prices, prices, prices, actual, legacyLength); Check();
            registry.ComputeOhlc(prices, prices, prices, actual, legacyLength, new[] { 2d, 3d }); Check();
            registry.ComputeWithVolume(prices, volume, actual, legacyLength); Check();

            StockData Data() => new(prices, prices, prices, prices, volume, bars.Select(b => b.Time));
            Assert.Equal(expected, Data().CalculateEhlersVariableIndexDynamicAverage().CustomValuesList);
            var data = Data();
            var originalValues = data.CustomValuesList;
            // The component helper has explicit fast/slow arguments, unlike the registry interface.
            Assert.Equal(expected, CalculationsHelper.GetMovingAverageList(data,
                MovingAvgType.EhlersVariableIndexDynamicAverage, legacyLength, fastLength: 9, slowLength: 30));
            Assert.Same(originalValues, data.CustomValuesList);
        }
    }

    [Fact]
    public void SpanBoundaryAndInPlaceEvaluationPreserveTheDefaultFormula()
    {
        var output = new[] { 123d };
        Assert.Throws<ArgumentException>(() => MovingAverageCore.EhlersVariableIndexDynamicAverage(new[] { 1d, 2d }, output));
        Assert.Equal(123d, output[0]);
        MovingAverageCore.EhlersVariableIndexDynamicAverage(Array.Empty<double>(), output);
        Assert.Equal(123d, output[0]);

        var prices = Enumerable.Range(0, 41).Select(i => (double)(i * 11 % 17 - 8)).ToArray();
        var bars = prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
        var expected = BuiltInFormulaReferences.EhlersVidyaOutputs(bars, 2, 9, 30)["Evidya"];
        MovingAverageCore.EhlersVariableIndexDynamicAverage(prices, prices);
        Assert.Equal(expected, prices);
    }
}
