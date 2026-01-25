//     Ooples Finance Stock Indicator Library
//     https://ooples.github.io/OoplesFinance.StockIndicators/
//
//     Copyright © Franklin Moormann, 2020-2022
//     cheatcountry@gmail.com
//
//     This library is free software and it uses the Apache 2.0 license
//     so if you are going to re-use or modify my code then I just ask
//     that you include my copyright info and my contact info in a comment

using System;
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;

namespace OoplesFinance.StockIndicators.Core.Registry;

/// <summary>
/// Registry for MovingAvgType to IMovingAverageCore mapping.
/// Provides O(1) lookup for MA implementations.
/// </summary>
public static class MovingAverageRegistry
{
    private static readonly Dictionary<MovingAvgType, IMovingAverageCore> _cores;

    static MovingAverageRegistry()
    {
        _cores = new Dictionary<MovingAvgType, IMovingAverageCore>
        {
            // Standard single-input MAs
            [MovingAvgType.SimpleMovingAverage] = new SmaCore(),
            [MovingAvgType.ExponentialMovingAverage] = new EmaCore(),
            [MovingAvgType.DoubleExponentialMovingAverage] = new DemaCore(),
            [MovingAvgType.TripleExponentialMovingAverage] = new TemaCore(),
            [MovingAvgType.WeightedMovingAverage] = new WmaCore(),
            [MovingAvgType.HullMovingAverage] = new HmaCore(),
            [MovingAvgType.TriangularMovingAverage] = new TmaCore(),
            [MovingAvgType.ArnaudLegouxMovingAverage] = new AlmaCore(),
            [MovingAvgType.KaufmanAdaptiveMovingAverage] = new KamaCore(),
            [MovingAvgType.TillsonT3MovingAverage] = new T3Core(),
            [MovingAvgType.ZeroLagExponentialMovingAverage] = new ZlemaCore(),
            [MovingAvgType.LinearRegression] = new LinearRegressionCore(),
            [MovingAvgType.WildersSmoothingMethod] = new WilderCore(),

            // Additional single-input MAs
            [MovingAvgType._1LCLeastSquaresMovingAverage] = new OneLcLsmaCore(),
            [MovingAvgType._3HMA] = new ThreeHmaCore(),
            [MovingAvgType.AdaptiveAutonomousRecursiveMovingAverage] = new AarmaCore(),
            [MovingAvgType.AdaptiveExponentialMovingAverage] = new AemaCore(),
            [MovingAvgType.AdaptiveLeastSquares] = new AlsCore(),
            [MovingAvgType.AdaptiveMovingAverage] = new AmaCore(),
            [MovingAvgType.AhrensMovingAverage] = new AhrensCore(),
            [MovingAvgType.AlphaDecreasingExponentialMovingAverage] = new AlphaDecEmaCore(),
            [MovingAvgType.AutonomousRecursiveMovingAverage] = new ArmaCore(),
            [MovingAvgType.BryantAdaptiveMovingAverage] = new BamaCore(),
            [MovingAvgType.CompoundRatioMovingAverage] = new CrmaCore(),
            [MovingAvgType.CorrectedMovingAverage] = new CmaCore(),
            [MovingAvgType.CubedWeightedMovingAverage] = new CubedWmaCore(),
            [MovingAvgType.DampedSineWaveWeightedFilter] = new DswwfCore(),
            [MovingAvgType.DistanceWeightedMovingAverage] = new DwmaCore(),
            [MovingAvgType.DoubleExponentialSmoothing] = new DesCore(),
            [MovingAvgType.DynamicallyAdjustableFilter] = new DafCore(),
            [MovingAvgType.DynamicallyAdjustableMovingAverage] = new DamaCore(),
            [MovingAvgType.EdgePreservingFilter] = new EpfCore(),
            [MovingAvgType.EndPointWeightedMovingAverage] = new EpmaCore(),
            [MovingAvgType.FallingRisingFilter] = new FrfCore(),
            [MovingAvgType.FareySequenceWeightedMovingAverage] = new FswmaCore(),
            [MovingAvgType.FibonacciWeightedMovingAverage] = new FwmaCore(),
            [MovingAvgType.FisherLeastSquaresMovingAverage] = new FlsmaCore(),
            [MovingAvgType.FollowingAdaptiveMovingAverage] = new FamaCore(),
            [MovingAvgType.GeneralFilterEstimator] = new GfeCore(),
            [MovingAvgType.GeneralizedDoubleExponentialMovingAverage] = new GdemaCore(),
            [MovingAvgType.HampelFilter] = new HampelCore(),
            [MovingAvgType.HendersonWeightedMovingAverage] = new HwmaCore(),
            [MovingAvgType.HoltExponentialMovingAverage] = new HoltEmaCore(),
            [MovingAvgType.HullEstimate] = new HullEstCore(),
            [MovingAvgType.HybridConvolutionFilter] = new HcfCore(),
            [MovingAvgType.IIRLeastSquaresEstimate] = new IirLsCore(),
            [MovingAvgType.InverseDistanceWeightedMovingAverage] = new IdwmaCore(),
            [MovingAvgType.JsaMovingAverage] = new JsaCore(),
            [MovingAvgType.JurikMovingAverage] = new JmaCore(),
            [MovingAvgType.KalmanSmoother] = new KalmanCore(),
            [MovingAvgType.KaufmanAdaptiveLeastSquaresMovingAverage] = new KalsmaCore(),
            [MovingAvgType.LeastSquaresMovingAverage] = new LsmaCore(),
            [MovingAvgType.LeoMovingAverage] = new LeoCore(),
            [MovingAvgType.LightLeastSquaresMovingAverage] = new LlsmaCore(),
            [MovingAvgType.LinearExtrapolation] = new LinExtCore(),
            [MovingAvgType.LinearRegressionLine] = new LrlCore(),
            [MovingAvgType.LinearWeightedMovingAverage] = new LwmaCore(),
            [MovingAvgType.McNichollMovingAverage] = new McNichollCore(),
            [MovingAvgType.ModularFilter] = new ModularCore(),
            [MovingAvgType.MovingAverageAdaptiveQ] = new MaaqCore(),
            [MovingAvgType.MovingAverageV3] = new Mav3Core(),
            [MovingAvgType.MultiDepthZeroLagExponentialMovingAverage] = new MdzlemaCore(),
            [MovingAvgType.NaturalMovingAverage] = new NmaCore(),
            [MovingAvgType.OptimalWeightedMovingAverage] = new OwmaCore(),
            [MovingAvgType.OvershootReductionMovingAverage] = new OrmaCore(),
            [MovingAvgType.ParabolicWeightedMovingAverage] = new PwmaCore(),
            [MovingAvgType.ParametricCorrectiveLinearMovingAverage] = new PclmaCore(),
            [MovingAvgType.ParametricKalmanFilter] = new PkfCore(),
            [MovingAvgType.PentupleExponentialMovingAverage] = new PemaCore(),
            [MovingAvgType.PolynomialLeastSquaresMovingAverage] = new PolyLsmaCore(),
            [MovingAvgType.PoweredKaufmanAdaptiveMovingAverage] = new PkamaCore(),
            [MovingAvgType.QuadraticLeastSquaresMovingAverage] = new QlsmaCore(),
            [MovingAvgType.QuadraticMovingAverage] = new QmaCore(),
            [MovingAvgType.QuadraticRegression] = new QrCore(),
            [MovingAvgType.QuadrupleExponentialMovingAverage] = new QemaCore(),
            [MovingAvgType.QuickMovingAverage] = new QuickMaCore(),
            [MovingAvgType.R2AdaptiveRegression] = new R2arCore(),
            [MovingAvgType.RecursiveMovingTrendAverage] = new RmtaCore(),
            [MovingAvgType.RegularizedExponentialMovingAverage] = new RemaCore(),
            [MovingAvgType.RepulsionMovingAverage] = new RepmaCore(),
            [MovingAvgType.RetentionAccelerationFilter] = new RafCore(),
            [MovingAvgType.ReverseEngineeringRelativeStrengthIndex] = new RersiCore(),
            [MovingAvgType.ReverseMovingAverageConvergenceDivergence] = new RmacdCore(),
            [MovingAvgType.RightSidedRickerMovingAverage] = new RsrmaCore(),
            [MovingAvgType.SelfWeightedMovingAverage] = new SwmaCore(),
            [MovingAvgType.SequentiallyFilteredMovingAverage] = new SfmaCore(),
            [MovingAvgType.SettingLessTrendStepFiltering] = new SltsCore(),
            [MovingAvgType.ShapeshiftingMovingAverage] = new SsmaCore(),
            [MovingAvgType.SharpModifiedMovingAverage] = new SmmaCore(),
            [MovingAvgType.SimplifiedLeastSquaresMovingAverage] = new SlsmaCore(),
            [MovingAvgType.SimplifiedWeightedMovingAverage] = new SimWmaCore(),
            [MovingAvgType.SineWeightedMovingAverage] = new SinWmaCore(),
            [MovingAvgType.SlowSmoothedMovingAverage] = new SloSmmaCore(),
            [MovingAvgType.Spencer15PointMovingAverage] = new Sp15Core(),
            [MovingAvgType.Spencer21PointMovingAverage] = new Sp21Core(),
            [MovingAvgType.SquareRootWeightedMovingAverage] = new SqrtWmaCore(),
            [MovingAvgType.Svama] = new SvamaCore(),
            [MovingAvgType.SymmetricallyWeightedMovingAverage] = new SymWmaCore(),
            [MovingAvgType.TStepLeastSquaresMovingAverage] = new TstepLsmaCore(),
            [MovingAvgType.TillsonIE2] = new TillsonIe2Core(),
            [MovingAvgType.Trimean] = new TrimeanCore(),
            [MovingAvgType.UltimateMovingAverage] = new UmaCore(),
            [MovingAvgType.VariableAdaptiveMovingAverage] = new VamaCore(),
            [MovingAvgType.VariableIndexDynamicAverage] = new VidyaCore(),
            [MovingAvgType.VariableLengthMovingAverage] = new VlmaCore(),
            [MovingAvgType.VariableMovingAverage] = new VmaCore(),
            [MovingAvgType.VerticalHorizontalMovingAverage] = new VhmaCore(),
            [MovingAvgType.VolatilityMovingAverage] = new VolMaCore(),
            [MovingAvgType.VolatilityWaveMovingAverage] = new VwmCore(),
            [MovingAvgType.WellRoundedMovingAverage] = new WrmaCore(),
            [MovingAvgType.WildersSummationMethod] = new WilderSumCore(),
            [MovingAvgType.ZeroLagTripleExponentialMovingAverage] = new ZltemaCore(),
            [MovingAvgType.ZeroLowLagMovingAverage] = new ZllmaCore(),

            // Ehlers filters
            [MovingAvgType.Ehlers2PoleButterworthFilterV1] = new E2pbfV1Core(),
            [MovingAvgType.Ehlers2PoleButterworthFilterV2] = new E2pbfV2Core(),
            [MovingAvgType.Ehlers3PoleButterworthFilterV1] = new E3pbfV1Core(),
            [MovingAvgType.Ehlers3PoleButterworthFilterV2] = new E3pbfV2Core(),
            [MovingAvgType.Ehlers2PoleSuperSmootherFilterV1] = new E2pssV1Core(),
            [MovingAvgType.Ehlers2PoleSuperSmootherFilterV2] = new E2pssV2Core(),
            [MovingAvgType.Ehlers3PoleSuperSmootherFilter] = new E3pssCore(),
            [MovingAvgType.EhlersAdaptiveLaguerreFilter] = new EalfCore(),
            [MovingAvgType.EhlersAllPassPhaseShifter] = new EapsCore(),
            [MovingAvgType.EhlersAverageErrorFilter] = new EaefCore(),
            [MovingAvgType.EhlersBetterExponentialMovingAverage] = new EbemaCore(),
            [MovingAvgType.EhlersChebyshevLowPassFilter] = new EclpfCore(),
            [MovingAvgType.EhlersDeviationScaledMovingAverage] = new EdsmaCore(),
            [MovingAvgType.EhlersDeviationScaledSuperSmoother] = new EdssCore(),
            [MovingAvgType.EhlersDistanceCoefficientFilter] = new EdcfCore(),
            [MovingAvgType.EhlersFilter] = new EfCore(),
            [MovingAvgType.EhlersFiniteImpulseResponseFilter] = new EfirCore(),
            [MovingAvgType.EhlersFractalAdaptiveMovingAverage] = new EfamaCore(),
            [MovingAvgType.EhlersGaussianFilter] = new EgfCore(),
            [MovingAvgType.EhlersHammingMovingAverage] = new EhmaCore(),
            [MovingAvgType.EhlersHannMovingAverage] = new EhannCore(),
            [MovingAvgType.EhlersInfiniteImpulseResponseFilter] = new EiirCore(),
            [MovingAvgType.EhlersKaufmanAdaptiveMovingAverage] = new EkamaCore(),
            [MovingAvgType.EhlersLaguerreFilter] = new ElfCore(),
            [MovingAvgType.EhlersLeadingIndicator] = new EliCore(),
            [MovingAvgType.EhlersMedianAverageAdaptiveFilter] = new EmaafCore(),
            [MovingAvgType.EhlersMesaAdaptiveMovingAverage] = new EmesaCore(),
            [MovingAvgType.EhlersModifiedOptimumEllipticFilter] = new EmoefCore(),
            [MovingAvgType.EhlersNoiseEliminationTechnology] = new EnetCore(),
            [MovingAvgType.EhlersOptimumEllipticFilter] = new EoefCore(),
            [MovingAvgType.EhlersRecursiveMedianFilter] = new ErmfCore(),
            [MovingAvgType.EhlersSimpleDecycler] = new EsdCore(),
            [MovingAvgType.EhlersSuperSmootherFilter] = new EssfCore(),
            [MovingAvgType.EhlersTriangleMovingAverage] = new EtmaCore(),
            [MovingAvgType.EhlersVariableIndexDynamicAverage] = new EVidyaCore(),
            [MovingAvgType.EhlersZeroLagExponentialMovingAverage] = new EzlemaCore(),

            // Volume-weighted MAs
            [MovingAvgType.VolumeWeightedMovingAverage] = new VwmaCore(),
            [MovingAvgType.VolumeAdjustedMovingAverage] = new VadjmaCore(),
            [MovingAvgType.VolumeWeightedAveragePrice] = new VwapCore(),
            [MovingAvgType.ElasticVolumeWeightedMovingAverageV1] = new Evwma1Core(),
            [MovingAvgType.ElasticVolumeWeightedMovingAverageV2] = new Evwma2Core(),
            [MovingAvgType.EquityMovingAverage] = new EquityMaCore(),
            [MovingAvgType.WindowedVolumeWeightedMovingAverage] = new WvwmaCore(),

            // OHLC MAs
            [MovingAvgType.McGinleyDynamicIndicator] = new McGinleyCore(),
            [MovingAvgType.MiddleHighLowMovingAverage] = new MhlmaCore(),
            [MovingAvgType.AutoFilter] = new AutoFilterCore(),
            [MovingAvgType.RatioOCHLAverager] = new RochlCore(),
            [MovingAvgType.AtrFilteredExponentialMovingAverage] = new AtrfEmaCore(),
            [MovingAvgType.TrueRangeAdjustedExponentialMovingAverage] = new TraemaCore(),
        };
    }

    /// <summary>
    /// Gets the IMovingAverageCore implementation for the specified MovingAvgType.
    /// </summary>
    /// <param name="type">The moving average type.</param>
    /// <returns>The core implementation, or null if not registered.</returns>
    public static IMovingAverageCore? Get(MovingAvgType type)
    {
        return _cores.TryGetValue(type, out var core) ? core : null;
    }

    /// <summary>
    /// Gets the IMovingAverageCore implementation for the specified MovingAvgType.
    /// Throws if not found.
    /// </summary>
    /// <param name="type">The moving average type.</param>
    /// <returns>The core implementation.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the type is not registered.</exception>
    public static IMovingAverageCore GetRequired(MovingAvgType type)
    {
        if (_cores.TryGetValue(type, out var core))
            return core;
        throw new KeyNotFoundException($"MovingAvgType.{type} is not registered in MovingAverageRegistry.");
    }

    /// <summary>
    /// Checks if a MovingAvgType is registered.
    /// </summary>
    public static bool IsRegistered(MovingAvgType type) => _cores.ContainsKey(type);

    /// <summary>
    /// Gets the count of registered MA types.
    /// </summary>
    public static int Count => _cores.Count;
}

#region Single-Input MA Implementations

/// <summary>
/// Simple Moving Average core implementation.
/// </summary>
public readonly struct SmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
        => MovingAverageCore.SimpleMovingAverage(input, output, length);

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(input, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length)
        => Compute(close, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(close, output, length);

    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume,
        Span<double> output, int length)
        => Compute(input, output, length);
}

/// <summary>
/// Exponential Moving Average core implementation.
/// </summary>
public readonly struct EmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
        => MovingAverageCore.ExponentialMovingAverage(input, output, length);

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(input, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length)
        => Compute(close, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(close, output, length);

    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume,
        Span<double> output, int length)
        => Compute(input, output, length);
}

/// <summary>
/// Double Exponential Moving Average core implementation.
/// </summary>
public readonly struct DemaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
        => MovingAverageCore.DoubleExponentialMovingAverage(input, output, length);

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(input, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length)
        => Compute(close, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(close, output, length);

    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume,
        Span<double> output, int length)
        => Compute(input, output, length);
}

/// <summary>
/// Triple Exponential Moving Average core implementation.
/// </summary>
public readonly struct TemaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
        => MovingAverageCore.TripleExponentialMovingAverage(input, output, length);

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(input, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length)
        => Compute(close, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(close, output, length);

    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume,
        Span<double> output, int length)
        => Compute(input, output, length);
}

/// <summary>
/// Weighted Moving Average core implementation.
/// </summary>
public readonly struct WmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
        => MovingAverageCore.WeightedMovingAverage(input, output, length);

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(input, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length)
        => Compute(close, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(close, output, length);

    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume,
        Span<double> output, int length)
        => Compute(input, output, length);
}

/// <summary>
/// Hull Moving Average core implementation.
/// </summary>
public readonly struct HmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
        => MovingAverageCore.HullMovingAverage(input, output, length);

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(input, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length)
        => Compute(close, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(close, output, length);

    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume,
        Span<double> output, int length)
        => Compute(input, output, length);
}

/// <summary>
/// Triangular Moving Average core implementation.
/// </summary>
public readonly struct TmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
        => MovingAverageCore.TriangularMovingAverage(input, output, length);

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(input, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length)
        => Compute(close, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(close, output, length);

    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume,
        Span<double> output, int length)
        => Compute(input, output, length);
}

/// <summary>
/// Zero-Lag Exponential Moving Average core implementation.
/// </summary>
public readonly struct ZlemaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
        => MovingAverageCore.ZeroLagEma(input, output, length);

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(input, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length)
        => Compute(close, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(close, output, length);

    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume,
        Span<double> output, int length)
        => Compute(input, output, length);
}

/// <summary>
/// Linear Regression core implementation.
/// </summary>
public readonly struct LinearRegressionCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
        => MovingAverageCore.LinearRegression(input, output, length);

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(input, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length)
        => Compute(close, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(close, output, length);

    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume,
        Span<double> output, int length)
        => Compute(input, output, length);
}

/// <summary>
/// Wilder's Smoothing Method core implementation.
/// </summary>
public readonly struct WilderCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
        => MovingAverageCore.WellesWilderMovingAverage(input, output, length);

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(input, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length)
        => Compute(close, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(close, output, length);

    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume,
        Span<double> output, int length)
        => Compute(input, output, length);
}

#endregion

#region MAs with Extra Parameters

/// <summary>
/// Arnaud Legoux Moving Average core implementation.
/// Extra params: [0] = offset (default 0.85), [1] = sigma (default 6)
/// </summary>
public readonly struct AlmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => true;

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
        => MovingAverageCore.ArnaudLegouxMovingAverage(input, output, length, 0.85, 6);

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams)
    {
        var offset = extraParams.Length > 0 ? extraParams[0] : 0.85;
        var sigma = extraParams.Length > 1 ? extraParams[1] : 6.0;
        MovingAverageCore.ArnaudLegouxMovingAverage(input, output, length, offset, sigma);
    }

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length)
        => Compute(close, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(close, output, length, extraParams);

    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume,
        Span<double> output, int length)
        => Compute(input, output, length);
}

/// <summary>
/// Kaufman Adaptive Moving Average core implementation.
/// </summary>
public readonly struct KamaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
        => MovingAverageCore.KaufmanAdaptiveMovingAverage(input, output, length);

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(input, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length)
        => Compute(close, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(close, output, length, extraParams);

    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume,
        Span<double> output, int length)
        => Compute(input, output, length);
}

/// <summary>
/// T3 Moving Average core implementation.
/// </summary>
public readonly struct T3Core : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
        => MovingAverageCore.T3MovingAverage(input, output, length);

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(input, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length)
        => Compute(close, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(close, output, length, extraParams);

    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume,
        Span<double> output, int length)
        => Compute(input, output, length);
}

#endregion

#region Additional Single-Input MA Implementations

public readonly struct OneLcLsmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.OneLCLeastSquaresMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct ThreeHmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.ThreeHma(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct AarmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.AdaptiveAutonomousRecursiveMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct AemaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.AdaptiveExponentialMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct AlsCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.AdaptiveLeastSquares(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct AmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.AdaptiveMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct AhrensCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.AhrensMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct AlphaDecEmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.AlphaDecreasingExponentialMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct ArmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.AutonomousRecursiveMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct BamaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.BryantAdaptiveMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct CrmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.CompoundRatioMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct CmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.CorrectedMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct CubedWmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.CubedWeightedMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct DswwfCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.DampedSineWaveWeightedFilter(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct DwmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.DistanceWeightedMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct DesCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.DoubleExponentialSmoothing(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct DafCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.DynamicallyAdjustableFilter(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct DamaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.DynamicallyAdjustableMovingAverage(input, output, length, length * 10);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct EpfCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.EdgePreservingFilter(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct EpmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.EndPointMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct FrfCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.FallingRisingFilter(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct FswmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.FareySequenceWeightedMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct FwmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.FibonacciWeightedMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct FlsmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.FisherLeastSquaresMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct FamaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.FollowingAdaptiveMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct GfeCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.GeneralFilterEstimator(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct GdemaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.GeneralizedDoubleExponentialMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct HampelCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.HampelFilter(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct HwmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.HendersonWeightedMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct HoltEmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.HoltExponentialMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct HullEstCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.HullEstimate(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct HcfCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.HybridConvolutionFilter(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct IirLsCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.IIRLeastSquaresEstimate(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct IdwmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.InverseDistanceWeightedMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct JsaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.JsaMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct JmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.JurikMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct KalmanCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.KalmanSmoother(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct KalsmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.KaufmanAdaptiveLeastSquaresMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct LsmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.LeastSquaresMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct LeoCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.LeoMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct LlsmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.LightLeastSquaresMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct LinExtCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.LinearExtrapolation(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct LrlCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.LinearRegressionLine(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct LwmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.LinearWeightedMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct McNichollCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.McNichollMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct ModularCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.ModularFilter(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct MaaqCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.MovingAverageAdaptiveQ(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct Mav3Core : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.MovingAverageV3(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct MdzlemaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.MultiDepthZeroLagExponentialMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct NmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.NaturalMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct OwmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.OptimalWeightedMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct OrmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.OvershootReductionMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct PwmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.ParabolicWeightedMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct PclmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.ParametricCorrectiveLinearMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct PkfCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.ParametricKalmanFilter(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct PemaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.PentupleExponentialMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct PolyLsmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.PolynomialLeastSquaresMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct PkamaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.PoweredKaufmanAdaptiveMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct QlsmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.QuadraticLeastSquaresMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct QmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.QuadraticMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct QrCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.QuadraticRegression(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct QemaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.QuadrupleExponentialMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct QuickMaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.QuickMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct R2arCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.R2AdaptiveRegression(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct RmtaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.RecursiveMovingTrendAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct RemaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.RegularizedEma(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct RepmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.RepulsionMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct RafCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.RetentionAccelerationFilter(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct RersiCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.ReverseEngineeringRsi(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct RmacdCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.ReverseMovingAverageConvergenceDivergence(input, output, length, length * 2);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct RsrmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.RightSidedRickerMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct SwmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.SelfWeightedMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct SfmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.SequentiallyFilteredMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct SltsCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.SettingLessTrendStepFiltering(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct SsmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.ShapeshiftingMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct SmmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.SharpModifiedMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct SlsmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.SimplifiedLeastSquaresMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct SimWmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.SimplifiedWeightedMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct SinWmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.SineWeightedMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct SloSmmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.SlowSmoothedMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct Sp15Core : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.Spencer15PointMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct Sp21Core : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.Spencer21PointMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct SqrtWmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.SquareRootWeightedMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct SvamaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.Svama(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct SymWmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.SymmetricallyWeightedMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct TstepLsmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.TStepLeastSquaresMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct TillsonIe2Core : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.TillsonIE2(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct TrimeanCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.Trimean(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct UmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.UltimateMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct VamaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.VariableAdaptiveMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct VidyaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.Vidya(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct VlmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.VariableLengthMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct VmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.VariableMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct VhmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.VerticalHorizontalMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct VolMaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.VolatilityMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct VwmCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.VolatilityWaveMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct WrmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.WellRoundedMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct WilderSumCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.WellesWilderSummation(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct ZltemaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.ZeroLagTripleExponentialMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

public readonly struct ZllmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length) => MovingAverageCore.ZeroLowLagMovingAverage(input, output, length);
    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(input, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length) => Compute(close, output, length);
    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length, ReadOnlySpan<double> extraParams) => Compute(close, output, length);
    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length) => Compute(input, output, length);
}

#endregion
