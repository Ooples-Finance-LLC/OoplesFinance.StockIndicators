using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Enums;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class IndicatorInvokerTests
{
    [Fact]
    public void Every_batch_calculation_is_reachable_by_its_reviewed_indicator_name()
    {
        var supported = IndicatorInvoker.GetSupportedIndicators();
        var allIndicators = Enum.GetValues<IndicatorName>()
            .Where(x => x != IndicatorName.None)
            .ToArray();
        var excluded = allIndicators.Where(x => !supported.Contains(x)).ToArray();

        // The contract is that reflection reaches every indicator - stated here as the exclusion set
        // rather than as a literal count. A hardcoded count says the same thing while also failing
        // every time an indicator is legitimately added, which turns each new indicator into a
        // cross-PR merge conflict. Both assertions still fail if reflection silently drops one: it
        // lands in excluded and the count falls. VolatilityIndexDynamicAverageIndicator used to be
        // the one enum entry with no batch method; it now has one, so there is no exclusion left.
        Assert.Empty(excluded);
        Assert.Equal(allIndicators.Length, supported.Count);
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
