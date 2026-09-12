using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Factory for creating V2-native StatefulIndicator instances from IndicatorSpec.
/// This replaces the V1 IndicatorInvoker reflection-based approach.
///
/// The factory uses a two-tier approach:
/// 1. Fast path: TryCreateFromTypedOptions for specific typed specs (hand-written, optimized)
/// 2. Generic path: CreateFromGenericOptionsGenerated for all other indicators (source-generated)
/// </summary>
internal static partial class StatefulIndicatorFactory
{
    /// <summary>
    /// Creates a StatefulIndicator from an IndicatorSpec.
    /// </summary>
    /// <param name="spec">The indicator specification.</param>
    /// <returns>The stateful indicator instance.</returns>
    /// <exception cref="NotSupportedException">If the indicator is not supported in V2.</exception>
    public static IStreamingIndicatorState Create(IndicatorSpec spec)
    {
        // First, try specific typed options (fast path)
        var result = TryCreateFromTypedOptions(spec);
        if (result is not null)
        {
            return result;
        }

        // Handle GenericIndicatorOptions by IndicatorName using source-generated factory
        if (spec.Options is GenericIndicatorOptions generic)
        {
            return CreateFromGenericOptionsGenerated(spec.Name, generic);
        }

        throw new NotSupportedException($"Indicator '{spec.Name}' with options type '{spec.Options?.GetType().Name}' is not supported in V2.");
    }

    /// <summary>
    /// Creates from specific typed options (optimized path).
    /// </summary>
    private static IStreamingIndicatorState? TryCreateFromTypedOptions(IndicatorSpec spec)
    {
        return spec.Options switch
        {
            // Moving Averages - use named parameters where MovingAvgType is first
            SmaSpecOptions sma => new SimpleMovingAverageState(sma.Length),
            EmaSpecOptions ema => new ExponentialMovingAverageState(ema.Length),
            WmaSpecOptions wma => new WeightedMovingAverageState(wma.Length),
            DemaSpecOptions dema => new DoubleExponentialMovingAverageState(length: dema.Length),
            TemaSpecOptions tema => new TripleExponentialMovingAverageState(length: tema.Length),
            HmaSpecOptions hma => new HullMovingAverageState(length: hma.Length),
            TmaSpecOptions tma => new TriangularMovingAverageState(maType: tma.MaType, length: tma.Length),
            WwmaSpecOptions wwma => new WellesWilderMovingAverageState(wwma.Length),
            ZlemaSpecOptions zlema => new ZeroLagExponentialMovingAverageState(length: zlema.Length),
            KamaSpecOptions kama => new KaufmanAdaptiveMovingAverageState(kama.Length),
            VidyaSpecOptions vidya => new VariableIndexDynamicAverageState(length: vidya.Length),
            AlmaSpecOptions alma => new ArnaudLegouxMovingAverageState(alma.Length),
            LsmaSpecOptions lsma => new LeastSquaresMovingAverageState(lsma.Length),
            FramaSpecOptions frama => new EhlersFractalAdaptiveMovingAverageState(frama.Length),
            McGinleyDynamicSpecOptions mcg => new McGinleyDynamicIndicatorState(length: mcg.Length),
            T3SpecOptions t3 => new TillsonT3MovingAverageState(maType: t3.MaType, length: t3.Length),
            LinRegSpecOptions linreg => new LinearRegressionState(length: linreg.Length),

            // Oscillators - using named parameters where needed
            RsiSpecOptions rsi => new RelativeStrengthIndexState(length: rsi.Length, maType: rsi.MaType),
            CciSpecOptions cci => new CommodityChannelIndexState(length: cci.Length),
            CmoSpecOptions cmo => new ChandeMomentumOscillatorState(length: cmo.Length),
            WilliamsRSpecOptions willr => new WilliamsRState(length: willr.Length),
            RocSpecOptions roc => new RateOfChangeState(length: roc.Length),
            MomentumSpecOptions mom => new MomentumOscillatorState(length: mom.Length),
            StochasticSpecOptions stoch => new StochasticOscillatorState(length: stoch.KLength, smoothLength1: stoch.DLength),
            PpoSpecOptions ppo => new PercentagePriceOscillatorState(fastLength: ppo.FastLength, slowLength: ppo.SlowLength),
            ApoSpecOptions apo => new AbsolutePriceOscillatorState(fastLength: apo.FastLength, slowLength: apo.SlowLength),
            TsiSpecOptions tsi => new TrueStrengthIndexState(maType: tsi.MaType, length1: tsi.LongLength, length2: tsi.ShortLength),
            AroonSpecOptions aroon => new AroonOscillatorState(length: aroon.Length),
            DpoSpecOptions dpo => new DetrendedPriceOscillatorState(length: dpo.Length),
            TrixSpecOptions trix => new TrixState(length: trix.Length, maType: trix.MaType),
            UltimateOscillatorSpecOptions uo => new UltimateOscillatorState(length1: uo.Length1, length2: uo.Length2, length3: uo.Length3),

            // MACD variants
            MacdSpecOptions macd => CreateMacdState(spec.Output, macd),
            MacdLineSpecOptions macdl => new MovingAverageConvergenceDivergenceState(macdl.FastLength, macdl.SlowLength),
            MacdSignalSpecOptions macds => new MovingAverageConvergenceDivergenceState(macds.FastLength, macds.SlowLength, macds.SignalLength),
            MacdHistogramSpecOptions macdh => new MovingAverageConvergenceDivergenceState(macdh.FastLength, macdh.SlowLength, macdh.SignalLength),

            // Bollinger Bands
            BollingerBandsSpecOptions bb => new BollingerBandsState(bb.Length, bb.StdDevMult, bb.MaType),

            // Volatility - using named parameters where MovingAvgType is first
            AtrSpecOptions atr => new AverageTrueRangeState(length: atr.Length, maType: atr.MaType),
            AdxSpecOptions adx => new AverageDirectionalIndexState(length: adx.Length, maType: adx.MaType),
            StdDevSpecOptions std => new StandardDeviationState(length: std.Length),
            VarianceSpecOptions variance => new VarianceState(length: variance.Length),
            HistoricalVolatilitySpecOptions hv => new HistoricalVolatilityState(maType: hv.MaType, length: hv.Length),
            ChaikinVolatilitySpecOptions cv => new ChaikinVolatilityState(maType: cv.MaType, length1: cv.Length),
            UlcerIndexSpecOptions ui => new UlcerIndexState(length: ui.Length),

            // Volume - using named parameters to skip MovingAvgType defaults
            ObvSpecOptions _ => new OnBalanceVolumeState(),
            AdlSpecOptions _ => new AccumulationDistributionLineState(),
            CmfSpecOptions cmf => new ChaikinMoneyFlowState(length: cmf.Length),
            ForceIndexSpecOptions fi => new ForceIndexState(maType: fi.MaType, length: fi.Length),
            MfiSpecOptions mfi => new MoneyFlowIndexState(length: mfi.Length),
            PvtSpecOptions _ => new PriceVolumeTrendState(),
            NviSpecOptions _ => new NegativeVolumeIndexState(),
            PviSpecOptions _ => new PositiveVolumeIndexState(),
            ChaikinOscillatorSpecOptions co => new ChaikinOscillatorState(fastLength: co.FastLength, slowLength: co.SlowLength),
            // The state has no averaging length of its own; its divisor is the batch indicator's, not the length.
            EmvSpecOptions _ => new EaseOfMovementState(),
            KvoSpecOptions kvo => new KlingerVolumeOscillatorState(fastLength: kvo.Length),
            MassIndexSpecOptions mi => new MassIndexState(maType: mi.MaType, length1: mi.EmaLength, length2: mi.EmaLength, length3: mi.SumLength),

            // Price/Trend - using named parameters to skip MovingAvgType defaults
            VhfSpecOptions vhf => new VerticalHorizontalFilterState(length: vhf.Length),
            HighestHighSpecOptions hh => new HighestHighState(length: hh.Length),
            LowestLowSpecOptions ll => new LowestLowState(length: ll.Length),
            RollingMaxSpecOptions rmax => new RollingMaxState(length: rmax.Length),
            RollingMinSpecOptions rmin => new RollingMinState(length: rmin.Length),
            CumulativeSumSpecOptions _ => new CumulativeSumState(),
            DonchianChannelSpecOptions dc => new DonchianChannelsState(length: dc.Length),
            ParabolicSarSpecOptions _ => new ParabolicSARState(),
            SuperTrendSpecOptions st => new SuperTrendState(maType: st.MaType, length: st.Length),
            BalanceOfPowerSpecOptions bop => new BalanceOfPowerState(length: bop.Length),

            // Additional oscillators - using named parameters to skip MovingAvgType defaults
            AwesomeOscillatorSpecOptions ao => new AwesomeOscillatorState(fastLength: ao.Length, maType: ao.MaType),
            AcceleratorOscillatorSpecOptions aco => new AcceleratorOscillatorState(fastLength: aco.Length, maType: aco.MaType),
            FisherTransformSpecOptions ft => new EhlersFisherTransformState(length: ft.Length),
            ConnorsRsiSpecOptions crsi => new ConnorsRelativeStrengthIndexState(length2: crsi.Length),
            PmoSpecOptions pmo => new PriceMomentumOscillatorState(length1: pmo.Length),
            KstSpecOptions kst => new KnowSureThingState(length1: kst.Length),
            ChoppinessIndexSpecOptions ci => new ChoppinessIndexState(length: ci.Length),
            CoppockCurveSpecOptions cop => new CoppockCurveState(maType: cop.MaType, length: cop.Length),
            ChandeForecastOscillatorSpecOptions cfo => new ChandeForecastOscillatorState(length: cfo.Length),
            BullPowerSpecOptions bp => new BullPowerIndicatorState(length: bp.Length),
            BearPowerSpecOptions bear => new BearPowerIndicatorState(length: bear.Length),
            PfeSpecOptions pfe => new PolarizedFractalEfficiencyState(length: pfe.Length),
            StcSpecOptions stc => new SchaffTrendCycleState(cycleLength: stc.Length),
            PzoSpecOptions pzo => new PriceZoneOscillatorState(length: pzo.Length),
            PgoSpecOptions pgo => new PrettyGoodOscillatorState(length: pgo.Length),
            VortexPositiveSpecOptions vp => new VortexIndicatorState(length: vp.Length),
            VortexNegativeSpecOptions vn => new VortexIndicatorState(length: vn.Length),
            TrendIntensityIndexSpecOptions tii => new TrendIntensityIndexState(maType: tii.MaType, fastLength: tii.Length),
            StochRsiSpecOptions srsi => new StochasticRelativeStrengthIndexState(maType: srsi.MaType, length: srsi.RsiLength, stochLength: srsi.StochLength),
            PvoSpecOptions pvo => new PercentageVolumeOscillatorState(fastLength: pvo.Length),
            RviSpecOptions rvi => new RelativeVigorIndexState(length: rvi.Length),

            // Additional MAs - using named parameters to skip MovingAvgType defaults
            VmaSpecOptions vma => new VariableMovingAverageState(length: vma.Length),
            AmaSpecOptions ama => new AdaptiveMovingAverageState(length: ama.Length),
            JmaSpecOptions jma => new JurikMovingAverageState(length: jma.Length),
            SuperSmootherSpecOptions ss => new EhlersSuperSmootherFilterState(length: ss.Length),

            // Not a typed option
            _ => null
        };
    }

    /// <summary>
    /// Creates MACD state with appropriate output selection.
    /// </summary>
    private static IStreamingIndicatorState CreateMacdState(IndicatorOutput output, MacdSpecOptions macd)
    {
        // MACD state handles all outputs internally - just return the base state
        return new MovingAverageConvergenceDivergenceState(macd.FastLength, macd.SlowLength, macd.SignalLength);
    }

    // NOTE: The CreateFromGenericOptionsGenerated method is auto-generated by the
    // StatefulIndicatorFactoryGenerator source generator. It provides mappings for
    // all IStreamingIndicatorState implementations discovered at compile time.
    //
    // The generated method is in: StatefulIndicatorFactory.Generated.g.cs
}
