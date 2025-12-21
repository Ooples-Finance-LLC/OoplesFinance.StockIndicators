//     Ooples Finance Stock Indicator Library
//     https://ooples.github.io/OoplesFinance.StockIndicators/
//
//     Copyright � Franklin Moormann, 2020-2022
//     cheatcountry@gmail.com
//
//     This library is free software and it uses the Apache 2.0 license
//     so if you are going to re-use or modify my code then I just ask
//     that you include my copyright info and my contact info in a comment

namespace OoplesFinance.StockIndicators.Helpers;

public static class CalculationsHelper
{
    /// <summary>
    /// Calculates the user chosen moving average with user's custom settings
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="movingAvgType"></param>
    /// <param name="length"></param>
    /// <param name="customValuesList"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    public static List<double> GetMovingAverageList(StockData stockData, MovingAvgType movingAvgType, int length, List<double>? customValuesList = null, 
        int? fastLength = null, int? slowLength = null)
    {
        List<double> movingAvgList = new();

        if (customValuesList != null)
        {
            stockData.CustomValuesList = customValuesList;
        }

        switch (movingAvgType)
        {
            case MovingAvgType._1LCLeastSquaresMovingAverage:
                movingAvgList = stockData.Calculate1LCLeastSquaresMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType._3HMA:
                movingAvgList = stockData.Calculate3HMA(length: length).CustomValuesList;
                break;
            case MovingAvgType.AdaptiveAutonomousRecursiveMovingAverage:
                movingAvgList = stockData.CalculateAdaptiveAutonomousRecursiveMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.AdaptiveExponentialMovingAverage:
                movingAvgList = stockData.CalculateAdaptiveExponentialMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.AdaptiveLeastSquares:
                movingAvgList = stockData.CalculateAdaptiveLeastSquares(length: length).CustomValuesList;
                break;
            case MovingAvgType.AdaptiveMovingAverage:
                movingAvgList = stockData.CalculateAdaptiveMovingAverage(fastLength ?? default, slowLength ?? length, length).CustomValuesList;
                break;
            case MovingAvgType.AhrensMovingAverage:
                movingAvgList = stockData.CalculateAhrensMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.AlphaDecreasingExponentialMovingAverage:
                movingAvgList = stockData.CalculateAlphaDecreasingExponentialMovingAverage().CustomValuesList;
                break;
            case MovingAvgType.ArnaudLegouxMovingAverage:
                movingAvgList = stockData.CalculateArnaudLegouxMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.AtrFilteredExponentialMovingAverage:
                movingAvgList = stockData.CalculateAtrFilteredExponentialMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.AutoFilter:
                movingAvgList = stockData.CalculateAutoFilter(length: length).CustomValuesList;
                break;
            case MovingAvgType.AutonomousRecursiveMovingAverage:
                movingAvgList = stockData.CalculateAutonomousRecursiveMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.BryantAdaptiveMovingAverage:
                movingAvgList = stockData.CalculateBryantAdaptiveMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.CompoundRatioMovingAverage:
                movingAvgList = stockData.CalculateCompoundRatioMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.CorrectedMovingAverage:
                movingAvgList = stockData.CalculateCorrectedMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.CubedWeightedMovingAverage:
                movingAvgList = stockData.CalculateCubedWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.DampedSineWaveWeightedFilter:
                movingAvgList = stockData.CalculateDampedSineWaveWeightedFilter(length).CustomValuesList;
                break;
            case MovingAvgType.DistanceWeightedMovingAverage:
                movingAvgList = stockData.CalculateDistanceWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.DoubleExponentialMovingAverage:
                movingAvgList = stockData.CalculateDoubleExponentialMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.DoubleExponentialSmoothing:
                movingAvgList = stockData.CalculateDoubleExponentialSmoothing().CustomValuesList;
                break;
            case MovingAvgType.DynamicallyAdjustableFilter:
                movingAvgList = stockData.CalculateDynamicallyAdjustableFilter(length).CustomValuesList;
                break;
            case MovingAvgType.DynamicallyAdjustableMovingAverage:
                movingAvgList = stockData.CalculateDynamicallyAdjustableMovingAverage(fastLength ?? default, slowLength ?? length).CustomValuesList;
                break;
            case MovingAvgType.EdgePreservingFilter:
                movingAvgList = stockData.CalculateEdgePreservingFilter(length: length).CustomValuesList;
                break;
            case MovingAvgType.Ehlers2PoleButterworthFilterV1:
                movingAvgList = stockData.CalculateEhlers2PoleButterworthFilterV1(length).CustomValuesList;
                break;
            case MovingAvgType.Ehlers2PoleButterworthFilterV2:
                movingAvgList = stockData.CalculateEhlers2PoleButterworthFilterV2(length).CustomValuesList;
                break;
            case MovingAvgType.Ehlers2PoleSuperSmootherFilterV1:
                movingAvgList = stockData.CalculateEhlers2PoleSuperSmootherFilterV1(length).CustomValuesList;
                break;
            case MovingAvgType.Ehlers2PoleSuperSmootherFilterV2:
                movingAvgList = stockData.CalculateEhlers2PoleSuperSmootherFilterV2(length).CustomValuesList;
                break;
            case MovingAvgType.Ehlers3PoleButterworthFilterV1:
                movingAvgList = stockData.CalculateEhlers3PoleButterworthFilterV1(length).CustomValuesList;
                break;
            case MovingAvgType.Ehlers3PoleButterworthFilterV2:
                movingAvgList = stockData.CalculateEhlers3PoleButterworthFilterV2(length).CustomValuesList;
                break;
            case MovingAvgType.Ehlers3PoleSuperSmootherFilter:
                movingAvgList = stockData.CalculateEhlers3PoleSuperSmootherFilter(length).CustomValuesList;
                break;
            case MovingAvgType.EhlersAdaptiveLaguerreFilter:
                movingAvgList = stockData.CalculateEhlersAdaptiveLaguerreFilter(slowLength ?? length, fastLength ?? default).CustomValuesList;
                break;
            case MovingAvgType.EhlersAllPassPhaseShifter:
                movingAvgList = stockData.CalculateEhlersAllPassPhaseShifter(length: length).CustomValuesList;
                break;
            case MovingAvgType.EhlersAverageErrorFilter:
                movingAvgList = stockData.CalculateEhlersAverageErrorFilter(length).CustomValuesList;
                break;
            case MovingAvgType.EhlersBetterExponentialMovingAverage:
                movingAvgList = stockData.CalculateEhlersBetterExponentialMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.EhlersChebyshevLowPassFilter:
                movingAvgList = stockData.CalculateEhlersChebyshevLowPassFilter().CustomValuesList;
                break;
            case MovingAvgType.EhlersDeviationScaledMovingAverage:
                movingAvgList = stockData.CalculateEhlersDeviationScaledMovingAverage(
                    fastLength: fastLength ?? length, slowLength: slowLength ?? length * 2).CustomValuesList;
                break;
            case MovingAvgType.EhlersDeviationScaledSuperSmoother:
                movingAvgList = stockData.CalculateEhlersDeviationScaledSuperSmoother(length1: fastLength ?? length, length2: slowLength ?? default).CustomValuesList;
                break;
            case MovingAvgType.EhlersDistanceCoefficientFilter:
                movingAvgList = stockData.CalculateEhlersDistanceCoefficientFilter(length).CustomValuesList;
                break;
            case MovingAvgType.EhlersFilter:
                movingAvgList = stockData.CalculateEhlersFilter(slowLength ?? length, fastLength ?? default).CustomValuesList;
                break;
            case MovingAvgType.EhlersFiniteImpulseResponseFilter:
                movingAvgList = stockData.CalculateEhlersFiniteImpulseResponseFilter().CustomValuesList;
                break;
            case MovingAvgType.EhlersFractalAdaptiveMovingAverage:
                movingAvgList = stockData.CalculateEhlersFractalAdaptiveMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.EhlersGaussianFilter:
                movingAvgList = stockData.CalculateEhlersGaussianFilter(length).CustomValuesList;
                break;
            case MovingAvgType.EhlersHammingMovingAverage:
                movingAvgList = stockData.CalculateEhlersHammingMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.EhlersHannMovingAverage:
                movingAvgList = stockData.CalculateEhlersHannMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.EhlersInfiniteImpulseResponseFilter:
                movingAvgList = stockData.CalculateEhlersInfiniteImpulseResponseFilter(length).CustomValuesList;
                break;
            case MovingAvgType.EhlersKaufmanAdaptiveMovingAverage:
                movingAvgList = stockData.CalculateEhlersKaufmanAdaptiveMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.EhlersLaguerreFilter:
                movingAvgList = stockData.CalculateEhlersLaguerreFilter().CustomValuesList;
                break;
            case MovingAvgType.EhlersLeadingIndicator:
                movingAvgList = stockData.CalculateEhlersLeadingIndicator().CustomValuesList;
                break;
            case MovingAvgType.EhlersMedianAverageAdaptiveFilter:
                movingAvgList = stockData.CalculateEhlersMedianAverageAdaptiveFilter(length: length).CustomValuesList;
                break;
            case MovingAvgType.EhlersMesaAdaptiveMovingAverage:
                movingAvgList = stockData.CalculateEhlersMotherOfAdaptiveMovingAverages().CustomValuesList;
                break;
            case MovingAvgType.EhlersModifiedOptimumEllipticFilter:
                movingAvgList = stockData.CalculateEhlersModifiedOptimumEllipticFilter().CustomValuesList;
                break;
            case MovingAvgType.EhlersOptimumEllipticFilter:
                movingAvgList = stockData.CalculateEhlersOptimumEllipticFilter().CustomValuesList;
                break;
            case MovingAvgType.EhlersRecursiveMedianFilter:
                movingAvgList = stockData.CalculateEhlersRecursiveMedianFilter(fastLength ?? default, slowLength ?? length).CustomValuesList;
                break;
            case MovingAvgType.EhlersSuperSmootherFilter:
                movingAvgList = stockData.CalculateEhlersSuperSmootherFilter(length).CustomValuesList;
                break;
            case MovingAvgType.EhlersTriangleMovingAverage:
                movingAvgList = stockData.CalculateEhlersTriangleMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.EhlersVariableIndexDynamicAverage:
                movingAvgList = stockData.CalculateEhlersVariableIndexDynamicAverage(fastLength: fastLength ?? default, slowLength: slowLength ?? length)
                    .CustomValuesList;
                break;
            case MovingAvgType.EhlersZeroLagExponentialMovingAverage:
                movingAvgList = stockData.CalculateEhlersZeroLagExponentialMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.ElasticVolumeWeightedMovingAverageV1:
                movingAvgList = stockData.CalculateElasticVolumeWeightedMovingAverageV1(length: length).CustomValuesList;
                break;
            case MovingAvgType.ElasticVolumeWeightedMovingAverageV2:
                movingAvgList = stockData.CalculateElasticVolumeWeightedMovingAverageV2(length).CustomValuesList;
                break;
            case MovingAvgType.EndPointWeightedMovingAverage:
                movingAvgList = stockData.CalculateEndPointMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.EquityMovingAverage:
                movingAvgList = stockData.CalculateEquityMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.ExponentialMovingAverage:
                movingAvgList = stockData.CalculateExponentialMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.FallingRisingFilter:
                movingAvgList = stockData.CalculateFallingRisingFilter(length).CustomValuesList;
                break;
            case MovingAvgType.FareySequenceWeightedMovingAverage:
                movingAvgList = stockData.CalculateFareySequenceWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.FibonacciWeightedMovingAverage:
                movingAvgList = stockData.CalculateFibonacciWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.FisherLeastSquaresMovingAverage:
                movingAvgList = stockData.CalculateFisherLeastSquaresMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.FollowingAdaptiveMovingAverage:
                movingAvgList = stockData.CalculateEhlersMotherOfAdaptiveMovingAverages().OutputValues["Fama"];
                break;
            case MovingAvgType.GeneralFilterEstimator:
                movingAvgList = stockData.CalculateGeneralFilterEstimator(length: length).CustomValuesList;
                break;
            case MovingAvgType.GeneralizedDoubleExponentialMovingAverage:
                movingAvgList = stockData.CalculateGeneralizedDoubleExponentialMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.HampelFilter:
                movingAvgList = stockData.CalculateHampelFilter(length: length).CustomValuesList;
                break;
            case MovingAvgType.HendersonWeightedMovingAverage:
                movingAvgList = stockData.CalculateHendersonWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.HoltExponentialMovingAverage:
                movingAvgList = stockData.CalculateHoltExponentialMovingAverage(length, length).CustomValuesList;
                break;
            case MovingAvgType.HullEstimate:
                movingAvgList = stockData.CalculateHullEstimate(length).CustomValuesList;
                break;
            case MovingAvgType.HullMovingAverage:
                movingAvgList = stockData.CalculateHullMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.HybridConvolutionFilter:
                movingAvgList = stockData.CalculateHybridConvolutionFilter(length).CustomValuesList;
                break;
            case MovingAvgType.IIRLeastSquaresEstimate:
                movingAvgList = stockData.CalculateIIRLeastSquaresEstimate(length).CustomValuesList;
                break;
            case MovingAvgType.InverseDistanceWeightedMovingAverage:
                movingAvgList = stockData.CalculateInverseDistanceWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.JsaMovingAverage:
                movingAvgList = stockData.CalculateJsaMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.JurikMovingAverage:
                movingAvgList = stockData.CalculateJurikMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.KalmanSmoother:
                movingAvgList = stockData.CalculateKalmanSmoother(length).CustomValuesList;
                break;
            case MovingAvgType.KaufmanAdaptiveLeastSquaresMovingAverage:
                movingAvgList = stockData.CalculateKaufmanAdaptiveLeastSquaresMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.KaufmanAdaptiveMovingAverage:
                movingAvgList = stockData.CalculateKaufmanAdaptiveMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.LeastSquaresMovingAverage:
                movingAvgList = stockData.CalculateLeastSquaresMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.LeoMovingAverage:
                movingAvgList = stockData.CalculateLeoMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.LightLeastSquaresMovingAverage:
                movingAvgList = stockData.CalculateLightLeastSquaresMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.LinearExtrapolation:
                movingAvgList = stockData.CalculateLinearExtrapolation(length).CustomValuesList;
                break;
            case MovingAvgType.LinearRegression:
                movingAvgList = stockData.CalculateLinearRegression(length).CustomValuesList;
                break;
            case MovingAvgType.LinearRegressionLine:
                movingAvgList = stockData.CalculateLinearRegressionLine(length: length).CustomValuesList;
                break;
            case MovingAvgType.LinearWeightedMovingAverage:
                movingAvgList = stockData.CalculateLinearWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.McGinleyDynamicIndicator:
                movingAvgList = stockData.CalculateMcGinleyDynamicIndicator(length: length).CustomValuesList;
                break;
            case MovingAvgType.McNichollMovingAverage:
                movingAvgList = stockData.CalculateMcNichollMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.MiddleHighLowMovingAverage:
                movingAvgList = stockData.CalculateMiddleHighLowMovingAverage(length1: slowLength ?? length, length2: fastLength ?? default).CustomValuesList;
                break;
            case MovingAvgType.ModularFilter:
                movingAvgList = stockData.CalculateModularFilter(length: length).CustomValuesList;
                break;
            case MovingAvgType.MovingAverageAdaptiveQ:
                movingAvgList = stockData.CalculateMovingAverageAdaptiveQ(length: length).CustomValuesList;
                break;
            case MovingAvgType.MovingAverageV3:
                movingAvgList = stockData.CalculateMovingAverageV3(length1: length).CustomValuesList;
                break;
            case MovingAvgType.MultiDepthZeroLagExponentialMovingAverage:
                movingAvgList = stockData.CalculateMultiDepthZeroLagExponentialMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.NaturalMovingAverage:
                movingAvgList = stockData.CalculateNaturalMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.OptimalWeightedMovingAverage:
                movingAvgList = stockData.CalculateOptimalWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.OvershootReductionMovingAverage:
                movingAvgList = stockData.CalculateOvershootReductionMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.ParabolicWeightedMovingAverage:
                movingAvgList = stockData.CalculateParabolicWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.ParametricCorrectiveLinearMovingAverage:
                movingAvgList = stockData.CalculateParametricCorrectiveLinearMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.ParametricKalmanFilter:
                movingAvgList = stockData.CalculateParametricKalmanFilter(length).CustomValuesList;
                break;
            case MovingAvgType.PentupleExponentialMovingAverage:
                movingAvgList = stockData.CalculatePentupleExponentialMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.PolynomialLeastSquaresMovingAverage:
                movingAvgList = stockData.CalculatePolynomialLeastSquaresMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.PoweredKaufmanAdaptiveMovingAverage:
                movingAvgList = stockData.CalculatePoweredKaufmanAdaptiveMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.QuadraticLeastSquaresMovingAverage:
                movingAvgList = stockData.CalculateQuadraticLeastSquaresMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.QuadraticMovingAverage:
                movingAvgList = stockData.CalculateQuadraticMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.QuadraticRegression:
                movingAvgList = stockData.CalculateQuadraticRegression(length: length).CustomValuesList;
                break;
            case MovingAvgType.QuadrupleExponentialMovingAverage:
                movingAvgList = stockData.CalculateQuadrupleExponentialMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.QuickMovingAverage:
                movingAvgList = stockData.CalculateQuickMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.R2AdaptiveRegression:
                movingAvgList = stockData.CalculateR2AdaptiveRegression(length: length).CustomValuesList;
                break;
            case MovingAvgType.RatioOCHLAverager:
                movingAvgList = stockData.CalculateRatioOCHLAverager().CustomValuesList;
                break;
            case MovingAvgType.RecursiveMovingTrendAverage:
                movingAvgList = stockData.CalculateRecursiveMovingTrendAverage(length).CustomValuesList;
                break;
            case MovingAvgType.RegularizedExponentialMovingAverage:
                movingAvgList = stockData.CalculateRegularizedExponentialMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.RepulsionMovingAverage:
                movingAvgList = stockData.CalculateRepulsionMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.RetentionAccelerationFilter:
                movingAvgList = stockData.CalculateRetentionAccelerationFilter(length).CustomValuesList;
                break;
            case MovingAvgType.ReverseEngineeringRelativeStrengthIndex:
                movingAvgList = stockData.CalculateReverseEngineeringRelativeStrengthIndex(length: length).CustomValuesList;
                break;
            case MovingAvgType.ReverseMovingAverageConvergenceDivergence:
                movingAvgList = stockData.CalculateReverseMovingAverageConvergenceDivergence(fastLength: fastLength ?? default, slowLength: slowLength ?? length)
                    .CustomValuesList;
                break;
            case MovingAvgType.RightSidedRickerMovingAverage:
                movingAvgList = stockData.CalculateRightSidedRickerMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.SelfWeightedMovingAverage:
                movingAvgList = stockData.CalculateSelfWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.SequentiallyFilteredMovingAverage:
                movingAvgList = stockData.CalculateSequentiallyFilteredMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.SettingLessTrendStepFiltering:
                movingAvgList = stockData.CalculateSettingLessTrendStepFiltering().CustomValuesList;
                break;
            case MovingAvgType.ShapeshiftingMovingAverage:
                movingAvgList = stockData.CalculateShapeshiftingMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.SharpModifiedMovingAverage:
                movingAvgList = stockData.CalculateSharpModifiedMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.SimpleMovingAverage:
                movingAvgList = stockData.CalculateSimpleMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.SimplifiedLeastSquaresMovingAverage:
                movingAvgList = stockData.CalculateSimplifiedLeastSquaresMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.SimplifiedWeightedMovingAverage:
                movingAvgList = stockData.CalculateSimplifiedWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.SineWeightedMovingAverage:
                movingAvgList = stockData.CalculateSineWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.SlowSmoothedMovingAverage:
                movingAvgList = stockData.CalculateSlowSmoothedMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.Spencer15PointMovingAverage:
                movingAvgList = stockData.CalculateSpencer15PointMovingAverage().CustomValuesList;
                break;
            case MovingAvgType.Spencer21PointMovingAverage:
                movingAvgList = stockData.CalculateSpencer21PointMovingAverage().CustomValuesList;
                break;
            case MovingAvgType.SquareRootWeightedMovingAverage:
                movingAvgList = stockData.CalculateSquareRootWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.Svama:
                movingAvgList = stockData.CalculateSvama(length).CustomValuesList;
                break;
            case MovingAvgType.SymmetricallyWeightedMovingAverage:
                movingAvgList = stockData.CalculateSymmetricallyWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.TStepLeastSquaresMovingAverage:
                movingAvgList = stockData.CalculateTStepLeastSquaresMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.TillsonIE2:
                movingAvgList = stockData.CalculateTillsonIE2(length: length).CustomValuesList;
                break;
            case MovingAvgType.TillsonT3MovingAverage:
                movingAvgList = stockData.CalculateTillsonT3MovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.TriangularMovingAverage:
                movingAvgList = stockData.CalculateTriangularMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.Trimean:
                movingAvgList = stockData.CalculateTrimean(length).CustomValuesList;
                break;
            case MovingAvgType.TripleExponentialMovingAverage:
                movingAvgList = stockData.CalculateTripleExponentialMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.UltimateMovingAverage:
                movingAvgList = stockData.CalculateUltimateMovingAverage(minLength: fastLength ?? default, maxLength: slowLength ?? length).CustomValuesList;
                break;
            case MovingAvgType.VariableAdaptiveMovingAverage:
                movingAvgList = stockData.CalculateVariableAdaptiveMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.VariableIndexDynamicAverage:
                movingAvgList = stockData.CalculateVariableIndexDynamicAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.VariableLengthMovingAverage:
                movingAvgList = stockData.CalculateVariableLengthMovingAverage(minLength: fastLength ?? default, maxLength: slowLength ?? length).CustomValuesList;
                break;
            case MovingAvgType.VariableMovingAverage:
                movingAvgList = stockData.CalculateVariableMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.VerticalHorizontalMovingAverage:
                movingAvgList = stockData.CalculateVerticalHorizontalMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.VolatilityMovingAverage:
                movingAvgList = stockData.CalculateVolatilityMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.VolatilityWaveMovingAverage:
                movingAvgList = stockData.CalculateVolatilityWaveMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.VolumeAdjustedMovingAverage:
                movingAvgList = stockData.CalculateVolumeAdjustedMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.VolumeWeightedAveragePrice:
                movingAvgList = stockData.CalculateVolumeWeightedAveragePrice().CustomValuesList;
                break;
            case MovingAvgType.VolumeWeightedMovingAverage:
                movingAvgList = stockData.CalculateVolumeWeightedMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.WeightedMovingAverage:
                movingAvgList = stockData.CalculateWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.WellRoundedMovingAverage:
                movingAvgList = stockData.CalculateWellRoundedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.WildersSmoothingMethod:
                movingAvgList = stockData.CalculateWellesWilderMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.WildersSummationMethod:
                movingAvgList = stockData.CalculateWellesWilderSummation(length).CustomValuesList;
                break;
            case MovingAvgType.WindowedVolumeWeightedMovingAverage:
                movingAvgList = stockData.CalculateWindowedVolumeWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.ZeroLagExponentialMovingAverage:
                movingAvgList = stockData.CalculateZeroLagExponentialMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.ZeroLagTripleExponentialMovingAverage:
                movingAvgList = stockData.CalculateZeroLagTripleExponentialMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.ZeroLowLagMovingAverage:
                movingAvgList = stockData.CalculateZeroLowLagMovingAverage(length: length).CustomValuesList;
                break;
            default:
                Console.WriteLine($"Moving Avg Name: {movingAvgType} not supported!");
                break;
        }

        return movingAvgList;
    }

    /// <summary>
    /// Gets the input values list.
    /// </summary>
    /// <param name="inputName">Name of the input.</param>
    /// <param name="stockData">The stock data.</param>
    /// <returns></returns>
    public static (List<double> inputList, List<double> highList, List<double> lowList, List<double> openList, List<double> closeList,
        List<double> volumeList) GetInputValuesList(InputName inputName, StockData stockData)
    {
        List<double> highList;
        List<double> lowList;
        List<double> openList;
        List<double> closeList;
        List<double> volumeList;
        var inputList = inputName switch
        {
            InputName.Close => stockData.ClosePrices,
            InputName.Low => stockData.LowPrices,
            InputName.High => stockData.HighPrices,
            InputName.Volume => stockData.Volumes,
            InputName.TypicalPrice => stockData.CalculateTypicalPrice().CustomValuesList,
            InputName.FullTypicalPrice => stockData.CalculateFullTypicalPrice().CustomValuesList,
            InputName.MedianPrice => stockData.CalculateMedianPrice().CustomValuesList,
            InputName.WeightedClose => stockData.CalculateWeightedClose().CustomValuesList,
            InputName.Open => stockData.OpenPrices,
            InputName.AdjustedClose => stockData.ClosePrices,
            InputName.Midpoint => stockData.CalculateMidpoint().CustomValuesList,
            InputName.Midprice => stockData.CalculateMidprice().CustomValuesList,
            InputName.AveragePrice => stockData.CalculateAveragePrice().CustomValuesList,
            _ => stockData.ClosePrices,
        };

        if (inputList.Count > 0)
        {
            var sum = inputList.Sum();

            if (inputList.SequenceEqual(stockData.Volumes) || sum < stockData.LowPrices.Sum() || sum > stockData.HighPrices.Sum())
            {
                var minMaxList = GetMaxAndMinValuesList(inputList, 0);
                highList = minMaxList.Item1;
                lowList = minMaxList.Item2;
            }
            else
            {
                highList = stockData.HighPrices;
                lowList = stockData.LowPrices;
            }
        }
        else
        {
            highList = stockData.HighPrices;
            lowList = stockData.LowPrices;
        }

        openList = stockData.OpenPrices;
        closeList = stockData.ClosePrices;
        volumeList = stockData.Volumes;

        return (inputList, highList, lowList, openList, closeList, volumeList);
    }

    /// <summary>
    /// Gets the input values list.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <returns></returns>
    /// <exception cref="OoplesFinance.StockIndicators.Exceptions.CalculationException">Calculations based off of 
    /// {stockData.IndicatorName} can't be completed because this indicator doesn't have a single output.</exception>
    public static (List<double> inputList, List<double> highList, List<double> lowList, List<double> openList, List<double> volumeList) 
        GetInputValuesList(StockData stockData)
    {
        List<double> inputList;
        List<double> highList;
        List<double> lowList;
        List<double> openList;
        List<double> volumeList;

        if (stockData.CustomValuesList != null && stockData.CustomValuesList.Count > 0)
        {
            inputList = stockData.CustomValuesList;
        }
        else if ((stockData.CustomValuesList == null || (stockData.CustomValuesList != null && stockData.CustomValuesList.Count == 0)) &&
            stockData.SignalsList != null && stockData.SignalsList.Count > 0)
        {
            throw new CalculationException($"Calculations based off of {stockData.IndicatorName} can't be completed because this indicator doesn't have a single output.");
        }
        else
        {
            inputList = stockData.InputValues;
        }

        if (inputList.Count > 0)
        {
            var sum = inputList.Sum();

            if (inputList.SequenceEqual(stockData.Volumes) || sum < stockData.LowPrices.Sum() || sum > stockData.HighPrices.Sum())
            {
                var minMaxList = GetMaxAndMinValuesList(inputList, 0);
                highList = minMaxList.Item1;
                lowList = minMaxList.Item2;
            }
            else
            {
                highList = stockData.HighPrices;
                lowList = stockData.LowPrices;
            }
        }
        else
        {
            highList = stockData.HighPrices;
            lowList = stockData.LowPrices;
        }

        openList = stockData.OpenPrices;
        volumeList = stockData.Volumes;

        return (inputList, highList, lowList, openList, volumeList);
    }

    /// <summary>
    /// Gets input values using a fixed length according to the input length to be used with indicators such as Math.PIvot Points or similar indicators
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="inputLength"></param>
    /// <returns></returns>
    /// <exception cref="CalculationException"></exception>
    public static (List<double> inputList, List<double> highList, List<double> lowList, List<double> openList, List<double> volumeList)
        GetInputValuesList(StockData stockData, InputLength inputLength)
    {
        List<double> inputList = new();
        List<double> highList = new();
        List<double> lowList = new();
        List<double> openList = new();
        List<double> volumeList = new();

        var groupedDatesParent = stockData.TickerDataList.GroupBy(x => x.Date.Date);
        for (var i = 0; i < groupedDatesParent.Count(); i++)
        {
            var parent = groupedDatesParent.ElementAt(i);

            IEnumerable<IGrouping<int, TickerData>> groupedDatesChild = inputLength switch
            {
                InputLength.Minute => parent.GroupBy(x => x.Date.Minute),
                InputLength.Hour => parent.GroupBy(x => x.Date.Hour),
                InputLength.Day => parent.GroupBy(x => x.Date.Day),
                InputLength.Week => parent.GroupBy(x => CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(x.Date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday)),
                InputLength.Month => parent.GroupBy(x => x.Date.Month),
                InputLength.Year => parent.GroupBy(x => x.Date.Year),
                _ => parent.GroupBy(x => x.Date.Day),
            };

            for (var j = 0; j < groupedDatesChild.Count(); j++)
            {
                var groupedDates = groupedDatesChild.ElementAt(j);

                if (groupedDates.Any())
                {
                    var high = groupedDates.Max(x => x.High);
                    highList.Add(high);

                    var low = groupedDates.Min(x => x.Low);
                    lowList.Add(low);

                    var volume = groupedDates.Sum(x => x.Volume);
                    volumeList.Add(volume);

                    var open = groupedDates.First().Open;
                    openList.Add(open);

                    var close = groupedDates.Last().Close;
                    inputList.Add(close);
                }
            }
        }

        return (inputList, highList, lowList, openList, volumeList);
    }

    /// <summary>
    /// Calculates the ema.
    /// </summary>
    /// <param name="currentValue">The current value.</param>
    /// <param name="prevEma">The previous ema.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static double CalculateEMA(double currentValue, double prevEma, int length = 14)
    {
        var k = MinOrMax((double)2 / (length + 1), 0.99, 0.01);
        var ema = (currentValue * k) + (prevEma * (1 - k));

        return ema;
    }

    /// <summary>
    /// Calculates the true range.
    /// </summary>
    /// <param name="currentHigh">The current high.</param>
    /// <param name="currentLow">The current low.</param>
    /// <param name="prevClose">The previous close.</param>
    /// <returns></returns>
    public static double CalculateTrueRange(double currentHigh, double currentLow, double prevClose)
    {
        return Math.Max(currentHigh - currentLow, Math.Max(Math.Abs(currentHigh - prevClose), Math.Abs(currentLow - prevClose)));
    }

    /// <summary>
    /// Calculates the percent change.
    /// </summary>
    /// <param name="currentValue">The current value.</param>
    /// <param name="previousValue">The previous value.</param>
    /// <returns></returns>
    public static double CalculatePercentChange(double currentValue, double previousValue)
    {
        return previousValue != 0 ? (currentValue - previousValue) / Math.Abs(previousValue) * 100 : 0;
    }

    /// <summary>
    /// Gets the maximum and minimum values list.
    /// </summary>
    /// <param name="inputs">The inputs.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static (List<double>, List<double>) GetMaxAndMinValuesList(List<double> inputs, int length)
    {
        List<double> highestValuesList = new();
        List<double> lowestValuesList = new();
        List<double> inputList = new();

        for (var i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i];
            inputList.AddRounded(input);

            var list = inputList.TakeLastExt(Math.Max(length, 2)).ToList();

            var highestValue = list.Max();
            highestValuesList.AddRounded(highestValue);

            var lowestValue = list.Min();
            lowestValuesList.AddRounded(lowestValue);
        }

        return (highestValuesList, lowestValuesList);
    }

    /// <summary>
    /// Gets the maximum and minimum values list.
    /// </summary>
    /// <param name="highList">The high list.</param>
    /// <param name="lowList">The low list.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static (List<double>, List<double>) GetMaxAndMinValuesList(List<double> highList, List<double> lowList, int length)
    {
        List<double> highestList = new();
        List<double> lowestList = new();
        List<double> tempHighList = new();
        List<double> tempLowList = new();
        var count = highList.Count == lowList.Count ? highList.Count : 0;

        for (var i = 0; i < count; i++)
        {
            var high = highList[i];
            tempHighList.AddRounded(high);

            var low = lowList[i];
            tempLowList.AddRounded(low);

            var highest = tempHighList.TakeLastExt(length).Max();
            highestList.AddRounded(highest);

            var lowest = tempLowList.TakeLastExt(length).Min();
            lowestList.AddRounded(lowest);
        }

        return (highestList, lowestList);
    }

    /// <summary>
    /// Rounds the incoming value to a default of 4 double points
    /// </summary>
    /// <param name="list">The list.</param>
    /// <param name="value">The value.</param>
    public static void AddRounded(this List<double> list, double value)
    {
        list.Add(value);
    }

    /// <summary>
    /// Ensures that there are enough past values to be able to perform calculations on the data
    /// </summary>
    /// <param name="currentIndex"></param>
    /// <param name="minIndex"></param>
    /// <param name="currentValue"></param>
    /// <returns></returns>
    public static double MinPastValues(int currentIndex, int minIndex, double currentValue)
    {
        return currentIndex >= minIndex ? currentValue : 0;
    }

    /// <summary>
    /// Extension for the default TakeLast method that works for older versions of .Net
    /// </summary>
    /// <typeparam name="TSource"></typeparam>
    /// <param name="source"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    public static IEnumerable<T> TakeLastExt<T>(this IEnumerable<T> source, int count)
    {
        if (null == source)
            throw new ArgumentNullException(nameof(source));
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        if (0 == count)
            yield break;

        if (source is ICollection<T> collection)
        {
            foreach (var item in source.Skip(Math.Max(0, collection.Count - count)))
                yield return item;

            yield break;
        }

        if (source is IReadOnlyCollection<T> collection1)
        {
            foreach (var item in source.Skip(Math.Max(0, collection1.Count - count)))
                yield return item;

            yield break;
        }

        Queue<T> result = new();

        foreach (var item in source)
        {
            if (result.Count == count)
                result.Dequeue();

            result.Enqueue(item);
        }

        foreach (var _ in result)
            yield return result.Dequeue();
    }

    /// <summary>
    /// Gets the Percentile Nearest Rank
    /// </summary>
    /// <param name="sequence"></param>
    /// <param name="percentile"></param>
    /// <returns></returns>
    public static double PercentileNearestRank(this IEnumerable<double> sequence, double percentile)
    {
        var list = sequence.OrderBy(i => i).ToList();
        var n = list.Count;
        var rank = n > 0 ? (int)Math.Ceiling(percentile / 100 * n) : 0;

        return list[Math.Max(rank - 1, 0)];
    }

    /// <summary>
    /// Rescales a value between a min and a max
    /// </summary>
    /// <param name="value"></param>
    /// <param name="oldMax"></param>
    /// <param name="oldMin"></param>
    /// <param name="newMax"></param>
    /// <param name="newMin"></param>
    /// <param name="isReversed"></param>
    /// <returns></returns>
    public static double RescaleValue(double value, double oldMax, double oldMin, double newMax, double newMin, bool isReversed = false)
    {
        var d = isReversed ? (oldMax - value) : (value - oldMin);
        var dRatio = oldMax - oldMin != 0 ? d / (oldMax - oldMin) : 0;

        return (dRatio * (newMax - newMin)) + newMin;
    }

    /// <summary>
    /// This needs to be called after you calculate an indicator if you are re-using the same input data to calculate a second indicator on a separate line
    /// </summary>
    /// <param name="stockData"></param>
    public static void Clear(this StockData stockData)
    {
        stockData.SignalsList?.Clear();
        stockData.CustomValuesList?.Clear();
    }
}