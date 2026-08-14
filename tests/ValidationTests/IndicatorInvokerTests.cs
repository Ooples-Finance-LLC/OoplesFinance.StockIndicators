using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Enums;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class IndicatorInvokerTests
{
    [Fact]
    public void Every_batch_calculation_is_reachable_by_its_reviewed_indicator_name()
    {
        var supported = IndicatorInvoker.GetSupportedIndicators();
        var excluded = Enum.GetValues<IndicatorName>()
            .Where(x => x != IndicatorName.None && !supported.Contains(x))
            .ToArray();

        Assert.Equal(773, supported.Count);
        Assert.Equal([IndicatorName.VolatilityIndexDynamicAverageIndicator], excluded);
    }

    [Theory]
    [InlineData(IndicatorName._1LCLeastSquaresMovingAverage, "Calculate1LCLeastSquaresMovingAverage")]
    [InlineData(IndicatorName._3HMA, "Calculate3HMA")]
    [InlineData(IndicatorName._4MovingAverageConvergenceDivergence, "Calculate4MovingAverageConvergenceDivergence")]
    [InlineData(IndicatorName._4PercentagePriceOscillator, "Calculate4PercentagePriceOscillator")]
    [InlineData(IndicatorName.BollingerBandsAverageTrueRange, "CalculateBollingerBandsAvgTrueRange")]
    [InlineData(IndicatorName.CCTStochRelativeStrengthIndex, "CalculateCCTStochRSI")]
    [InlineData(IndicatorName.EhlersSmoothedAdaptiveMomentumIndicator, "CalculateEhlersSmoothedAdaptiveMomentum")]
    [InlineData(IndicatorName.StandardDeviation, "CalculateStandardDevation")]
    [InlineData(IndicatorName.ZDistanceFromVwap, "CalculateZDistanceFromVwapIndicator")]
    public void Nonstandard_batch_method_names_are_mapped(IndicatorName indicator, string expectedMethod)
    {
        Assert.True(IndicatorInvoker.IsSupported(indicator));
        Assert.Equal(expectedMethod, IndicatorInvoker.GetMethod(indicator)?.Name);
    }
}
