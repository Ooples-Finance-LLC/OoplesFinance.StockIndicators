namespace OoplesFinance.StockIndicators.Tests.Unit.CalculationsTests;

public sealed class EhlersTests : GlobalTestData
{
    [Fact]
    public void CalculateEhlersMotherOfAdaptiveMovingAverages_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersMotherOfAdaptiveMovingAverages().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersRoofingFilterV2_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersRoofingFilterV2().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersPhaseCalculation_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersPhaseCalculation().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersAdaptiveCyberCycle_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersAdaptiveCyberCycle().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersSimpleDecycler_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersSimpleDecycler().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersHighPassFilterV1_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersHighPassFilterV1().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersRocketRelativeStrengthIndex_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersRocketRelativeStrengthIndex().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersCorrelationTrendIndicator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersCorrelationTrendIndicator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersRelativeVigorIndex_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersRelativeVigorIndex().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersCenterofGravityOscillator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersCenterofGravityOscillator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersAdaptiveCenterOfGravityOscillator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersAdaptiveCenterOfGravityOscillator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersSmoothedAdaptiveMomentum_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersSmoothedAdaptiveMomentum().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersStochasticCenterOfGravityOscillator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersStochasticCenterOfGravityOscillator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersSimpleCycleIndicator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersSimpleCycleIndicator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersDecyclerOscillatorV1_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersDecyclerOscillatorV1().OutputValues["FastEdo"];

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersHighPassFilterV2_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersHighPassFilterV2().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersDecyclerOscillatorV2_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersDecyclerOscillatorV2().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersModifiedStochasticIndicator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersModifiedStochasticIndicator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersModifiedRelativeStrengthIndex_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersModifiedRelativeStrengthIndex().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersHpLpRoofingFilter_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersHpLpRoofingFilter().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersDecycler_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersDecycler().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersZeroMeanRoofingFilter_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersZeroMeanRoofingFilter().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersRoofingFilterIndicator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersRoofingFilterIndicator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersHurstCoefficient_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersHurstCoefficient().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersReflexIndicator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersReflexIndicator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersSpectrumDerivedFilterBank_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersSpectrumDerivedFilterBank().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersDominantCycleTunedBypassFilter_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersDominantCycleTunedBypassFilter().OutputValues["V1"];

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersRestoringPullIndicator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersRestoringPullIndicator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersTrendflexIndicator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersTrendflexIndicator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersCorrelationCycleIndicator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersCorrelationCycleIndicator().OutputValues["Real"];

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersCorrelationAngleIndicator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersCorrelationAngleIndicator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersMarketStateIndicator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersMarketStateIndicator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersTrendExtraction_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersTrendExtraction().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersEmpiricalModeDecomposition_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersEmpiricalModeDecomposition().OutputValues["Trend"];

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersEarlyOnsetTrendIndicator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersEarlyOnsetTrendIndicator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersRoofingFilterV1_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersRoofingFilterV1().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersSnakeUniversalTradingFilter_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersSnakeUniversalTradingFilter().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersImpulseResponse_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersImpulseResponse().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersMesaPredictIndicatorV1_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersMesaPredictIndicatorV1().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersMesaPredictIndicatorV2_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersMesaPredictIndicatorV2().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersAnticipateIndicator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersAnticipateIndicator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersImpulseReaction_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersImpulseReaction().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersUniversalTradingFilter_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersUniversalTradingFilter().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersRecursiveMedianOscillator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersRecursiveMedianOscillator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersSuperPassbandFilter_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersSuperPassbandFilter().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersSimpleDerivIndicator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersSimpleDerivIndicator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersSimpleClipIndicator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersSimpleClipIndicator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersSpearmanRankIndicator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersSpearmanRankIndicator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersNoiseEliminationTechnology_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersNoiseEliminationTechnology().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersTruncatedBandPassFilter_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersTruncatedBandPassFilter().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersAutoCorrelationIndicator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersAutoCorrelationIndicator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersAutoCorrelationPeriodogram_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersAutoCorrelationPeriodogram().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersAdaptiveRelativeStrengthIndexV2_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersAdaptiveRelativeStrengthIndexV2().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersAdaptiveRsiFisherTransformV2_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersAdaptiveRsiFisherTransformV2().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersAdaptiveStochasticIndicatorV2_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersAdaptiveStochasticIndicatorV2().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersAdaptiveStochasticInverseFisherTransform_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersAdaptiveStochasticInverseFisherTransform().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersAdaptiveCommodityChannelIndexV2_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersAdaptiveCommodityChannelIndexV2().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersDiscreteFourierTransformSpectralEstimate_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersDiscreteFourierTransformSpectralEstimate().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersCombFilterSpectralEstimate_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersCombFilterSpectralEstimate().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersAutoCorrelationReversals_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersAutoCorrelationReversals().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersReverseExponentialMovingAverageIndicatorV1_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersReverseExponentialMovingAverageIndicatorV1().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersReverseExponentialMovingAverageIndicatorV2_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersReverseExponentialMovingAverageIndicatorV2().OutputValues["EremaCycle"];

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersMovingAverageDifferenceIndicator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersMovingAverageDifferenceIndicator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersFisherizedDeviationScaledOscillator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersFisherizedDeviationScaledOscillator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersHilbertTransformIndicator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersHilbertTransformIndicator().OutputValues["Inphase"];

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersInstantaneousPhaseIndicator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersInstantaneousPhaseIndicator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersSquelchIndicator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersSquelchIndicator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersClassicHilbertTransformer_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersClassicHilbertTransformer().OutputValues["Real"];

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersHilbertTransformer_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersHilbertTransformer().OutputValues["Real"];

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersHilbertTransformerIndicator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersHilbertTransformerIndicator().OutputValues["Real"];

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersDualDifferentiatorDominantCycle_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersDualDifferentiatorDominantCycle().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersPhaseAccumulationDominantCycle_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersPhaseAccumulationDominantCycle().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersHomodyneDominantCycle_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersHomodyneDominantCycle().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersSignalToNoiseRatioV1_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersSignalToNoiseRatioV1().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersHannWindowIndicator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersHannWindowIndicator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersHammingWindowIndicator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersHammingWindowIndicator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersTriangleWindowIndicator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersTriangleWindowIndicator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersSimpleWindowIndicator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersSimpleWindowIndicator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersSignalToNoiseRatioV2_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersSignalToNoiseRatioV2().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersEnhancedSignalToNoiseRatio_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersEnhancedSignalToNoiseRatio().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersHilbertOscillator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersHilbertOscillator().OutputValues["IQ"];

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersAlternateSignalToNoiseRatio_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersAlternateSignalToNoiseRatio().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersDiscreteFourierTransform_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersDiscreteFourierTransform().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersFourierSeriesAnalysis_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersFourierSeriesAnalysis().OutputValues["Wave"];

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersVossPredictiveFilter_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersVossPredictiveFilter().OutputValues["Voss"];

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersSwissArmyKnifeIndicator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersSwissArmyKnifeIndicator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersUniversalOscillator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersUniversalOscillator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersDetrendedLeadingIndicator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersDetrendedLeadingIndicator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersBandPassFilterV1_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersBandPassFilterV1().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersBandPassFilterV2_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersBandPassFilterV2().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersCycleBandPassFilter_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersCycleBandPassFilter().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersCycleAmplitude_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersCycleAmplitude().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersZeroCrossingsDominantCycle_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersZeroCrossingsDominantCycle().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersAdaptiveBandPassFilter_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersAdaptiveBandPassFilter().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersCyberCycle_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersCyberCycle().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersStochasticCyberCycle_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersStochasticCyberCycle().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersFMDemodulatorIndicator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersFMDemodulatorIndicator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersStochastic_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersStochastic().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersTripleDelayLineDetrender_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersTripleDelayLineDetrender().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersAMDetector_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersAMDetector().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersSineWaveIndicatorV1_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersSineWaveIndicatorV1().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersSineWaveIndicatorV2_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersSineWaveIndicatorV2().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersEvenBetterSineWaveIndicator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersEvenBetterSineWaveIndicator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersConvolutionIndicator_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersConvolutionIndicator().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersFisherTransform_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersFisherTransform().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersInverseFisherTransform_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersInverseFisherTransform().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersInstantaneousTrendlineV2_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersInstantaneousTrendlineV2().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersInstantaneousTrendlineV1_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersInstantaneousTrendlineV1().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersLaguerreRelativeStrengthIndex_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersLaguerreRelativeStrengthIndex().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlpha_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlpha().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersAdaptiveRelativeStrengthIndexV1_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersAdaptiveRelativeStrengthIndexV1().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersAdaptiveRsiFisherTransformV1_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersAdaptiveRsiFisherTransformV1().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersAdaptiveStochasticIndicatorV1_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersAdaptiveStochasticIndicatorV1().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersAdaptiveCommodityChannelIndexV1_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersAdaptiveCommodityChannelIndexV1().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersCommodityChannelIndexInverseFisherTransform_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersCommodityChannelIndexInverseFisherTransform().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }

    [Fact]
    public void CalculateEhlersRelativeStrengthIndexInverseFisherTransform_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersRelativeStrengthIndexInverseFisherTransform().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }
}
