using System.Buffers;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Models;

// The arms below still read typed options that are obsolete because their batch indicator has nothing they could
// set. Those arms are not served unless verified, and BuilderArmTests fails any served arm whose result such an
// option changes; the reads go when the options do.
#pragma warning disable CS0618

namespace OoplesFinance.StockIndicators.Builder.Compute;

/// <summary>
/// Internal fast path for indicator computation.
/// Bypasses graph machinery for maximum performance on simple single-output indicators.
/// </summary>
/// <remarks>
/// <para>This class provides a zero-allocation computation path for common indicators.</para>
/// <para>Results are stored in pooled buffers that are automatically returned on dispose.</para>
/// <para>For complex multi-output or chained indicators, use the standard SeriesEvaluator path.</para>
/// </remarks>
internal static partial class IndicatorCompute
{
    /// <summary>
    /// Computes an indicator using the fast path if available, falling back to standard computation.
    /// </summary>
    /// <param name="data">The stock data to compute on.</param>
    /// <param name="spec">The indicator specification.</param>
    /// <param name="context">The compute context for buffer management.</param>
    /// <returns>A ComputeBuffer containing the indicator result, or null if fast path unavailable.</returns>
    public static ComputeBuffer? TryComputeFast(StockData data, IndicatorSpec spec, ComputeContext context)
    {
        // A typed arm is served only once BuilderArmTests has shown it computes its batch indicator. Every other
        // typed spec with a batch indicator is computed by that indicator; see BuilderArmBinding.
        var optionsType = spec.Options.GetType();
        if (BuilderArmTargets.Targets.ContainsKey(optionsType) && !BuilderVerifiedArms.Arms.Contains(optionsType))
        {
            return BuilderArmBinding.TryCompute(data, spec, context);
        }

        return ComputeArm(data, spec, context);
    }

    /// <summary>
    /// The typed spec's own fast arm, unchecked; <see cref="TryComputeFast"/> serves it only when verified.
    /// </summary>
    internal static ComputeBuffer? ComputeArm(StockData data, IndicatorSpec spec, ComputeContext context)
    {
        return spec.Options switch
        {
            // Multi-output indicators, dispatched on the key the caller named. A spec that names none wants the
            // indicator's own series, which is the first key each of these publishes.
            MacdSpecOptions macd => spec.OutputKey switch
            {
                null or "Macd" => ComputeMacdLineFast(data, context, macd.FastLength, macd.SlowLength),
                "Signal" => ComputeMacdSignalFast(data, context, macd.FastLength, macd.SlowLength, macd.SignalLength),
                "Histogram" => ComputeMacdHistogramFast(data, context, macd.FastLength, macd.SlowLength, macd.SignalLength),
                _ => null
            },
            BollingerBandsSpecOptions bb => spec.OutputKey switch
            {
                "UpperBand" => ComputeBollingerUpperFast(data, context, bb.Length, bb.StdDevMult, bb.MaType),
                // Bollinger publishes no single series of its own, so an unnamed request is the middle band -
                // the same answer the slot path gave for Primary.
                null or "MiddleBand" => ComputeBollingerMiddleFast(data, context, bb.Length, bb.MaType),
                "LowerBand" => ComputeBollingerLowerFast(data, context, bb.Length, bb.StdDevMult, bb.MaType),
                _ => null
            },
            StochasticSpecOptions stoch => spec.OutputKey switch
            {
                null or "FastK" => ComputeStochasticKFast(data, context, stoch.KLength),
                "FastD" => ComputeStochasticDFast(data, context, stoch.KLength, stoch.DLength),
                _ => null
            },

            // Single-output indicators (Primary only)
            // Moving Averages
            SmaSpecOptions sma => ComputeSmaFast(data, context, sma.Length),
            EmaSpecOptions ema => ComputeEmaFast(data, context, ema.Length),
            WmaSpecOptions wma => ComputeWmaFast(data, context, wma.Length),
            DemaSpecOptions dema => ComputeDemaFast(data, context, dema.Length),
            TemaSpecOptions tema => ComputeTemaFast(data, context, tema.Length),
            HmaSpecOptions hma => ComputeHmaFast(data, context, hma.Length),
            TmaSpecOptions tma => ComputeTmaFast(data, context, tma.Length),
            WwmaSpecOptions wwma => ComputeWwmaFast(data, context, wwma.Length),
            LinRegSpecOptions linreg => ComputeLinRegFast(data, context, linreg.Length),
            KamaSpecOptions kama => ComputeKamaFast(data, context, kama.Length),
            ZlemaSpecOptions zlema => ComputeZlemaFast(data, context, zlema.Length),

            // Oscillators
            RsiSpecOptions rsi => ComputeRsiFast(data, context, rsi.Length, rsi.MaType),
            RocSpecOptions roc => ComputeRocFast(data, context, roc.Length),
            MomentumSpecOptions mom => ComputeMomentumFast(data, context, mom.Length),
            WilliamsRSpecOptions willr => ComputeWilliamsRFast(data, context, willr.Length),
            CciSpecOptions cci => ComputeCciFast(data, context, cci.Length),
            CmoSpecOptions cmo => ComputeCmoFast(data, context, cmo.Length),
            PpoSpecOptions ppo => ComputePpoFast(data, context, ppo.FastLength, ppo.SlowLength),
            ApoSpecOptions apo => ComputeApoFast(data, context, apo.FastLength, apo.SlowLength),
            UltimateOscillatorSpecOptions uo => ComputeUltimateOscillatorFast(data, context, uo.Length1, uo.Length2, uo.Length3),
            TsiSpecOptions tsi => ComputeTsiFast(data, context, tsi.LongLength, tsi.ShortLength),
            StochRsiSpecOptions srsi => ComputeStochasticRsiFast(data, context, srsi.RsiLength, maType: srsi.MaType,
                stochLength: srsi.StochLength),
            AroonSpecOptions aroon => ComputeAroonOscillatorFast(data, context, aroon.Length),
            DpoSpecOptions dpo => ComputeDpoFast(data, context, dpo.Length),
            TrixSpecOptions trix => ComputeTrixFast(data, context, trix.Length),
            MassIndexSpecOptions mi => ComputeMassIndexFast(data, context, mi.EmaLength, mi.SumLength),
            AtrSpecOptions atr => ComputeAtrFast(data, context, atr.Length, atr.MaType),
            AdxSpecOptions adx => ComputeAdxFast(data, context, adx.Length, adx.MaType),

            // Volume
            ObvSpecOptions obv => ComputeObvFast(data, context, obv.Length),
            AdlSpecOptions adl => ComputeAdlFast(data, context, adl.Length),
            CmfSpecOptions cmf => ComputeCmfFast(data, context, cmf.Length),
            ForceIndexSpecOptions fi => ComputeForceIndexFast(data, context, fi.Length),
            VrocSpecOptions vroc => ComputeVrocFast(data, context, vroc.Length),
            NviSpecOptions nvi => ComputeNviFast(data, context, nvi.Length),
            PviSpecOptions pvi => ComputePviFast(data, context, pvi.Length),
            PvtSpecOptions pvt => ComputePvtFast(data, context, pvt.Length),
            ChaikinOscillatorSpecOptions co => ComputeChaikinOscillatorFast(data, context, co.FastLength, co.SlowLength),
            EmvSpecOptions emv => ComputeEmvFast(data, context, emv.Length),
            KvoSpecOptions kvo => ComputeKlingerVolumeFast(data, context, kvo.Length),
            MfiSpecOptions mfi => ComputeMoneyFlowIndexFast(data, context, mfi.Length),

            // Volatility
            StdDevSpecOptions stddev => ComputeStdDevFast(data, context, stddev.Length),
            HistoricalVolatilitySpecOptions hv => ComputeHistoricalVolatilityFast(data, context, hv.Length),
            ChaikinVolatilitySpecOptions cv => ComputeChaikinVolatilityFast(data, context, cv.Length, cv.MaType),
            UlcerIndexSpecOptions ui => ComputeUlcerIndexFast(data, context, ui.Length),
            NatrSpecOptions natr => ComputeNormalizedAtrFast(data, context, natr.Length),
            TrueRangeSpecOptions tr => ComputeTrueRangeFast(data, context, tr.Length),

            // Price/Trend
            DonchianChannelSpecOptions dc => ComputeDonchianChannelFast(data, context, dc.Length),
            HighestHighSpecOptions hh => ComputeHighestHighFast(data, context, hh.Length),
            LowestLowSpecOptions ll => ComputeLowestLowFast(data, context, ll.Length),
            PercentageChangeSpecOptions pct => ComputePercentageChangeFast(data, context, pct.Length),
            LinRegSlopeSpecOptions lrs => ComputeLinRegSlopeFast(data, context, lrs.Length),
            RSquaredSpecOptions rsq => ComputeRSquaredFast(data, context, rsq.Length),
            VhfSpecOptions vhf => ComputeVhfFast(data, context, vhf.Length),

            // Additional Oscillators
            AwesomeOscillatorSpecOptions ao => ComputeAwesomeOscillatorFast(data, context, ao.Length, ao.MaType),
            AcceleratorOscillatorSpecOptions aco => ComputeAcceleratorOscillatorFast(data, context, aco.Length, aco.MaType),
            StochasticKSpecOptions sk => ComputeStochasticKFast(data, context, sk.Length),
            FisherTransformSpecOptions ft => ComputeFisherTransformFast(data, context, ft.Length),
            ConnorsRsiSpecOptions crsi => ComputeConnorsRsiFast(data, context, crsi.Length),
            PmoSpecOptions pmo => ComputePmoFast(data, context, pmo.Length),
            KstSpecOptions kst => ComputeKstFast(data, context, kst.Length),
            PercentRankSpecOptions pr => ComputePercentRankFast(data, context, pr.Length),
            ChoppinessIndexSpecOptions ci => ComputeChoppinessIndexFast(data, context, ci.Length),

            // Batch 3 - Moving Averages
            SmmaSpecOptions smma => ComputeSmmaFast(data, context, smma.Length),
            McGinleyDynamicSpecOptions mgd => ComputeMcGinleyDynamicFast(data, context, mgd.Length),
            T3SpecOptions t3 => ComputeT3Fast(data, context, t3.Length),
            VidyaSpecOptions vidya => ComputeVidyaFast(data, context, vidya.Length),
            VmaSpecOptions vma => ComputeVmaFast(data, context, vma.Length),
            AlmaSpecOptions alma => ComputeAlmaFast(data, context, alma.Length),
            LsmaSpecOptions lsma => ComputeLsmaFast(data, context, lsma.Length),
            FramaSpecOptions frama => ComputeFramaFast(data, context, frama.Length),
            AmaSpecOptions ama => ComputeAmaFast(data, context, ama.Length),
            JmaSpecOptions jma => ComputeJmaFast(data, context, jma.Length),
            SuperSmootherSpecOptions ss => ComputeSuperSmootherFast(data, context, ss.Length),
            ButterworthFilterSpecOptions bw => ComputeButterworthFilterFast(data, context, bw.Length),

            // Batch 3 - MACD variants
            MacdLineSpecOptions macdl => ComputeMacdLineFast(data, context, macdl.FastLength, macdl.SlowLength),
            MacdSignalSpecOptions macds => ComputeMacdSignalFast(data, context, macds.FastLength, macds.SlowLength, macds.SignalLength),
            MacdHistogramSpecOptions macdh => ComputeMacdHistogramFast(data, context, macdh.FastLength, macdh.SlowLength, macdh.SignalLength),

            // Batch 3 - Trend indicators
            ParabolicSarSpecOptions psar => ComputeParabolicSarFast(data, context, psar.Length),
            SuperTrendSpecOptions st => ComputeSuperTrendFast(data, context, st.Length),
            ChandelierExitLongSpecOptions cel => ComputeChandelierExitLongFast(data, context, cel.Length, cel.MaType),
            ChandelierExitShortSpecOptions ces => ComputeChandelierExitShortFast(data, context, ces.Length, ces.MaType),

            // Batch 3 - Volume/Power indicators
            BalanceOfPowerSpecOptions bop => ComputeBalanceOfPowerFast(data, context, bop.Length),
            PvoSpecOptions pvo => ComputePvoFast(data, context, pvo.Length),

            // Batch 3 - More Oscillators
            CoppockCurveSpecOptions coppock => ComputeCoppockCurveFast(data, context, coppock.Length, coppock.MaType),
            ChandeForecastOscillatorSpecOptions cfo => ComputeChandeForecastOscillatorFast(data, context, cfo.Length),
            BullPowerSpecOptions bp => ComputeBullPowerFast(data, context, bp.Length),
            BearPowerSpecOptions bear => ComputeBearPowerFast(data, context, bear.Length),
            ElderForceIndexSpecOptions efi => ComputeElderForceIndexFast(data, context, efi.Length),
            RelativeVolatilityIndexSpecOptions rvi => ComputeRelativeVolatilityIndexFast(data, context, rvi.Length),
            QstickSpecOptions qstick => ComputeQstickFast(data, context, qstick.Length),
            SpecialKSpecOptions spk => ComputeSpecialKFast(data, context, spk.Length),

            // Batch 3 - Vortex and Trend Intensity
            VortexPositiveSpecOptions vp => ComputeVortexPositiveFast(data, context, vp.Length),
            VortexNegativeSpecOptions vn => ComputeVortexNegativeFast(data, context, vn.Length),
            TrendIntensityIndexSpecOptions tii => ComputeTrendIntensityIndexFast(data, context, tii.Length),
            AbsoluteStrengthIndexSpecOptions asi => ComputeAbsoluteStrengthIndexFast(data, context, asi.Length),
            RelativeMomentumIndexSpecOptions rmi => ComputeRelativeMomentumIndexFast(data, context, rmi.Length, rmi.Momentum),
            IntradayMomentumIndexSpecOptions imi => ComputeIntradayMomentumIndexFast(data, context, imi.Length),

            // Batch 3 - Volume weighted MAs
            VwmaSpecOptions vwma => ComputeVwmaFast(data, context, vwma.Length),
            VwapSpecOptions vwap => ComputeVwapFast(data, context, vwap.Length),

            // Batch 3 - Complex oscillators
            ElliottWaveOscillatorSpecOptions ewo => ComputeElliottWaveOscillatorFast(data, context, ewo.FastLength, ewo.SlowLength),
            GatorOscillatorSpecOptions gator => ComputeGatorOscillatorFast(data, context, gator.Length),

            // Batch 3 - Ichimoku
            IchimokuTenkanSenSpecOptions its => ComputeIchimokuTenkanSenFast(data, context, its.Length),
            IchimokuKijunSenSpecOptions iks => ComputeIchimokuKijunSenFast(data, context, iks.Length),

            // Batch 3 - Additional oscillators (ComputeFast methods exist)
            PfeSpecOptions pfe => ComputePolarizedFractalEfficiencyFast(data, context, pfe.Length),
            StcSpecOptions stc => ComputeSchaffTrendCycleFast(data, context, stc.Length),
            PzoSpecOptions pzo => ComputePriceZoneOscillatorFast(data, context, pzo.Length),
            PgoSpecOptions pgo => ComputePrettyGoodOscillatorFast(data, context, pgo.Length),
            RviSpecOptions rvi => ComputeRelativeVigorIndexFast(data, context, rvi.Length),

            // Batch 4 - Price indicators
            TypicalPriceSpecOptions tp => ComputeTypicalPriceFast(data, context, tp.Length),
            MedianPriceSpecOptions mp => ComputeMedianPriceFast(data, context, mp.Length),
            WeightedCloseSpecOptions wc => ComputeWeightedCloseFast(data, context, wc.Length),
            AveragePriceSpecOptions ap => ComputeAveragePriceFast(data, context, ap.Length),
            MidpointSpecOptions midpt => ComputeMidpointFast(data, context, midpt.Length),
            MidpriceSpecOptions midpr => ComputeMidpriceFast(data, context, midpr.Length),

            // Batch 4 - Statistical indicators
            VarianceSpecOptions var => ComputeVarianceFast(data, context, var.Length),
            CoefficientOfVariationSpecOptions cov => ComputeCoefficientOfVariationFast(data, context, cov.Length),
            StandardErrorSpecOptions se => ComputeStandardErrorFast(data, context, se.Length),

            // Batch 4 - Aroon components
            AroonUpSpecOptions arup => ComputeAroonUpFast(data, context, arup.Length),
            AroonDownSpecOptions ardn => ComputeAroonDownFast(data, context, ardn.Length),

            // Batch 4 - More oscillators
            DemarkerSpecOptions dmk => ComputeDemarkerFast(data, context, dmk.Length, dmk.MaType),
            SmoothedRocSpecOptions sroc => ComputeSmoothedRateOfChangeFast(data, context, sroc.Length, maType: sroc.MaType),
            DerivativeOscillatorSpecOptions dro => ComputeDerivativeOscillatorFast(data, context, dro.Length,
                dro.MaType),
            FractalChaosOscillatorSpecOptions fco => ComputeFractalChaosOscillatorFast(data, context, fco.Length),
            DisparityIndexSpecOptions di => ComputeDisparityIndexFast(data, context, di.Length, di.MaType),
            // Length is marked as having no effect: CalculateDynamicMomentumIndex chooses its own
            // lookback per bar and has no parameter a single length could set.
            DynamicMomentumIndexSpecOptions dmi => ComputeDynamicMomentumIndexFast(data, context, dmi.MaType),

            // Batch 4 - More MAs
            SineWmaSpecOptions swma => ComputeSineWmaFast(data, context, swma.Length),
            HammingMaSpecOptions hma2 => ComputeHammingMaFast(data, context, hma2.Length),
            GeoMaSpecOptions gma => ComputeGeoMaFast(data, context, gma.Length),
            RegularizedEmaSpecOptions rema => ComputeRegularizedEmaFast(data, context, rema.Length),
            ModifiedMaSpecOptions mma => ComputeModifiedMaFast(data, context, mma.Length),
            EndPointMovingAverageSpecOptions epma => ComputeEndPointMovingAverageFast(data, context, epma.Length),
            CubicWmaSpecOptions cwma => ComputeCubicWmaFast(data, context, cwma.Length),
            NaturalMaSpecOptions nma => ComputeNaturalMaFast(data, context, nma.Length),

            // Batch 4 - Volume indicators
            TradeVolumeIndexSpecOptions tvi => ComputeTradeVolumeIndexFast(data, context, tvi.Length),
            VolumeOscillatorSpecOptions vo => ComputeVolumeOscillatorFast(data, context, vo.Length),
            VolumeZoneOscillatorSpecOptions vzo => ComputeVolumeZoneOscillatorFast(data, context, vzo.Length),
            NetVolumeSpecOptions nv => ComputeNetVolumeFast(data, context, nv.Length),
            VolumeMomentumSpecOptions vmom => ComputeVolumeMomentumFast(data, context, vmom.Length),
            NormalizedVolumeSpecOptions nvol => ComputeNormalizedVolumeFast(data, context, nvol.Length),

            // Batch 4 - Stochastic variants
            StochasticDSpecOptions sd => ComputeStochasticDFast(data, context, sd.Length),
            DoubleSmoothedStochasticSpecOptions dss => ComputeDoubleSmoothedStochasticFast(data, context, dss.Length, dss.MaType),
            PremierStochasticSpecOptions ps => ComputePremierStochasticFast(data, context, ps.Length),

            // Batch 4 - Volatility indicators
            CloseToCloseVolatilitySpecOptions ctc => ComputeCloseToCloseVolatilityFast(data, context, ctc.Length),
            ParkinsonVolatilitySpecOptions pkv => ComputeParkinsonVolatilityFast(data, context, pkv.Length),
            GarmanKlassVolatilitySpecOptions gkv => ComputeGarmanKlassVolatilityFast(data, context, gkv.Length),

            // Batch 5 - Price/Range indicators
            AdrSpecOptions adr => ComputeAdrFast(data, context, adr.Length),
            BollingerBandsMiddleSpecOptions bbm => ComputeBollingerBandsFast(data, context, bbm.Length),
            VpciSpecOptions vpci => ComputeVpciFast(data, context, vpci.Length),
            KeltnerChannelMiddleSpecOptions kcm => ComputeKeltnerChannelMiddleFast(data, context, kcm.Length),
            TrendDetectionSpecOptions td => ComputeTrendDetectionFast(data, context, td.Length),
            PriceChannelMiddleSpecOptions pcm => ComputePriceChannelMiddleFast(data, context, pcm.Length),
            SwingIndexSpecOptions swi => ComputeSwingIndexFast(data, context, swi.LimitMove),
            AccumulativeSwingIndexSpecOptions asi => ComputeAccumulativeSwingIndexFast(data, context, asi.LimitMove),
            ZigZagSpecOptions zz => ComputeZigZagFast(data, context, zz.Length),
            PivotPointSpecOptions pp => ComputePivotPointFast(data, context, pp.Length),
            RangeSpecOptions rng => ComputeRangeFast(data, context, rng.Length),
            PriceMomentumSpecOptions pmom => ComputePriceMomentumFast(data, context, pmom.Length),

            // Batch 5 - Volume indicators
            MfiCoreSpecOptions mfic => ComputeMfiCoreFast(data, context, mfic.Length),
            TwiggsMoneyFlowSpecOptions tmf => ComputeTwiggsMoneyFlowFast(data, context, tmf.Length),
            DemandIndexSpecOptions dmidx => ComputeDemandIndexFast(data, context, dmidx.Length),
            WilliamsADSpecOptions wad => ComputeWilliamsADFast(data, context, wad.Length),
            CumulativeVolumeIndexSpecOptions cvi => ComputeCumulativeVolumeIndexFast(data, context, cvi.Length),
            VolumePriceTrendSpecOptions vpt => ComputeVolumePriceTrendFast(data, context, vpt.Length),
            ElderRayBullPowerSpecOptions erbp => ComputeElderRayBullPowerFast(data, context, erbp.Length),
            ElderRayBearPowerSpecOptions erbrp => ComputeElderRayBearPowerFast(data, context, erbrp.Length),
            VolumeWeightedRsiSpecOptions vwrsi => ComputeVolumeWeightedRsiFast(data, context, vwrsi.Length),

            // Batch 5 - Trend indicators
            DirectionalTrendIndexSpecOptions dti => ComputeDirectionalTrendIndexFast(data, context, dti.Length,
                dti.MaType),
            LinRegInterceptSpecOptions lri => ComputeLinRegInterceptFast(data, context, lri.Length),
            ElderImpulseSystemSpecOptions eis => ComputeElderImpulseSystemFast(data, context, eis.Length),
            MassThrustSpecOptions mt => ComputeMassThrustFast(data, context, mt.Length),

            // Batch 5 - Chande indicators
            // Both lengths are declared obsolete because ChandeCompositeMomentumIndex has no parameter
            // they could set, so this spec asks for the same series the defaults give.
            ChandeCompositeMomentumIndexSpecOptions => ComputeChandeCompositeMomentumIndexFast(data, context),
            ChandeKrollRSquaredIndexSpecOptions ckrsi => ComputeChandeKrollRSquaredIndexFast(data, context, ckrsi.Length,
                ckrsi.MaType),
            ChandeTrendScoreSpecOptions cts => ComputeChandeTrendScoreFast(data, context, endLength: cts.Length),
            ChandeMomentumOscillatorAbsoluteSpecOptions cmoa => ComputeChandeMomentumOscillatorAbsoluteFast(data, context, cmoa.Length),

            // Batch 5 - Oscillators
            ErgodicCandlestickOscillatorSpecOptions eco => ComputeErgodicCandlestickOscillatorFast(data, context, eco.Length),
            BayesianOscillatorSpecOptions bayes => ComputeBayesianOscillatorFast(data, context, bayes.Length, bayes.MaType),
            AnchoredMomentumSpecOptions amom => ComputeAnchoredMomentumFast(data, context, amom.Length, amom.MaType),
            ChartmillValueIndicatorSpecOptions cmvi => ComputeChartmillValueIndicatorFast(data, context, cmvi.Length,
                cmvi.MaType),
            CenterOfLinearitySpecOptions col => ComputeCenterOfLinearityFast(data, context, col.Length),
            BreakoutRsiSpecOptions brsi => ComputeBreakoutRsiFast(data, context, brsi.Length),
            ChopZoneSpecOptions cz => ComputeChopZoneFast(data, context, cz.Length, cz.MaType),
            ForecastOscillatorSpecOptions fco2 => ComputeForecastOscillatorFast(data, context, fco2.Length),

            // Batch 5 - Adaptive indicators
            AsymmetricalRsiSpecOptions arsi => ComputeAsymmetricalRsiFast(data, context, arsi.UpLength, arsi.DownLength),
            AdaptiveStochasticSpecOptions adstoch => ComputeAdaptiveStochasticFast(data, context, adstoch.MinLength, adstoch.MaxLength),
            // Both min and max length are declared obsolete because AdaptiveRelativeStrengthIndex has no
            // parameter they could set, so this spec asks for the same series the defaults give.
            AdaptiveRsiSpecOptions => ComputeAdaptiveRsiFast(data, context),

            // Batch 5 - Moving averages
            AutoLineSpecOptions al => ComputeAutoLineFast(data, context, al.Length),
            AutoLineWithDriftSpecOptions alwd => ComputeAutoLineWithDriftFast(data, context, alwd.Length),
            AutoFilterSpecOptions af => ComputeAutoFilterFast(data, context, af.Length, af.MaType),
            BuffAverageSpecOptions ba => ComputeBuffAverageFast(data, context, ba.Length),
            BryantAdaptiveMovingAverageSpecOptions bama => ComputeBryantAdaptiveMovingAverageFast(data, context, bama.Length),
            CompoundRatioMovingAverageSpecOptions crma => ComputeCompoundRatioMovingAverageFast(data, context,
                crma.Length, crma.MaType),
            // Length and MaType only smooth the Signal line CalculateConditionalAccumulator publishes
            // beside the accumulator, and this spec is bound to the accumulator itself.
            ConditionalAccumulatorSpecOptions => ComputeConditionalAccumulatorFast(data, context),
            AhrensMovingAverageSpecOptions ahma => ComputeAhrensMovingAverageFast(data, context, ahma.Length),
            AlphaDecreasingEmaSpecOptions => ComputeAlphaDecreasingEmaFast(data, context),
            AdaptiveEmaSpecOptions aema => ComputeAdaptiveEmaFast(data, context, aema.Length),
            AutonomousRecursiveMaSpecOptions arma => ComputeAutonomousRecursiveMaFast(data, context, arma.Length),
            AdaptiveLeastSquaresSpecOptions als => ComputeAdaptiveLeastSquaresFast(data, context, als.Length),
            AtrFilteredEmaSpecOptions afema => ComputeAtrFilteredEmaFast(data, context, afema.Length),
            MedianMaSpecOptions medma => ComputeMedianMaFast(data, context, medma.Length),
            VolumeAdjustedMaSpecOptions vama => ComputeVolumeAdjustedMaFast(data, context, vama.Length),
            QuadraticWmaSpecOptions qwma => ComputeQuadraticWmaFast(data, context, qwma.Length),
            ParabolicWmaSpecOptions pwma => ComputeParabolicWmaFast(data, context, pwma.Length),

            // Batch 5 - Volatility indicators
            RogersSatchellVolatilitySpecOptions rsv => ComputeRogersSatchellVolatilityFast(data, context, rsv.Length),
            YangZhangVolatilitySpecOptions yzv => ComputeYangZhangVolatilityFast(data, context, yzv.Length),
            DownsideDeviationSpecOptions dd => ComputeDownsideDeviationFast(data, context, dd.Length),
            StandardDeviationChannelSpecOptions sdch => spec.OutputKey switch
            {
                null or "MiddleBand" => ComputeStandardDeviationChannelFast(data, context, sdch.Length),
                "UpperBand" => ComputeStandardDeviationChannelFast(data, context, sdch.Length, 2, ChannelBand.Upper),
                "LowerBand" => ComputeStandardDeviationChannelFast(data, context, sdch.Length, 2, ChannelBand.Lower),
                _ => null
            },
            VolatilityRatioSpecOptions vr => ComputeVolatilityRatioFast(data, context, vr.Length),

            // Batch 5 - Bands/Channels
            AtrTrailingStopsSpecOptions ats => ComputeAtrTrailingStopsFast(data, context, ats.Length, ats.Multiplier,
                ats.MaType),
            AtrChannelWidthSpecOptions acw => ComputeAtrChannelWidthFast(data, context, acw.Length, acw.Multiplier),
            AverageTrueRangeChannelSpecOptions atrc => ComputeAverageTrueRangeChannelFast(data, context, atrc.Length,
                atrc.Multiplier, atrc.MaType),
            VolatilityStopSpecOptions vs => ComputeVolatilityStopFast(data, context, vs.Length, vs.Multiplier),
            BollingerBandsPercentBSpecOptions bbpb => ComputeBollingerBandsPercentBFast(data, context, bbpb.Length, bbpb.Multiplier),
            BollingerBandsAtrSpecOptions bbatr => ComputeBollingerBandsAtrFast(data, context, bbatr.Length, bbatr.Multiplier),

            // Batch 5 - Ratio/Performance
            CalmarRatioSpecOptions cr => ComputeCalmarRatioFast(data, context, cr.Length),
            CommoditySelectionIndexSpecOptions csi => ComputeCommoditySelectionIndexFast(data, context, csi.Length,
                csi.MaType),

            // Batch 5 - Smoothed oscillators
            SmoothedWilliamsRSpecOptions swillr => ComputeSmoothedWilliamsRFast(data, context, swillr.Length, swillr.SmoothLength),
            PriceOscillatorPercentSpecOptions pop => ComputePriceOscillatorPercentFast(data, context, pop.ShortLength, pop.LongLength),
            NormalizedMacdSpecOptions nmacd => ComputeNormalizedMacdFast(data, context, nmacd.FastLength, nmacd.SlowLength),
            RelativeVigorIndexSignalSpecOptions rvis => ComputeRelativeVigorIndexSignalFast(data, context, rvis.Length, rvis.SignalLength),
            VolumeMomentumOscillatorSpecOptions vmo => ComputeVolumeMomentumOscillatorFast(data, context, vmo.ShortLength, vmo.LongLength),
            TrendContinuationFactorSpecOptions tcf => ComputeTrendContinuationFactorFast(data, context, tcf.Length),
            TrendPersistenceRateSpecOptions tpr => ComputeTrendPersistenceRateFast(data, context, tpr.Length),
            InertiaSpecOptions inertia => ComputeInertiaFast(data, context, inertia.SmoothLength, inertia.RviLength),

            // Batch 5 - Price calculations
            PercentChangeSpecOptions pchg => ComputePercentChangeFast(data, context, pchg.Length),
            PriceChangeSpecOptions prc => ComputePriceChangeFast(data, context),
            MidRangeSpecOptions mr => ComputeMidRangeFast(data, context),
            OhlcAverageSpecOptions ohlc => ComputeOhlcAverageFast(data, context),
            HlcAverageSpecOptions hlc => ComputeHlcAverageFast(data, context),
            DoubleSmoothedMomentaSpecOptions dsm => ComputeDoubleSmoothedMomentaFast(data, context, dsm.MomentumLength, dsm.MaType, dsm.FirstSmooth, dsm.SecondSmooth),

            // Batch 5 - Statistical indicators
            HighLowIndexSpecOptions hli => ComputeHighLowIndexFast(data, context, hli.Length),
            MarketFacilitationIndexSpecOptions mfidx => ComputeMarketFacilitationIndexFast(data, context, mfidx.Length),
            TrendScoreSpecOptions ts => ComputeTrendScoreFast(data, context, ts.Length),
            MedianValueSpecOptions mv => ComputeMedianValueFast(data, context, mv.Length),
            LogReturnsSpecOptions lr => ComputeLogReturnsFast(data, context, lr.Length),
            SimpleReturnsSpecOptions sr => ComputeSimpleReturnsFast(data, context, sr.Length),
            CumulativeSumSpecOptions csum => ComputeCumulativeSumFast(data, context),
            RollingMaxSpecOptions rmax => ComputeRollingMaxFast(data, context, rmax.Length),
            RollingMinSpecOptions rmin => ComputeRollingMinFast(data, context, rmin.Length),
            PricePositionSpecOptions ppos => ComputePricePositionFast(data, context, ppos.Length),
            AtrPercentSpecOptions atrp => ComputeAtrPercentFast(data, context, atrp.Length),

            // Batch 5 - Trend/Activator indicators
            RepulseSpecOptions rep => ComputeRepulseFast(data, context, rep.Length),
            GannHiLoActivatorSpecOptions ghla => ComputeGannHiLoActivatorFast(data, context, ghla.Length),
            HalfTrendSpecOptions ht => ComputeHalfTrendFast(data, context, ht.Length),

            // Batch 6 - Chande oscillators
            // Length is declared obsolete because ChandeMomentumOscillatorAbsoluteAverage has no parameter
            // it could set, so this spec asks for the same series the defaults give.
            ChandeMomentumOscillatorAbsoluteAverageSpecOptions =>
                ComputeChandeMomentumOscillatorAbsoluteAverageFast(data, context),
            // Length is declared obsolete because ChandeMomentumOscillatorAverage has no parameter it
            // could set, so this spec asks for the same series the defaults give.
            ChandeMomentumOscillatorAverageSpecOptions => ComputeChandeMomentumOscillatorAverageFast(data, context),
            // Length is declared obsolete because ChandeMomentumOscillatorAverageDisparityIndex has no
            // parameter it could set, so this spec asks for the same series the defaults give.
            ChandeMomentumOscillatorAverageDisparityIndexSpecOptions =>
                ComputeChandeMomentumOscillatorAverageDisparityIndexFast(data, context),
            ChandeMomentumOscillatorFilterSpecOptions cmof => ComputeChandeMomentumOscillatorFilterFast(data, context, cmof.Length),

            // Batch 6 - Stochastic variants
            DoubleStochasticOscillatorSpecOptions dso => ComputeDoubleStochasticOscillatorFast(data, context, dso.MaType, dso.Length),
            BilateralStochasticOscillatorSpecOptions bso => ComputeBilateralStochasticOscillatorFast(data, context, bso.Length,
                bso.MaType),
            FisherTransformStochasticOscillatorSpecOptions ftso => ComputeFisherTransformStochasticOscillatorFast(data, context, ftso.Length),
            StochasticCustomOscillatorSpecOptions sco => ComputeStochasticCustomOscillatorFast(data, context, sco.Length),
            FastSlowStochasticOscillatorSpecOptions fsso => ComputeFastSlowStochasticOscillatorFast(data, context, fsso.Length),
            DiNapoliPreferredStochasticOscillatorSpecOptions dnpso => ComputeDiNapoliPreferredStochasticOscillatorFast(data, context, dnpso.Length),
            DMIStochasticSpecOptions dmis => ComputeDMIStochasticFast(data, context, dmis.Length, dmis.MaType),
            // Length is declared obsolete because CCTStochRelativeStrengthIndex has no parameter it could
            // set, so this spec asks for the same series the defaults give.
            CCTStochRelativeStrengthIndexSpecOptions => ComputeCCTStochRelativeStrengthIndexFast(data, context),

            // Batch 6 - DT/Dynamic oscillators
            DTOscillatorSpecOptions dto => ComputeDTOscillatorFast(data, context, dto.Length, dto.MaType),
            DynamicMomentumOscillatorSpecOptions dmo => ComputeDynamicMomentumOscillatorFast(data, context, dmo.Length, dmo.MaType),

            // Batch 6 - Price/Momentum oscillators
            ComparePriceMomentumOscillatorSpecOptions cpmo => ComputeComparePriceMomentumOscillatorFast(data, context, cpmo.Length),
            DailyAveragePriceDeltaSpecOptions dapd => ComputeDailyAveragePriceDeltaFast(data, context,
                dapd.Length, dapd.MaType),
            PriceCycleOscillatorSpecOptions pco => ComputePriceCycleOscillatorFast(data, context, pco.Length),
            PriceVolumeOscillatorSpecOptions pvo2 => ComputePriceVolumeOscillatorFast(data, context, pvo2.Length),
            PercentChangeOscillatorSpecOptions pchosc => ComputePercentChangeOscillatorFast(data, context, pchosc.Length),
            DecisionPointPriceMomentumOscillatorSpecOptions dppmo => ComputeDecisionPointPriceMomentumOscillatorFast(data, context, dppmo.Length),

            // Batch 6 - Demand/Volume oscillators
            // Length is declared obsolete because CalculateDemandOscillator has no parameter it could
            // set, so this spec asks for the same series the defaults give.
            DemandOscillatorSpecOptions demosc => ComputeDemandOscillatorFast(data, context, demosc.MaType),
            AverageMoneyFlowOscillatorSpecOptions amfo => ComputeAverageMoneyFlowOscillatorFast(data, context, amfo.Length,
                amfo.MaType),
            VolumeAccumulationOscillatorSpecOptions vao => ComputeVolumeAccumulationOscillatorFast(data, context, vao.Length),
            TFSVolumeOscillatorSpecOptions tfsvo => ComputeTFSVolumeOscillatorFast(data, context, tfsvo.Length),

            // Batch 6 - RSI variants
            DoubleSmoothedRelativeStrengthIndexSpecOptions dsrsi => ComputeDoubleSmoothedRelativeStrengthIndexFast(data, context, dsrsi.Length),
            FastSlowRsiOscillatorSpecOptions fsrsi => ComputeFastSlowRsiOscillatorFast(data, context, fsrsi.Length),

            // Batch 6 - DiNapoli/Ergodic oscillators
            DiNapoliPercentagePriceOscillatorSpecOptions dnppo => ComputeDiNapoliPercentagePriceOscillatorFast(data, context, dnppo.Length),
            ErgodicPercentagePriceOscillatorSpecOptions eppo => ComputeErgodicPercentagePriceOscillatorFast(data, context, eppo.Length),
            ImpulsePercentagePriceOscillatorSpecOptions ippo => ComputeImpulsePercentagePriceOscillatorFast(data, context, ippo.Length),
            MirroredPercentagePriceOscillatorSpecOptions mppo => ComputeMirroredPercentagePriceOscillatorFast(data, context, mppo.Length),
            PercentagePriceOscillatorLeaderSpecOptions ppol => ComputePercentagePriceOscillatorLeaderFast(data, context, ppol.Length),
            TFSMboPercentagePriceOscillatorSpecOptions tfsppo => ComputeTFSMboPercentagePriceOscillatorFast(data, context, tfsppo.Length),

            // Batch 6 - Kurtosis/Degree oscillators
            FastSlowKurtosisOscillatorSpecOptions fsko => ComputeFastSlowKurtosisOscillatorFast(data, context, fsko.Length),
            FastSlowDegreeOscillatorSpecOptions fsdo => ComputeFastSlowDegreeOscillatorFast(data, context, fsdo.Length),

            // Batch 6 - Gann oscillators
            GOscillatorSpecOptions gosc => ComputeGOscillatorFast(data, context, gosc.Length),
            GannSwingOscillatorSpecOptions gswo => ComputeGannSwingOscillatorFast(data, context, gswo.Length),
            GannTrendOscillatorSpecOptions gto => ComputeGannTrendOscillatorFast(data, context, gto.Length),

            // Batch 6 - Special oscillators
            FireflyOscillatorSpecOptions ffo => ComputeFireflyOscillatorFast(data, context, ffo.Length),
            KarobeinOscillatorSpecOptions kbo => ComputeKarobeinOscillatorFast(data, context, kbo.Length),
            GroverLlorensCycleOscillatorSpecOptions glco => ComputeGroverLlorensCycleOscillatorFast(data, context, glco.Length),
            LindaRaschke310OscillatorSpecOptions lr310 => ComputeLindaRaschke310OscillatorFast(data, context, lr310.FastLength),
            MidpointOscillatorSpecOptions mpo => ComputeMidpointOscillatorFast(data, context, mpo.Length),
            MobilityOscillatorSpecOptions mobo => ComputeMobilityOscillatorFast(data, context, mobo.Length),

            // Batch 6 - Projection/Regression oscillators
            ProjectionOscillatorSpecOptions projo => ComputeProjectionOscillatorFast(data, context, projo.Length),
            RainbowOscillatorSpecOptions rbo => ComputeRainbowOscillatorFast(data, context, rbo.Length),
            RegressionOscillatorSpecOptions regro => ComputeRegressionOscillatorFast(data, context, regro.Length),
            RexOscillatorSpecOptions rexo => ComputeRexOscillatorFast(data, context, rexo.Length),

            // Batch 6 - Sentiment/Zone oscillators
            SentimentZoneOscillatorSpecOptions szo => ComputeSentimentZoneOscillatorFast(data, context, szo.Length),
            WaveTrendOscillatorSpecOptions wto => ComputeWaveTrendOscillatorFast(data, context, wto.Length),
            WamiOscillatorSpecOptions wami => ComputeWamiOscillatorFast(data, context, wami.Length),

            // Batch 6 - Kase oscillators
            KasePeakOscillatorV1SpecOptions kpo1 => ComputeKasePeakOscillatorV1Fast(data, context, kpo1.Length),
            KasePeakOscillatorV2SpecOptions kpo2 => ComputeKasePeakOscillatorV2Fast(data, context, kpo2.Length),

            // Batch 6 - Mathematical oscillators
            VaradiOscillatorSpecOptions varosc => ComputeVaradiOscillatorFast(data, context, varosc.Length),
            PrimeNumberOscillatorSpecOptions pno => ComputePrimeNumberOscillatorFast(data, context, pno.Length),
            TrigonometricOscillatorSpecOptions trigo => ComputeTrigonometricOscillatorFast(data, context, trigo.Length),
            UltimateTraderOscillatorSpecOptions uto => ComputeUltimateTraderOscillatorFast(data, context, uto.Length),
            SmoothedDeltaRatioOscillatorSpecOptions sdro => ComputeSmoothedDeltaRatioOscillatorFast(data, context, sdro.Length),
            RobustWeightingOscillatorSpecOptions rwo => ComputeRobustWeightingOscillatorFast(data, context, rwo.Length),

            // Batch 6 - Detector/Pivot oscillators
            PivotDetectorOscillatorSpecOptions pdo => ComputePivotDetectorOscillatorFast(data, context, pdo.Length),
            TickLineMomentumOscillatorSpecOptions tlmo => ComputeTickLineMomentumOscillatorFast(data, context, tlmo.Length),
            SupportAndResistanceOscillatorSpecOptions saro => ComputeSupportAndResistanceOscillatorFast(data, context, saro.Length),
            TradingMadeMoreSimplerOscillatorSpecOptions tmmso => ComputeTradingMadeMoreSimplerOscillatorFast(data, context, tmmso.Length),
            NthOrderDifferencingOscillatorSpecOptions nodo => ComputeNthOrderDifferencingOscillatorFast(data, context, nodo.Length),
            OscOscillatorSpecOptions osco => ComputeOscOscillatorFast(data, context, osco.Length),

            // Batch 6 - Ehlers oscillators
            EhlersCenterOfGravityOscillatorSpecOptions ecogo => ComputeEhlersCenterOfGravityOscillatorFast(data, context, ecogo.Length),
            EhlersDecyclerOscillatorV1SpecOptions edov1 => ComputeEhlersDecyclerOscillatorV1Fast(data, context, edov1.Length),
            EhlersDecyclerOscillatorV2SpecOptions edov2 => ComputeEhlersDecyclerOscillatorV2Fast(data, context, edov2.FastLength, edov2.MaType, edov2.SlowLength),
            EhlersHilbertOscillatorSpecOptions eho => spec.OutputKey switch
            {
                null or "IQ" => ComputeEhlersHilbertOscillatorFast(data, context, eho.Length),
                "I3" => ComputeEhlersHilbertOscillatorFast(data, context, eho.Length, EhlersHilbertOutput.InPhase),
                _ => null
            },
            EhlersUniversalOscillatorSpecOptions euo => ComputeEhlersUniversalOscillatorFast(data, context, euo.Length),
            // The oscillator's three lengths are all fixed in the batch, so the spec's single Length has
            // nothing to bind to and is marked as having no effect.
            EhlersRecursiveMedianOscillatorSpecOptions ermo => ComputeEhlersRecursiveMedianOscillatorFast(data, context),
            EhlersStochasticCenterOfGravityOscillatorSpecOptions escogo => ComputeEhlersStochasticCenterOfGravityOscillatorFast(data, context, escogo.Length),
            EhlersFisherizedDeviationScaledOscillatorSpecOptions efdso => ComputeEhlersFisherizedDeviationScaledOscillatorFast(data, context, efdso.Length),
            EhlersAdaptiveCenterOfGravityOscillatorSpecOptions eacogo => ComputeEhlersAdaptiveCenterOfGravityOscillatorFast(data, context, eacogo.Length),

            // Batch 6 - Vervoort oscillators
            VervoortSmoothedOscillatorSpecOptions vso => ComputeVervoortSmoothedOscillatorFast(data, context, vso.Length),
            VervoortHeikenAshiCandlestickOscillatorSpecOptions vhaco => ComputeVervoortHeikenAshiCandlestickOscillatorFast(data, context, vhaco.Length),
            VervoortHeikenAshiLongTermCandlestickOscillatorSpecOptions vhaltco => ComputeVervoortHeikenAshiLongTermCandlestickOscillatorFast(data, context, vhaltco.Length),

            // Batch 6 - Convergence/Divergence oscillators
            RelativeDifferenceOfSquaresOscillatorSpecOptions rdoso => ComputeRelativeDifferenceOfSquaresOscillatorFast(data, context, rdoso.Length),
            LinearQuadraticConvergenceDivergenceOscillatorSpecOptions lqcdo => ComputeLinearQuadraticConvergenceDivergenceOscillatorFast(data, context, lqcdo.Length),
            StationaryExtrapolatedLevelsOscillatorSpecOptions selo => ComputeStationaryExtrapolatedLevelsOscillatorFast(data, context, selo.Length),

            // Batch 6 - Kaufman/MACD oscillators
            KaufmanAdaptiveCorrelationOscillatorSpecOptions kaco => ComputeKaufmanAdaptiveCorrelationOscillatorFast(data, context, kaco.Length),
            StochasticMacdOscillatorSpecOptions smo => ComputeStochasticMacdOscillatorFast(data, context, smo.Length),
            McClellanOscillatorSpecOptions mcco => ComputeMcClellanOscillatorFast(data, context, mcco.Length),

            // Batch 6 - Decision Point/Swenlin oscillators
            DecisionPointBreadthSwenlinTradingOscillatorSpecOptions dpbsto => ComputeDecisionPointBreadthSwenlinTradingOscillatorFast(data, context, dpbsto.Length),

            // Batch 6 - Mass Thrust oscillator
            MassThrustOscillatorSpecOptions mto => ComputeMassThrustOscillatorFast(data, context, mto.Length),

            // Batch 7 - Moving averages
            UltimateMovingAverageSpecOptions uma => ComputeUltimateMovingAverageFast(data, context, uma.Length),
            SymmetricallyWeightedMovingAverageSpecOptions swma2 => ComputeSymmetricallyWeightedMovingAverageFast(data, context, swma2.Length),
            SquareRootWeightedMovingAverageSpecOptions srwma => ComputeSquareRootWeightedMovingAverageFast(data, context, srwma.Length),
            Spencer15PointMovingAverageSpecOptions sp15 => ComputeSpencer15PointMovingAverageFast(data, context, sp15.Length),
            Spencer21PointMovingAverageSpecOptions sp21 => ComputeSpencer21PointMovingAverageFast(data, context, sp21.Length),
            SlowSmoothedMovingAverageSpecOptions ssma => ComputeSlowSmoothedMovingAverageFast(data, context, ssma.Length),
            RepulsionMovingAverageSpecOptions rema => ComputeRepulsionMovingAverageFast(data, context, rema.Length),
            QuickMovingAverageSpecOptions qma => ComputeQuickMovingAverageFast(data, context, qma.Length),

            // Batch 7 - Ehlers MAs
            EhlersBetterExponentialMovingAverageSpecOptions ebema => ComputeEhlersBetterExponentialMovingAverageFast(data, context, ebema.Length),
            EhlersDeviationScaledMovingAverageSpecOptions edsma => ComputeEhlersDeviationScaledMovingAverageFast(data, context, edsma.Length),
            EhlersHannMovingAverageSpecOptions ehma => ComputeEhlersHannMovingAverageFast(data, context, ehma.Length),
            EhlersTriangleMovingAverageSpecOptions etma => ComputeEhlersTriangleMovingAverageFast(data, context, etma.Length),

            // Batch 7 - Volume weighted/exponential MAs
            ElasticVolumeWeightedMovingAverageV1SpecOptions evwma => ComputeElasticVolumeWeightedMovingAverageV1Fast(data, context, evwma.Length),
            HoltExponentialMovingAverageSpecOptions hema => ComputeHoltExponentialMovingAverageFast(data, context, hema.Length),
            PentupleExponentialMovingAverageSpecOptions pema => ComputePentupleExponentialMovingAverageFast(data, context, pema.Length),
            QuadrupleExponentialMovingAverageSpecOptions qema => ComputeQuadrupleExponentialMovingAverageFast(data, context, qema.Length),

            // Batch 7 - Ichimoku components
            IchimokuSenkouSpanASpecOptions issa => ComputeIchimokuSenkouSpanAFast(data, context, issa.Length),
            IchimokuSenkouSpanBSpecOptions issb => ComputeIchimokuSenkouSpanBFast(data, context, issb.Length),
            IchimokuChikouSpanSpecOptions icsp => ComputeIchimokuChikouSpanFast(data, context, icsp.Length),

            // Batch 7 - Williams fractals
            WilliamsFractalUpSpecOptions wfu => ComputeWilliamsFractalUpFast(data, context, wfu.Length),
            WilliamsFractalDownSpecOptions wfd => ComputeWilliamsFractalDownFast(data, context, wfd.Length),

            // Batch 7 - Alligator components
            AlligatorJawSpecOptions aj => ComputeAlligatorJawFast(data, context, aj.Length),
            AlligatorTeethSpecOptions at => ComputeAlligatorTeethFast(data, context, at.Length),
            AlligatorLipsSpecOptions al2 => ComputeAlligatorLipsFast(data, context, al2.Length),

            // Batch 7 - Ehlers Laguerre
            EhlersLaguerreFilterSpecOptions elf => ComputeEhlersLaguerreFilterFast(data, context, elf.Length),
            EhlersLaguerreRsiSpecOptions elrsi => ComputeEhlersLaguerreRsiFast(data, context, elrsi.Length),
            EhlersZeroLagEmaSpecOptions ezle => ComputeEhlersZeroLagEmaFast(data, context, ezle.Length),
            EhlersFramaSpecOptions eframa => ComputeEhlersFramaFast(data, context, eframa.Length),
            // MaType drives both the relative strength index and the smoothing of its rescaled output,
            // so it does reach the bound series and is forwarded. Length2 is fixed by the batch.
            EhlersInverseFisherTransformSpecOptions eift => ComputeEhlersInverseFisherTransformFast(data, context, eift.Length,
                maType: eift.MaType),
            EhlersCyberCycleSpecOptions ecc => ComputeEhlersCyberCycleFast(data, context, ecc.Length),
            EhlersStochasticSpecOptions esto => ComputeEhlersStochasticFast(data, context, esto.Length),
            EhlersAdaptiveLaguerreFilterSpecOptions ealf => ComputeEhlersAdaptiveLaguerreFilterFast(data, context, ealf.Length),

            // Batch 7 - Trend/Filter indicators
            CoralTrendIndicatorSpecOptions cti => ComputeCoralTrendIndicatorFast(data, context, cti.Length),
            DampedSineWaveWeightedFilterSpecOptions dswf => ComputeDampedSineWaveWeightedFilterFast(data, context, dswf.Length),
            FibonacciWeightedMovingAverageSpecOptions fwma => ComputeFibonacciWeightedMovingAverageFast(data, context, fwma.Length),
            GeneralizedDoubleEmaSpecOptions gdema => ComputeGeneralizedDoubleEmaFast(data, context, gdema.Length),
            GeometricMeanMovingAverageSpecOptions gmma => ComputeGeometricMeanMovingAverageFast(data, context, gmma.Length),
            HarmonicMeanMovingAverageSpecOptions hmma => ComputeHarmonicMeanMovingAverageFast(data, context, hmma.Length),

            // Batch 7 - Ehlers Butterworth filters
            Ehlers2PoleButterworthFilterV1SpecOptions e2pbv1 => ComputeEhlers2PoleButterworthFilterV1Fast(data, context, e2pbv1.Length),
            Ehlers2PoleButterworthFilterV2SpecOptions e2pbv2 => ComputeEhlers2PoleButterworthFilterV2Fast(data, context, e2pbv2.Length),
            Ehlers3PoleButterworthFilterV1SpecOptions e3pbv1 => ComputeEhlers3PoleButterworthFilterV1Fast(data, context, e3pbv1.Length),
            Ehlers3PoleButterworthFilterV2SpecOptions e3pbv2 => ComputeEhlers3PoleButterworthFilterV2Fast(data, context, e3pbv2.Length),

            // Batch 7 - Ehlers Super Smoother filters
            Ehlers2PoleSuperSmootherFilterV1SpecOptions e2pssv1 => ComputeEhlers2PoleSuperSmootherFilterV1Fast(data, context, e2pssv1.Length),
            Ehlers2PoleSuperSmootherFilterV2SpecOptions e2pssv2 => ComputeEhlers2PoleSuperSmootherFilterV2Fast(data, context, e2pssv2.Length),
            Ehlers3PoleSuperSmootherFilterSpecOptions e3pss => ComputeEhlers3PoleSuperSmootherFilterFast(data, context, e3pss.Length),

            // Batch 7 - More Ehlers filters
            EhlersDecyclerSpecOptions edec => ComputeEhlersDecyclerFast(data, context, edec.Length),
            EhlersHammingMovingAverageSpecOptions ehmma => ComputeEhlersHammingMovingAverageFast(data, context, ehmma.Length),
            EhlersLeadingIndicatorSpecOptions eli => ComputeEhlersLeadingIndicatorFast(data, context, eli.Length),
            EhlersHighPassFilterV1SpecOptions ehpv1 => ComputeEhlersHighPassFilterV1Fast(data, context, ehpv1.Length),
            EhlersHighPassFilterV2SpecOptions ehpv2 => ComputeEhlersHighPassFilterV2Fast(data, context, ehpv2.Length, ehpv2.MaType),
            DistanceWeightedMovingAverageSpecOptions dwma => ComputeDistanceWeightedMovingAverageFast(data, context, dwma.Length),
            EhlersFilterSpecOptions efilter => ComputeEhlersFilterFast(data, context, efilter.Length),
            // The finite impulse response filter is a fixed seven-tap weighted average; it has no length
            // parameter, which is why both specs mark Length as having no effect.
            EhlersFirFilterSpecOptions efir => ComputeEhlersFirFilterFast(data, context),
            EhlersIirFilterSpecOptions eiir => ComputeEhlersIirFilterFast(data, context, eiir.Length),

            // Batch 7 - Cycle indicators
            SimpleCycleSpecOptions scyc => ComputeSimpleCycleFast(data, context, scyc.Length),
            SimpleLinesSpecOptions slines => ComputeSimpleLinesFast(data, context, slines.Length, slines.Multiplier),
            DoubleExponentialSmoothingSpecOptions => ComputeDoubleExponentialSmoothingFast(data, context),
            DetrendedSyntheticPriceSpecOptions dsp => ComputeDetrendedSyntheticPriceFast(data, context, dsp.Length),

            // Batch 7 - Timing/Setup indicators
            BelkhayateTimingSpecOptions beltim => ComputeBelkhayateTimingFast(data, context, beltim.Length),
            DemarkSetupIndicatorSpecOptions dmksetup => ComputeDemarkSetupIndicatorFast(data, context, dmksetup.Length),
            PerformanceIndexSpecOptions perfidx => ComputePerformanceIndexFast(data, context, perfidx.Length),
            PsychologicalLineSpecOptions psyline => ComputePsychologicalLineFast(data, context, psyline.Length),

            // Batch 7 - Market indicators
            MoveTrackerSpecOptions mvtrk => ComputeMoveTrackerFast(data, context, mvtrk.Length),
            MultiLevelIndicatorSpecOptions mli => ComputeMultiLevelIndicatorFast(data, context, mli.Length),
            MarketDirectionIndicatorSpecOptions mdi => ComputeMarketDirectionIndicatorFast(data, context, mdi.Length),
            MorphedSineWaveSpecOptions msw => ComputeMorphedSineWaveFast(data, context, msw.Length),

            // Batch 7 - Price/Statistical indicators
            FullTypicalPriceSpecOptions ftp => ComputeFullTypicalPriceFast(data, context, ftp.Length),
            InternalBarStrengthIndicatorSpecOptions ibs => ComputeInternalBarStrengthIndicatorFast(data, context, ibs.Length),
            ZScoreSpecOptions zscore => ComputeZScoreFast(data, context, zscore.Length),
            FastZScoreSpecOptions fzscore => ComputeFastZScoreFast(data, context, fzscore.Length),
            KurtosisIndicatorSpecOptions kurtosis => ComputeKurtosisIndicatorFast(data, context, kurtosis.Length),

            // Batch 7 - Demark indicators
            DemarkRangeExpansionIndexSpecOptions dmkrei => ComputeDemarkRangeExpansionIndexFast(data, context, dmkrei.Length),
            DemarkPressureRatioV1SpecOptions dmkprv1 => ComputeDemarkPressureRatioV1Fast(data, context, dmkprv1.Length),
            DemarkPressureRatioV2SpecOptions dmkprv2 => ComputeDemarkPressureRatioV2Fast(data, context, dmkprv2.Length),
            DemarkReversalPointsSpecOptions dmkrp => ComputeDemarkReversalPointsFast(data, context, dmkrp.Length1, dmkrp.Length2),

            // Batch 8 - Final (Channel widths, Core methods, RMO)
            BollingerBandsWidthSpecOptions bbw => ComputeBollingerBandsWidthFast(data, context, bbw.Length),
            DonchianChannelWidthSpecOptions dcw => ComputeDonchianChannelWidthFast(data, context, dcw.Length),
            KeltnerChannelWidthSpecOptions kcw => ComputeKeltnerChannelWidthFast(data, context, kcw.Length),
            MassIndexCoreSpecOptions mic => ComputeMassIndexCoreFast(data, context, mic.Length),
            RahulMohindarOscillatorSpecOptions rmo => ComputeRahulMohindarOscillatorFast(data, context, rmo.Length),
            RviVolatilitySpecOptions rviv => ComputeRviVolatilityFast(data, context, rviv.Length),
            StandardErrorCoreSpecOptions sec => ComputeStandardErrorCoreFast(data, context, sec.Length),

            // Batch 25 - Additional Moving Averages (Unwired Core Methods)
            AdaptiveAutonomousRecursiveMovingAverageSpecOptions aarmao => ComputeAdaptiveAutonomousRecursiveMovingAverageFast(data, context, aarmao.Length, aarmao.Lambda),
            CorrectedMovingAverageSpecOptions cma => ComputeCorrectedMovingAverageFast(data, context, cma.Length,
                cma.MaType),
            CubedWeightedMovingAverageSpecOptions cwma => ComputeCubedWeightedMovingAverageFast(data, context, cwma.Length),
            DynamicallyAdjustableFilterSpecOptions daf => ComputeDynamicallyAdjustableFilterFast(data, context, daf.Length),
            EdgePreservingFilterSpecOptions epf => ComputeEdgePreservingFilterFast(data, context, epf.Length, epf.MaType),
            EhlersAllPassPhaseShifterSpecOptions eapps => ComputeEhlersAllPassPhaseShifterFast(data, context, eapps.Length),
            EhlersAverageErrorFilterSpecOptions eaef => ComputeEhlersAverageErrorFilterFast(data, context, eaef.Length),
            EhlersDistanceCoefficientFilterSpecOptions edcf => ComputeEhlersDistanceCoefficientFilterFast(data, context, edcf.Length),
            EhlersKaufmanAdaptiveMovingAverageSpecOptions ekama => ComputeEhlersKaufmanAdaptiveMovingAverageFast(data, context, ekama.Length),
            EhlersModifiedOptimumEllipticFilterSpecOptions emoef => ComputeEhlersModifiedOptimumEllipticFilterFast(data, context, emoef.Length),
            EhlersNoiseEliminationTechnologySpecOptions enet => ComputeEhlersNoiseEliminationTechnologyFast(data, context, enet.Length),
            EhlersOptimumEllipticFilterSpecOptions eoef => ComputeEhlersOptimumEllipticFilterFast(data, context, eoef.Length),
            EhlersVariableIndexDynamicAverageSpecOptions evidao => ComputeEhlersVariableIndexDynamicAverageFast(data, context, evidao.Length),
            FallingRisingFilterSpecOptions frf => ComputeFallingRisingFilterFast(data, context, frf.Length),
            FareySequenceWeightedMovingAverageSpecOptions fswma => ComputeFareySequenceWeightedMovingAverageFast(data, context, fswma.Length),
            FisherLeastSquaresMovingAverageSpecOptions flsma => ComputeFisherLeastSquaresMovingAverageFast(data, context, flsma.Length),
            FollowingAdaptiveMovingAverageSpecOptions fama => spec.OutputKey switch
            {
                null or "Fama" => ComputeFollowingAdaptiveMovingAverageFast(data, context),
                _ => null
            },
            GeneralFilterEstimatorSpecOptions gfe => ComputeGeneralFilterEstimatorFast(data, context, gfe.Length),
            HendersonWeightedMovingAverageSpecOptions hwma => ComputeHendersonWeightedMovingAverageFast(data, context, hwma.Length),
            HullEstimateSpecOptions hest => ComputeHullEstimateFast(data, context, hest.Length),
            HybridConvolutionFilterSpecOptions hcf => ComputeHybridConvolutionFilterFast(data, context, hcf.Length),
            IIRLeastSquaresEstimateSpecOptions iirls => ComputeIIRLeastSquaresEstimateFast(data, context, iirls.Length),
            InverseDistanceWeightedMovingAverageSpecOptions idwma => ComputeInverseDistanceWeightedMovingAverageFast(data, context, idwma.Length),
            InverseFisherTransformCoreSpecOptions iftc => ComputeInverseFisherTransformCoreFast(data, context, iftc.Length),
            JsaMovingAverageSpecOptions jsama => ComputeJsaMovingAverageFast(data, context, jsama.Length),
            KalmanSmootherSpecOptions ksmo => ComputeKalmanSmootherFast(data, context, ksmo.Length),
            KaufmanAdaptiveLeastSquaresMovingAverageSpecOptions kalsma => ComputeKaufmanAdaptiveLeastSquaresMovingAverageFast(data, context, kalsma.Length),
            LeoMovingAverageSpecOptions leoma => ComputeLeoMovingAverageFast(data, context, leoma.Length),
            LightLeastSquaresMovingAverageSpecOptions llsma => ComputeLightLeastSquaresMovingAverageFast(data, context, llsma.Length),
            LinearExtrapolationSpecOptions lextra => ComputeLinearExtrapolationFast(data, context, lextra.Length),
            LinearRegressionLineSpecOptions lrline => ComputeLinearRegressionLineFast(data, context, lrline.Length),
            LinearWeightedMovingAverageCoreSpecOptions lwmac => ComputeLinearWeightedMovingAverageCoreFast(data, context, lwmac.Length),
            McNichollMovingAverageSpecOptions mcnma => ComputeMcNichollMovingAverageFast(data, context, mcnma.Length),
            MovingAverageAdaptiveQSpecOptions maaq => ComputeMovingAverageAdaptiveQFast(data, context, maaq.Length),
            MovingAverageV3SpecOptions mav3 => ComputeMovingAverageV3Fast(data, context, mav3.Length),
            OneLCLeastSquaresMovingAverageSpecOptions olclsma => ComputeOneLCLeastSquaresMovingAverageFast(data, context, olclsma.Length),
            OptimalWeightedMovingAverageSpecOptions owma => ComputeOptimalWeightedMovingAverageFast(data, context, owma.Length),
            OvershootReductionMovingAverageSpecOptions orma => ComputeOvershootReductionMovingAverageFast(data, context, orma.Length),
            ParametricCorrectiveLinearMovingAverageSpecOptions pclma => ComputeParametricCorrectiveLinearMovingAverageFast(data, context, pclma.Length),
            ParametricKalmanFilterSpecOptions pkf => ComputeParametricKalmanFilterFast(data, context, pkf.Length),

            // Batch 26 - Additional Unwired Core Methods
            ZeroLowLagMovingAverageSpecOptions zllma => ComputeZeroLowLagMovingAverageFast(data, context, zllma.Length),
            RecursiveMovingTrendAverageSpecOptions rmta => ComputeRecursiveMovingTrendAverageFast(data, context, rmta.Length),
            TrimeanSpecOptions trimean => ComputeTrimeanFast(data, context, trimean.Length),
            SkewnessSpecOptions skew => ComputeSkewnessFast(data, context, skew.Length),
            HampelFilterSpecOptions hampel => ComputeHampelFilterFast(data, context, hampel.Length, hampel.ScalingFactor),
            ModularFilterSpecOptions modf => ComputeModularFilterFast(data, context, modf.Length, modf.Beta, modf.Z),
            DynamicallyAdjustableMovingAverageSpecOptions dama => ComputeDynamicallyAdjustableMovingAverageFast(data, context, dama.FastLength, dama.SlowLength),
            EquityMovingAverageSpecOptions eqma => ComputeEquityMovingAverageFast(data, context, eqma.Length),
            MultiDepthZeroLagExponentialMovingAverageSpecOptions mdzlema => ComputeMultiDepthZeroLagExponentialMovingAverageFast(data, context, mdzlema.Length),
            PolynomialLeastSquaresMovingAverageSpecOptions plsma => ComputePolynomialLeastSquaresMovingAverageFast(data, context, plsma.Length),
            PoweredKaufmanAdaptiveMovingAverageSpecOptions pkama => ComputePoweredKaufmanAdaptiveMovingAverageFast(data, context, pkama.Length),
            QuadraticLeastSquaresMovingAverageSpecOptions qlsma => ComputeQuadraticLeastSquaresMovingAverageFast(data, context, qlsma.Length),
            QuadraticMovingAverageSpecOptions qma => ComputeQuadraticMovingAverageFast(data, context, qma.Length),
            QuadraticRegressionSpecOptions qreg => ComputeQuadraticRegressionFast(data, context, qreg.Length),
            R2AdaptiveRegressionSpecOptions r2ar => ComputeR2AdaptiveRegressionFast(data, context, r2ar.Length),
            RetentionAccelerationFilterSpecOptions raf => ComputeRetentionAccelerationFilterFast(data, context, raf.Length),
            RightSidedRickerMovingAverageSpecOptions rsrma => ComputeRightSidedRickerMovingAverageFast(data, context, rsrma.Length),
            SelfWeightedMovingAverageSpecOptions swma => ComputeSelfWeightedMovingAverageFast(data, context, swma.Length),
            SequentiallyFilteredMovingAverageSpecOptions sfma => ComputeSequentiallyFilteredMovingAverageFast(data, context, sfma.Length),
            SettingLessTrendStepFilteringSpecOptions sltsf => ComputeSettingLessTrendStepFilteringFast(data, context, sltsf.Length),
            ShapeshiftingMovingAverageSpecOptions ssma => ComputeShapeshiftingMovingAverageFast(data, context, ssma.Length),
            SharpModifiedMovingAverageSpecOptions shpma => ComputeSharpModifiedMovingAverageFast(data, context, shpma.Length),
            SimplifiedLeastSquaresMovingAverageSpecOptions slsma => ComputeSimplifiedLeastSquaresMovingAverageFast(data, context, slsma.Length),
            SimplifiedWeightedMovingAverageSpecOptions simpwma => ComputeSimplifiedWeightedMovingAverageFast(data, context, simpwma.Length),
            SvamaSpecOptions svama => ComputeSvamaFast(data, context, svama.Length),
            ThreeHMASpecOptions thma => ComputeThreeHMAFast(data, context, thma.Length),
            TillsonIE2SpecOptions tie2 => ComputeTillsonIE2Fast(data, context, tie2.Length),
            TStepLeastSquaresMovingAverageSpecOptions tslsma => ComputeTStepLeastSquaresMovingAverageFast(data, context, tslsma.Length),
            VariableAdaptiveMovingAverageSpecOptions vama => ComputeVariableAdaptiveMovingAverageFast(data, context, vama.Length),
            VariableLengthMovingAverageSpecOptions vlma => ComputeVariableLengthMovingAverageFast(data, context, vlma.Length),
            VerticalHorizontalMovingAverageSpecOptions vhma => ComputeVerticalHorizontalMovingAverageFast(data, context, vhma.Length),
            VolatilityMovingAverageSpecOptions volma => ComputeVolatilityMovingAverageFast(data, context, volma.Length),
            VolatilityWaveMovingAverageSpecOptions vwma => ComputeVolatilityWaveMovingAverageFast(data, context, vwma.Length),
            WellRoundedMovingAverageSpecOptions wrma => ComputeWellRoundedMovingAverageFast(data, context, wrma.Length),
            WildersSummationMethodSpecOptions wsm => ComputeWildersSummationMethodFast(data, context, wsm.Length),
            ZeroLagTripleExponentialMovingAverageSpecOptions zltema => ComputeZeroLagTripleExponentialMovingAverageFast(data, context, zltema.Length),

            // Batch 27 - Multi-Input Core Methods
            DeMarkerSpecOptions dem => ComputeDeMarkerFast(data, context, dem.Length),
            MiddleHighLowMovingAverageSpecOptions mhlma => ComputeMiddleHighLowMovingAverageFast(data, context, mhlma.Length1,
                mhlma.Length2, mhlma.MaType),
            VortexMinusSpecOptions vminus => ComputeVortexMinusFast(data, context, vminus.Length),
            VortexPlusSpecOptions vplus => ComputeVortexPlusFast(data, context, vplus.Length),
            VolumeWeightedMovingAverageSpecOptions vwma27 => ComputeVolumeWeightedMovingAverageFast(data, context, vwma27.Length),
            KlingerSignalSpecOptions ksig => ComputeKlingerSignalFast(data, context, ksig.FastLength, ksig.SlowLength, ksig.SignalLength),
            EhlersChebyshevLowPassFilterSpecOptions eclpf => ComputeEhlersChebyshevLowPassFilterFast(data, context, eclpf.Length, eclpf.Ripple),
            EhlersGaussianFilterSpecOptions egf => ComputeEhlersGaussianFilterFast(data, context, egf.Length, egf.Poles),
            EhlersMedianAverageAdaptiveFilterSpecOptions emaaf => ComputeEhlersMedianAverageAdaptiveFilterFast(data, context, emaaf.Length, emaaf.Threshold),
            EhlersMesaAdaptiveMovingAverageSpecOptions emama => spec.OutputKey switch
            {
                null or "Mama" => ComputeEhlersMesaAdaptiveMovingAverageFast(data, context, emama.FastLimit, emama.SlowLimit),
                "Fama" => ComputeEhlersMesaAdaptiveMovingAverageFast(data, context, emama.FastLimit, emama.SlowLimit,
                    EhlersMamaOutput.Fama),
                "I1" => ComputeEhlersMesaAdaptiveMovingAverageFast(data, context, emama.FastLimit, emama.SlowLimit,
                    EhlersMamaOutput.InPhase),
                "Q1" => ComputeEhlersMesaAdaptiveMovingAverageFast(data, context, emama.FastLimit, emama.SlowLimit,
                    EhlersMamaOutput.Quadrature),
                "SmoothPeriod" => ComputeEhlersMesaAdaptiveMovingAverageFast(data, context, emama.FastLimit, emama.SlowLimit,
                    EhlersMamaOutput.SmoothPeriod),
                "Smooth" => ComputeEhlersMesaAdaptiveMovingAverageFast(data, context, emama.FastLimit, emama.SlowLimit,
                    EhlersMamaOutput.Smooth),
                "Real" => ComputeEhlersMesaAdaptiveMovingAverageFast(data, context, emama.FastLimit, emama.SlowLimit,
                    EhlersMamaOutput.Real),
                "Imag" => ComputeEhlersMesaAdaptiveMovingAverageFast(data, context, emama.FastLimit, emama.SlowLimit,
                    EhlersMamaOutput.Imaginary),
                _ => null
            },
            // Alpha is derived from the smoothing length inside the filter; the batch takes no alpha.
            EhlersRecursiveMedianFilterSpecOptions ermf => ComputeEhlersRecursiveMedianFilterFast(data, context, ermf.Length),
            EhlersRoofingFilterSpecOptions eroof => ComputeEhlersRoofingFilterFast(data, context, eroof.HpLength, eroof.LpLength),

            // Batch 28
            // Poles is marked as having no effect: the cutoff is set per bar by the scaled deviation,
            // so there is no pole count for it to choose.
            EhlersDeviationScaledSuperSmootherSpecOptions edsss => ComputeEhlersDeviationScaledSuperSmootherFast(data, context, edsss.Length, edsss.MaType),
            PpoMaSpecOptions ppoma => ComputePpoMaFast(data, context, ppoma.FastLength, ppoma.SlowLength),
            PriceOscillatorSpecOptions posc => ComputePriceOscillatorFast(data, context, posc.ShortLength, posc.LongLength),
            ReverseEngineeringRsiSpecOptions rersi => ComputeReverseEngineeringRsiFast(data, context, rersi.Length, rersi.RsiLevel),
            ReverseMovingAverageConvergenceDivergenceSpecOptions rmacd => ComputeReverseMovingAverageConvergenceDivergenceFast(data, context, rmacd.FastLength, rmacd.SlowLength, rmacd.MacdLevel),
            SimplePriceZoneSpecOptions spz => ComputeSimplePriceZoneFast(data, context, spz.Length),
            StochasticRsiOscillatorSpecOptions srsio => ComputeStochasticRsiFast(data, context, srsio.RsiLength,
                stochLength: srsio.StochLength),
            ElasticVolumeWeightedMovingAverageV2SpecOptions evwma2 => ComputeElasticVolumeWeightedMovingAverageV2Fast(data, context, evwma2.Length),
            WindowedVolumeWeightedMovingAverageSpecOptions wvwma => ComputeWindowedVolumeWeightedMovingAverageFast(data, context, wvwma.Length),
            AtrFilteredExponentialMovingAverageSpecOptions atrfema => ComputeAtrFilteredExponentialMovingAverageFast(data, context, atrfema.Length, atrfema.AtrLength, atrfema.StdDevLength, atrfema.LbLength, atrfema.Min),
            TrueRangeAdjustedExponentialMovingAverageSpecOptions trema => ComputeTrueRangeAdjustedExponentialMovingAverageFast(data, context, trema.Length, trema.Mult),
            RelativeVolatilityIndexHighSpecOptions rvih => ComputeRelativeVolatilityIndexHighFast(data, context, rvih.Length, rvih.StdDevLength),
            RelativeVolatilityIndexLowSpecOptions rvil => ComputeRelativeVolatilityIndexLowFast(data, context, rvil.Length, rvil.StdDevLength),
            TypicalPriceVolatilitySpecOptions tpv => ComputeTypicalPriceVolatilityFast(data, context, tpv.Length),
            RatioOchlAveragerSpecOptions _ => ComputeRatioOchlAveragerFast(data, context),

            // Batch 29 - Additional Missing Indicators
            TripleHullMovingAverageSpecOptions thma => ComputeTripleHullMovingAverageFast(data, context, thma.Length),

            // Batch 30 - Additional Missing Core Methods
            GeneralizedDoubleExponentialMovingAverageSpecOptions gdema => ComputeGeneralizedDoubleExponentialMovingAverageFast(data, context, gdema.Length, gdema.VolumeFactor),
            EhlersFiniteImpulseResponseFilterSpecOptions efirf => ComputeEhlersFiniteImpulseResponseFilterFast(data, context),
            EhlersInfiniteImpulseResponseFilterSpecOptions eiirf => ComputeEhlersInfiniteImpulseResponseFilterFast(data, context, eiirf.Length),
            VolumeAdjustedMovingAverageSpecOptions vama => ComputeVolumeAdjustedMovingAverageFast(data, context, vama.Length, vama.Factor),
            AverageDayRangeSpecOptions adr => ComputeAverageDayRangeFast(data, context, adr.Length),
            ChandeIntradayMomentumIndexSpecOptions cimi => ComputeChandeIntradayMomentumIndexFast(data, context, cimi.Length),
            ContractHighSpecOptions _ => ComputeContractHighFast(data, context),
            ContractLowSpecOptions _ => ComputeContractLowFast(data, context),
            OscarIndicatorSpecOptions oscar => ComputeOscarIndicatorFast(data, context, oscar.Length),
            NarrowBandpassFilterSpecOptions nbpf => ComputeNarrowBandpassFilterFast(data, context, nbpf.Length),
            TFSTetherLineSpecOptions tether => ComputeTFSTetherLineFast(data, context, tether.Length),
            WilliamsFractalsUpSpecOptions wfu => ComputeWilliamsFractalsUpFast(data, context, wfu.Length),
            WilliamsFractalsDownSpecOptions wfd => ComputeWilliamsFractalsDownFast(data, context, wfd.Length),
            UpsideDownsideVolumeSpecOptions udv => ComputeUpsideDownsideVolumeFast(data, context, udv.Length),
            VortexIndicatorPlusSpecOptions vip => ComputeVortexIndicatorPlusFast(data, context, vip.Length),
            VortexIndicatorMinusSpecOptions vim => ComputeVortexIndicatorMinusFast(data, context, vim.Length),
            GuppyCountBackLineSpecOptions gcbl => ComputeGuppyCountBackLineFast(data, context, gcbl.Length),
            EhlersTrendflexSpecOptions etf => ComputeEhlersTrendflexFast(data, context, etf.Length),
            EhlersReflexSpecOptions erf => ComputeEhlersReflexFast(data, context, erf.Length),
            EhlersCorrelationTrendIndicatorSpecOptions ecti => ComputeEhlersCorrelationTrendIndicatorFast(data, context, ecti.Length),
            TrendTriggerFactorSpecOptions ttf => ComputeTrendTriggerFactorFast(data, context, ttf.Length),
            TrendDetectionIndexSpecOptions tdi => ComputeTrendDetectionIndexFast(data, context, tdi.Length1, tdi.Length2),
            UberTrendIndicatorSpecOptions uti => ComputeUberTrendIndicatorFast(data, context, uti.Length),
            PercentageTrendSpecOptions pt => ComputePercentageTrendFast(data, context, pt.Length, pt.Pct),
            LiquidRelativeStrengthIndexSpecOptions lrsi => ComputeLiquidRelativeStrengthIndexFast(data, context, lrsi.Length),
            AsymmetricalRelativeStrengthIndexSpecOptions arsi => ComputeAsymmetricalRelativeStrengthIndexFast(data, context, arsi.Length),
            AverageAbsoluteErrorNormalizationSpecOptions aaen => ComputeAverageAbsoluteErrorNormalizationFast(data, context, aaen.Length),
            RecursiveStochasticSpecOptions rs => ComputeRecursiveStochasticFast(data, context, rs.Length, rs.Alpha),
            ShinoharaIntensityRatioASpecOptions sira => ComputeShinoharaIntensityRatioAFast(data, context, sira.Length),
            ShinoharaIntensityRatioBSpecOptions sirb => ComputeShinoharaIntensityRatioBFast(data, context, sirb.Length),
            RangeActionVerificationIndexSpecOptions ravi => ComputeRangeActionVerificationIndexFast(data, context, ravi.FastLength, ravi.SlowLength),
            WilliamsAccumulationDistributionSpecOptions _ => ComputeWilliamsAccumulationDistributionFast(data, context),
            TotalPowerIndicatorSpecOptions tpi => ComputeTotalPowerIndicatorFast(data, context, tpi.Length1, tpi.Length2),
            TurboTriggerSpecOptions tt => ComputeTurboTriggerFast(data, context, tt.Length, tt.PctMultiplier),
            TurboScalerSpecOptions ts => ComputeTurboScalerFast(data, context, ts.Length, ts.PctMultiplier),
            TTMScalperIndicatorSpecOptions _ => ComputeTTMScalperIndicatorFast(data, context),
            StrengthOfMovementSpecOptions som => ComputeStrengthOfMovementFast(data, context, som.Length1, som.Length2),
            ValueChartIndicatorSpecOptions vci => ComputeValueChartIndicatorFast(data, context, vci.Length, vci.NumAtrs),
            SellGravitationIndexSpecOptions sgi => ComputeSellGravitationIndexFast(data, context, sgi.Length),
            TFSTetherLineIndicatorSpecOptions tfs => ComputeTFSTetherLineIndicatorFast(data, context, tfs.Length),
            EhlersSimpleCycleIndicatorSpecOptions esci => ComputeEhlersSimpleCycleIndicatorFast(data, context, esci.Alpha),
            EhlersFisherTransformSpecOptions eft => ComputeEhlersFisherTransformFast(data, context, eft.Length),
            EhlersVossPredictiveFilterSpecOptions evpf => ComputeEhlersVossPredictiveFilterFast(data, context, evpf.Length, evpf.Predict, evpf.Bandwidth),
            EhlersSpearmanRankIndicatorSpecOptions esri => ComputeEhlersSpearmanRankIndicatorFast(data, context, esri.Length),
            EhlersCorrelationCycleIndicatorSpecOptions ecci => ComputeEhlersCorrelationCycleIndicatorFast(data, context, ecci.Length),
            EhlersCorrelationAngleIndicatorSpecOptions ecai => ComputeEhlersCorrelationAngleIndicatorFast(data, context, ecai.Length),
            EhlersTruncatedBandPassFilterSpecOptions etbpf => ComputeEhlersTruncatedBandPassFilterFast(data, context, etbpf.Length1, etbpf.Length2, etbpf.Bandwidth),
            EhlersSimpleDecyclerSpecOptions esd => ComputeEhlersSimpleDecyclerFast(data, context, esd.Length),
            EhlersEvenBetterSineWaveIndicatorSpecOptions eebsw => ComputeEhlersEvenBetterSineWaveIndicatorFast(data, context, eebsw.Length1, eebsw.Length2),
            EhlersMarketStateIndicatorSpecOptions emsi => ComputeEhlersMarketStateIndicatorFast(data, context, emsi.Length),
            EhlersInstantaneousTrendlineV2SpecOptions eitv2 => ComputeEhlersInstantaneousTrendlineV2Fast(data, context, eitv2.Alpha),
            EhlersCyberCycleOscillatorSpecOptions ecco => ComputeEhlersCyberCycleOscillatorFast(data, context, ecco.Alpha),
            EhlersBandPassFilterV1SpecOptions ebpfv1 => ComputeEhlersBandPassFilterV1Fast(data, context, ebpfv1.Length, ebpfv1.Bw),
            EhlersBandPassFilterV2SpecOptions ebpfv2 => ComputeEhlersBandPassFilterV2Fast(data, context, ebpfv2.Length, ebpfv2.Bw),
            EhlersCycleBandPassFilterSpecOptions ecbpf => ComputeEhlersCycleBandPassFilterFast(data, context, ecbpf.Length, ecbpf.Delta),
            EhlersCycleAmplitudeSpecOptions eca => ComputeEhlersCycleAmplitudeFast(data, context, eca.Length, eca.Delta),
            EhlersHpLpRoofingFilterSpecOptions ehplprf => ComputeEhlersHpLpRoofingFilterFast(data, context, ehplprf.Length1, ehplprf.Length2),
            EhlersEarlyOnsetTrendIndicatorSpecOptions eeoti => ComputeEhlersEarlyOnsetTrendIndicatorFast(data, context, eeoti.Length1, eeoti.Length2, eeoti.K),
            EhlersDetrendedLeadingIndicatorSpecOptions edli => ComputeEhlersDetrendedLeadingIndicatorFast(data, context, edli.Length),
            EhlersClassicHilbertTransformerSpecOptions echt => ComputeEhlersClassicHilbertTransformerFast(data, context, echt.Length1, echt.Length2),
            EhlersZeroMeanRoofingFilterSpecOptions ezmrf => ComputeEhlersZeroMeanRoofingFilterFast(data, context, ezmrf.Length1, ezmrf.Length2),
            EhlersSuperPassbandFilterSpecOptions espf => ComputeEhlersSuperPassbandFilterFast(data, context, espf.FastLength, espf.SlowLength, espf.Length1, espf.Length2),
            EhlersRoofingFilterV2SpecOptions erfv2 => ComputeEhlersRoofingFilterV2Fast(data, context, erfv2.UpperLength, erfv2.LowerLength),
            EhlersImpulseReactionSpecOptions eir => ComputeEhlersImpulseReactionFast(data, context, eir.Length1, eir.Length2, eir.Q),
            EhlersReverseEmaIndicatorV1SpecOptions erema => ComputeEhlersReverseEmaIndicatorV1Fast(data, context, erema.Alpha),
            EhlersSquelchIndicatorSpecOptions esqe => ComputeEhlersSquelchIndicatorFast(data, context, esqe.Length1, esqe.Length2, esqe.Length3),
            EhlersReverseEmaIndicatorV2SpecOptions eremav2 => spec.OutputKey switch
            {
                null or "EremaCycle" => ComputeEhlersReverseEmaIndicatorV2Fast(data, context, eremav2.TrendAlpha, eremav2.CycleAlpha),
                "EremaTrend" => ComputeEhlersReverseEmaIndicatorV2Fast(data, context, eremav2.TrendAlpha, eremav2.CycleAlpha,
                    EhlersReverseEmaWave.Trend),
                _ => null
            },
            EhlersStochasticCyberCycleSpecOptions escc => ComputeEhlersStochasticCyberCycleFast(data, context, escc.Length, escc.Alpha),
            EhlersCenterofGravityOscillatorSpecOptions ecog => ComputeEhlersCenterofGravityOscillatorFast(data, context, ecog.Length),
            EhlersReflexIndicatorSpecOptions eri => ComputeEhlersReflexIndicatorFast(data, context, eri.Length),
            EhlersTrendflexIndicatorSpecOptions eti => ComputeEhlersTrendflexIndicatorFast(data, context, eti.Length),
            JmaRsxCloneSpecOptions jrsx => ComputeJmaRsxCloneFast(data, context, jrsx.Length),
            RateOfChangeSpecOptions roc => ComputeRateOfChangeFast(data, context, roc.Length),
            WilliamsFractalsSpecOptions wf => ComputeWilliamsFractalsFast(data, context, wf.Length),
            DetrendedPriceOscillatorSpecOptions dpo => ComputeDetrendedPriceOscillatorFast(data, context, dpo.Length, dpo.MaType),
            PolarizedFractalEfficiencySpecOptions pfe => ComputePolarizedFractalEfficiencyFast(data, context, pfe.Length, pfe.SmoothLength),
            SchaffTrendCycleSpecOptions stc => ComputeSchaffTrendCycleFast(data, context, stc.CycleLength, stc.FastLength, stc.SlowLength),
            SmoothedRateOfChangeSpecOptions sroc => ComputeSmoothedRateOfChangeFast(data, context, sroc.RocLength,
                sroc.SmoothLength, sroc.MaType),
            FloorPivotPointSpecOptions _ => ComputeFloorPivotPointFast(data, context),
            FloorPivotPointS1SpecOptions _ => ComputeFloorPivotPointS1Fast(data, context),
            FloorPivotPointR1SpecOptions _ => ComputeFloorPivotPointR1Fast(data, context),
            CamarillaPivotPointSpecOptions _ => ComputeCamarillaPivotPointFast(data, context),
            WoodiePivotPointSpecOptions _ => ComputeWoodiePivotPointFast(data, context),
            FibonacciPivotPointSpecOptions _ => ComputeFibonacciPivotPointFast(data, context),
            DemarkPivotPointSpecOptions _ => ComputeDemarkPivotPointFast(data, context),
            LinearChannelMiddleSpecOptions lcm => ComputeLinearChannelMiddleFast(data, context, lcm.Length),
            PriceChannelUpperSpecOptions pcu => ComputePriceChannelUpperFast(data, context, pcu.Length),
            PriceChannelLowerSpecOptions pcl => ComputePriceChannelLowerFast(data, context, pcl.Length),
            DonchianChannelUpperSpecOptions dcu => ComputeDonchianChannelUpperFast(data, context, dcu.Length),
            DonchianChannelLowerSpecOptions dcl => ComputeDonchianChannelLowerFast(data, context, dcl.Length),
            ThreeHmaSpecOptions thma => ComputeThreeHmaFast(data, context, thma.Length),
            AdaptiveAutonomousRecursiveTrailingStopSpecOptions aarts => ComputeAdaptiveAutonomousRecursiveTrailingStopFast(data, context, aarts.Length, aarts.Lambda),
            AdaptiveTrailingStopSpecOptions ats => ComputeAdaptiveTrailingStopFast(data, context, ats.Length, ats.Multiplier),
            AverageTrueRangeTrailingStopsSpecOptions atrts => ComputeAverageTrueRangeTrailingStopsFast(data, context,
                atrts.Length, atrts.Multiplier, atrts.MaType),
            WellesWilderSummationSpecOptions wws => ComputeWellesWilderSummationFast(data, context, wws.Length),
            DampingIndexSpecOptions di => ComputeDampingIndexFast(data, context, di.Length, di.MaType),
            // LongLength only reaches the Longa line CalculateDidiIndex publishes beside Curta, and this
            // spec is bound to Curta.
            DidiIndexSpecOptions didi => ComputeDidiIndexFast(data, context, didi.ShortLength,
                didi.MediumLength, didi.MaType),
            VerticalHorizontalFilterSpecOptions vhf => ComputeVerticalHorizontalFilterFast(data, context, vhf.Length),
            LinearRegressionSlopeSpecOptions lrs => ComputeLinearRegressionSlopeFast(data, context, lrs.Length),
            LinearRegressionInterceptSpecOptions lri => ComputeLinearRegressionInterceptFast(data, context, lri.Length),

            // Batch 5 - Indicators with Core methods (23 indicators)
            AbsolutePriceOscillatorSpecOptions apo2 => ComputeAbsolutePriceOscillatorFast(data, context, apo2.FastLength, apo2.SlowLength),
            AccumulationDistributionLineSpecOptions adl2 => ComputeAccumulationDistributionLineFast(data, context),
            AdaptiveExponentialMovingAverageSpecOptions aema => ComputeAdaptiveExponentialMovingAverageFast(data, context,
                aema.Length, aema.MaType),
            AverageDirectionalIndexSpecOptions adx2 => ComputeAverageDirectionalIndexFast(data, context, adx2.Length, adx2.MaType),
            AverageTrueRangeSpecOptions atr2 => ComputeAverageTrueRangeFast(data, context, atr2.Length, atr2.MaType),
            ChandeMomentumOscillatorSpecOptions cmo2 => ComputeChandeMomentumOscillatorFast(data, context, cmo2.Length),
            // Length and MaType reach only the Signal line of CalculateEaseOfMovement, not the series
            // this spec is bound to, so neither is passed.
            EaseOfMovementSpecOptions eom => ComputeEaseOfMovementFast(data, context, eom.Divisor),
            EhlersZeroLagExponentialMovingAverageSpecOptions ezlema => ComputeEhlersZeroLagEmaFast(data, context, ezlema.Length),
            HullMovingAverageSpecOptions hma2 => ComputeHullMovingAverageFast(data, context, hma2.Length),
            KlingerVolumeOscillatorSpecOptions kvo2 => ComputeKlingerVolumeOscillatorFast(data, context, kvo2.FastLength, kvo2.SlowLength),
            KnowSureThingSpecOptions kst2 => ComputeKnowSureThingFast(data, context, kst2.RocLength1, kst2.RocLength2, kst2.RocLength3, kst2.RocLength4, kst2.Length1, kst2.Length2, kst2.Length3, kst2.Length4),
            NegativeVolumeIndexSpecOptions nvi2 => ComputeNegativeVolumeIndexFast(data, context),
            OnBalanceVolumeSpecOptions obv2 => ComputeOnBalanceVolumeFast(data, context),
            PercentagePriceOscillatorSpecOptions ppo2 => ComputePercentagePriceOscillatorFast(data, context, ppo2.FastLength, ppo2.SlowLength),
            PercentageVolumeOscillatorSpecOptions pvo2 => ComputePercentageVolumeOscillatorFast(data, context, pvo2.FastLength, pvo2.SlowLength),
            PositiveVolumeIndexSpecOptions pvi2 => ComputePositiveVolumeIndexFast(data, context),
            PrettyGoodOscillatorSpecOptions pgo2 => ComputePrettyGoodOscillatorFast(data, context, pgo2.Length),
            PriceMomentumOscillatorSpecOptions pmo2 => ComputePriceMomentumOscillatorFast(data, context, pmo2.Length1, pmo2.Length2),
            PriceVolumeTrendSpecOptions pvt2 => ComputePriceVolumeTrendFast(data, context),
            PriceZoneOscillatorSpecOptions pzo2 => ComputePriceZoneOscillatorFast(data, context, pzo2.Length),
            RelativeVigorIndexSpecOptions rvi2 => ComputeRelativeVigorIndexFast(data, context, rvi2.Length),
            TriangularMovingAverageSpecOptions tma2 => ComputeTriangularMovingAverageFast(data, context, tma2.Length),
            TrueStrengthIndexSpecOptions tsi2 => ComputeTrueStrengthIndexFast(data, context, tsi2.Length1, tsi2.Length2),

            // Batch 6 - Additional Oscillators with Core methods
            ChandeQuickStickSpecOptions cqs => ComputeChandeQuickStickFast(data, context, cqs.Length, cqs.MaType),
            // Length1 and MaType only smooth the Signal and Histogram lines CalculateDeltaMovingAverage
            // publishes beside the delta, and this spec is bound to the delta itself.
            DeltaMovingAverageSpecOptions dma => ComputeDeltaMovingAverageFast(data, context, dma.Length2),
            FoldedRelativeStrengthIndexSpecOptions frsi => ComputeFoldedRsiFast(data, context, frsi.Length),
            EnhancedWilliamsRSpecOptions ewr => ComputeEnhancedWilliamsRFast(data, context, ewr.Length, ewr.SignalLength),
            ConnorsRelativeStrengthIndexSpecOptions crsi2 => ComputeConnorsRsiFast(data, context, crsi2.Length1, crsi2.Length2, crsi2.Length3),
            StochasticRelativeStrengthIndexSpecOptions srsi2 => ComputeStochasticRsiFast(data, context, srsi2.Length,
                srsi2.SmoothLength1, srsi2.SmoothLength2, srsi2.MaType),
            StochasticMomentumIndexSpecOptions smi => ComputeStochasticMomentumIndexFast(data, context, smi.Length1, smi.SmoothLength1, smi.SmoothLength2),

            // Batch 7 - Additional oscillators and power indicators
            CCTStochRSISpecOptions cctsr => ComputeCCTStochRelativeStrengthIndexFast(data, context, cctsr.Length2,
                cctsr.Length3, cctsr.Length5, cctsr.MaType),
            InertiaIndicatorSpecOptions inertia => ComputeInertiaFast(data, context, inertia.Length, maType: inertia.MaType),
            PremierStochasticOscillatorSpecOptions pso => ComputePremierStochasticFast(data, context, pso.Length, pso.SmoothLength),
            BullPowerIndicatorSpecOptions bpi => ComputeBullPowerFast(data, context, bpi.Length),
            BearPowerIndicatorSpecOptions beari => ComputeBearPowerFast(data, context, beari.Length),
            MomentumOscillatorSpecOptions mosc => ComputeMomentumOscillatorFast(data, context, mosc.Length, mosc.SmoothLength),
            StochasticOscillatorSpecOptions stosc => ComputeStochasticOscillatorFast(data, context, stosc.Length),
            StochasticFastOscillatorSpecOptions stfo => ComputeStochasticFastFast(data, context, stfo.Length, stfo.SmoothLength1),

            // Multi-output: KeltnerChannels
            KeltnerChannelsSpecOptions kc => spec.OutputKey switch
            {
                null or "MiddleBand" => ComputeKeltnerMiddleFast(data, context, kc.Length1),
                _ => null
            },

            // Multi-output: ElderRayIndex
            ElderRayIndexSpecOptions eri => spec.OutputKey switch
            {
                null or "BullPower" => ComputeElderRayBullPowerFast(data, context, eri.Length),
                _ => null
            },

            // Multi-output: ChandelierExit
            ChandelierExitSpecOptions ce => spec.OutputKey switch
            {
                null or "ExitLong" => ComputeChandelierExitLongFast(data, context, ce.Length, ce.MaType, ce.Mult),
                "ExitShort" => ComputeChandelierExitShortFast(data, context, ce.Length, ce.MaType, ce.Mult),
                _ => null
            },

            // Batch 8 - Moving Averages with existing Core methods
            _1LCLeastSquaresMovingAverageSpecOptions olc => ComputeOneLCLeastSquaresFast(data, context, olc.Length),
            _3HMASpecOptions thma2 => ComputeThreeHmaFast(data, context, thma2.Length),
            AdaptiveRelativeStrengthIndexSpecOptions arsi => ComputeAdaptiveRsiFast(data, context, arsi.Length, arsi.MaType),
            BollingerBandsAvgTrueRangeSpecOptions bbatr => ComputeBollingerBandsAvgTrueRangeFast(data, context,
                bbatr.AtrLength, bbatr.Length, bbatr.MaType, bbatr.StdDevMult),
            ChandeMomentumOscillatorSignalSpecOptions cmos => ComputeChandeMomentumOscillatorSignalFast(data, context,
                cmos.Length, cmos.SignalLength, cmos.MaType),
            EhlersRoofingFilterV1SpecOptions erf1 => ComputeEhlersRoofingFilterV1Fast(data, context, erf1.Length1, erf1.Length2, erf1.MaType),

            // Batch 9 - More oscillators and indicators
            SpearmanIndicatorSpecOptions spi => ComputeEhlersSpearmanRankFast(data, context, spi.Length),
            TillsonT3MovingAverageSpecOptions tt3 => ComputeTillsonT3Fast(data, context, tt3.Length, tt3.VFactor),
            UltimateMovingAverageBandsSpecOptions umab => ComputeUltimateMovingAverageFast(data, context, umab.MaxLength),

            // Batch 10 - Ehlers Window indicators
            // The batch never forwards pedestal to the moving average that produces the bound series,
            // so it cannot change the published values and is not passed here.
            EhlersHammingWindowIndicatorSpecOptions ehwi => ComputeEhlersHammingWindowFast(data, context, ehwi.Length, ehwi.MaType),
            EhlersHannWindowIndicatorSpecOptions ehnwi => ComputeEhlersHannWindowFast(data, context, ehnwi.Length, ehnwi.MaType),
            EhlersTriangleWindowIndicatorSpecOptions etwi => ComputeEhlersTriangleWindowFast(data, context, etwi.Length, etwi.MaType),
            EhlersImpulseResponseSpecOptions eir => ComputeEhlersImpulseResponseFast(data, context, eir.Length, eir.Bw, eir.MaType),
            EhlersModifiedStochasticIndicatorSpecOptions emsi => ComputeEhlersModifiedStochasticFast(data, context, emsi.Length1, emsi.Length2, emsi.Length3),

            // Batch 11 - Additional Moving Averages with Core methods
            VariableIndexDynamicAverageSpecOptions vida => ComputeVariableIndexDynamicAverageFast(data, context, vida.Length),

            // Batch 12 - New Core Methods for Previously Unimplemented Indicators
            // SignalLength and MaType smooth the oscillator into the Signal line only, so neither can
            // change the bound series. Length2 is not used by the clip indicator's batch at all.
            EhlersSimpleDerivIndicatorSpecOptions esdi => ComputeEhlersSimpleDerivIndicatorFast(data, context, esdi.Length),
            EhlersSimpleClipIndicatorSpecOptions esci => ComputeEhlersSimpleClipIndicatorFast(data, context, esci.Length1, esci.Length3),
            ElderMarketThermometerSpecOptions emt => ComputeElderMarketThermometerFast(data, context, emt.Length, emt.MaType),
            EhlersRelativeVigorIndexSpecOptions ervi => ComputeEhlersRelativeVigorIndexFast(data, context, ervi.Length, ervi.SignalLength, ervi.MaType),
            EhlersMovingAverageDifferenceIndicatorSpecOptions emad => ComputeEhlersMovingAverageDifferenceFast(data, context, emad.FastLength, emad.SlowLength, emad.MaType),
            Dema2LinesSpecOptions d2l => ComputeDema2LinesFast(data, context, d2l.FastLength, d2l.SlowLength, d2l.MaType),
            GainLossMovingAverageSpecOptions glma => ComputeGainLossMovingAverageFast(data, context, glma.Length, glma.SignalLength, glma.MaType),
            ErgodicMeanDeviationIndicatorSpecOptions emdi => ComputeErgodicMeanDeviationIndicatorFast(data, context, emdi.Length1, emdi.Length2, emdi.Length3, emdi.SignalLength, emdi.MaType),

            // Batch 13 - Volatility Indicators with Core Methods
            MayerMultipleSpecOptions mm => ComputeMayerMultipleFast(data, context, mm.Length, mm.MaType),
            GopalakrishnanRangeIndexSpecOptions gri => ComputeGopalakrishnanRangeIndexFast(data, context, gri.Length, gri.MaType),
            HighLowMovingAverageSpecOptions hlma => ComputeHighLowMovingAverageFast(data, context, hlma.Length, hlma.MaType),
            StiffnessIndicatorSpecOptions sti => ComputeStiffnessIndicatorFast(data, context, sti.Length1, sti.Length2, sti.SmoothingLength, sti.MaType),
            MarketMeannessIndexSpecOptions mmi => ComputeMarketMeannessIndexFast(data, context, mmi.Length, mmi.MaType),
            SharpeRatioSpecOptions sr => ComputeSharpeRatioFast(data, context, sr.Length, sr.Bmk, sr.MaType),

            // Batch 14 - More Risk Ratios and Trend Indicators
            SortinoRatioSpecOptions sortr => ComputeSortinoRatioFast(data, context, sortr.Length, sortr.Bmk, sortr.MaType),
            MartinRatioSpecOptions martr => ComputeMartinRatioFast(data, context, martr.Length, martr.Bmk, martr.MaType),
            InformationRatioSpecOptions ir => ComputeInformationRatioFast(data, context, ir.Length, ir.Bmk, ir.MaType),
            OptimizedTrendTrackerSpecOptions ott => ComputeOptimizedTrendTrackerFast(data, context, ott.Length, ott.Percent, ott.MaType),
            RandomWalkIndexSpecOptions rwi => ComputeRandomWalkIndexFast(data, context, rwi.Length, rwi.MaType),
            PriceChannelSpecOptions pc => ComputePriceChannelFast(data, context, pc.Length, pc.Pct, pc.MaType),

            // Batch 15 - Moving Average Band Indicators
            MovingAverageBandsSpecOptions mab => ComputeMovingAverageBandsFast(data, context, mab.FastLength, mab.SlowLength, mab.Mult, mab.MaType),
            MovingAverageBandWidthSpecOptions mabw => ComputeMovingAverageBandWidthFast(data, context, mabw.FastLength, mabw.SlowLength, mabw.Mult, mabw.MaType),
            MovingAverageChannelSpecOptions mac => ComputeMovingAverageChannelFast(data, context, mac.Length, mac.MaType),
            MovingAverageEnvelopeSpecOptions mae => ComputeMovingAverageEnvelopeFast(data, context, mae.Length, mae.Pct, mae.MaType),
            MovingAverageSupportResistanceSpecOptions masr => ComputeMovingAverageSupportResistanceFast(data, context, masr.Length, masr.MaType),
            VariableMovingAverageBandsSpecOptions vmab => spec.OutputKey switch
            {
                null or "MiddleBand" => ComputeVariableMovingAverageBandsFast(data, context, vmab.Length, vmab.Mult, vmab.MaType),
                "UpperBand" => ComputeVariableMovingAverageBandsFast(data, context, vmab.Length, vmab.Mult, vmab.MaType, ChannelBand.Upper),
                "LowerBand" => ComputeVariableMovingAverageBandsFast(data, context, vmab.Length, vmab.Mult, vmab.MaType, ChannelBand.Lower),
                _ => null
            },
            NarrowSidewaysChannelSpecOptions nsc => spec.OutputKey switch
            {
                null or "MiddleBand" => ComputeNarrowSidewaysChannelFast(data, context, nsc.Length, nsc.MaType),
                "UpperBand" => ComputeNarrowSidewaysChannelFast(data, context, nsc.Length, nsc.MaType, ChannelBand.Upper),
                "LowerBand" => ComputeNarrowSidewaysChannelFast(data, context, nsc.Length, nsc.MaType, ChannelBand.Lower),
                _ => null
            },

            // Batch 16 - More Band and Channel Indicators
            HighLowBandsSpecOptions hlb => spec.OutputKey switch
            {
                // The middle band is what the streaming state publishes as its value, so an unnamed request is
                // that band - the shift only ever moves the two outer ones.
                null or "MiddleBand" => ComputeHighLowBandsFast(data, context, hlb.Length, 0, hlb.MaType),
                "UpperBand" => ComputeHighLowBandsFast(data, context, hlb.Length, hlb.PctShift, hlb.MaType),
                "LowerBand" => ComputeHighLowBandsFast(data, context, hlb.Length, -hlb.PctShift, hlb.MaType),
                _ => null
            },
            AutoDispersionBandsSpecOptions adb => spec.OutputKey switch
            {
                null or "MiddleBand" => ComputeAutoDispersionBandsFast(data, context, adb.Length, adb.SmoothLength, adb.MaType),
                "UpperBand" => ComputeAutoDispersionBandsFast(data, context, adb.Length, adb.SmoothLength, adb.MaType, ChannelBand.Upper),
                "LowerBand" => ComputeAutoDispersionBandsFast(data, context, adb.Length, adb.SmoothLength, adb.MaType, ChannelBand.Lower),
                _ => null
            },
            BollingerBandsFibonacciRatiosSpecOptions bbfr => ComputeBollingerBandsFibonacciRatiosFast(data, context, bbfr.Length, bbfr.FibRatio1, bbfr.FibRatio2, bbfr.FibRatio3, bbfr.MaType),
            BollingerBandsWithAtrPctSpecOptions bbatrp => ComputeBollingerBandsWithAtrPctFast(data, context, bbatrp.Length, bbatrp.BbLength, bbatrp.StdDevMult, bbatrp.MaType),
            KirshenbaumBandsSpecOptions kb => ComputeKirshenbaumBandsFast(data, context, kb.Length1, kb.Length2, kb.StdDevFactor, kb.MaType),
            SmoothedVolatilityBandsSpecOptions svb => ComputeSmoothedVolatilityBandsFast(data, context, svb.Length1, svb.Length2, svb.Deviation, svb.BandAdjust, svb.MaType),
            StollerAverageRangeChannelsSpecOptions starc => ComputeStollerAverageRangeChannelsFast(data, context, starc.Length, starc.AtrMult, starc.MaType),
            VervoortVolatilityBandsSpecOptions vvb => ComputeVervoortVolatilityBandsFast(data, context, vvb.Length1, vvb.Length2, vvb.DevMult, vvb.LowBandMult, vvb.MaType),

            // Batch 17 - More Band and Channel Indicators
            VolumeAdaptiveBandsSpecOptions vab => ComputeVolumeAdaptiveBandsFast(data, context, vab.Length, vab.MaType),
            TrendTraderBandsSpecOptions ttb => spec.OutputKey switch
            {
                null or "MiddleBand" => ComputeTrendTraderBandsFast(data, context, ttb.Length, ttb.Mult, ttb.BandStep, ttb.MaType),
                "UpperBand" => ComputeTrendTraderBandsFast(data, context, ttb.Length, ttb.Mult, ttb.BandStep, ttb.MaType, ChannelBand.Upper),
                "LowerBand" => ComputeTrendTraderBandsFast(data, context, ttb.Length, ttb.Mult, ttb.BandStep, ttb.MaType, ChannelBand.Lower),
                _ => null
            },
            ScalpersChannelSpecOptions sc => ComputeScalpersChannelFast(data, context, sc.Length1, sc.Length2, sc.MaType),
            HurstCycleChannelSpecOptions hcc => spec.OutputKey switch
            {
                null or "FastMiddleBand" => ComputeHurstCycleChannelFast(data, context, hcc.FastLength, hcc.SlowLength, hcc.FastMult,
                    hcc.SlowMult, hcc.MaType),
                "FastUpperBand" => ComputeHurstCycleChannelFast(data, context, hcc.FastLength, hcc.SlowLength, hcc.FastMult,
                    hcc.SlowMult, hcc.MaType, HurstCycleSeries.FastUpperBand),
                "FastLowerBand" => ComputeHurstCycleChannelFast(data, context, hcc.FastLength, hcc.SlowLength, hcc.FastMult,
                    hcc.SlowMult, hcc.MaType, HurstCycleSeries.FastLowerBand),
                "SlowUpperBand" => ComputeHurstCycleChannelFast(data, context, hcc.FastLength, hcc.SlowLength, hcc.FastMult,
                    hcc.SlowMult, hcc.MaType, HurstCycleSeries.SlowUpperBand),
                "SlowMiddleBand" => ComputeHurstCycleChannelFast(data, context, hcc.FastLength, hcc.SlowLength, hcc.FastMult,
                    hcc.SlowMult, hcc.MaType, HurstCycleSeries.SlowMiddleBand),
                "SlowLowerBand" => ComputeHurstCycleChannelFast(data, context, hcc.FastLength, hcc.SlowLength, hcc.FastMult,
                    hcc.SlowMult, hcc.MaType, HurstCycleSeries.SlowLowerBand),
                "OMed" => ComputeHurstCycleChannelFast(data, context, hcc.FastLength, hcc.SlowLength, hcc.FastMult,
                    hcc.SlowMult, hcc.MaType, HurstCycleSeries.OMed),
                "OShort" => ComputeHurstCycleChannelFast(data, context, hcc.FastLength, hcc.SlowLength, hcc.FastMult,
                    hcc.SlowMult, hcc.MaType, HurstCycleSeries.OShort),
                _ => null
            },
            PriceCurveChannelSpecOptions pcc => spec.OutputKey switch
            {
                null or "MiddleBand" => ComputePriceCurveChannelFast(data, context, pcc.Length, pcc.MaType),
                "UpperBand" => ComputePriceCurveChannelFast(data, context, pcc.Length, pcc.MaType, ChannelBand.Upper),
                "LowerBand" => ComputePriceCurveChannelFast(data, context, pcc.Length, pcc.MaType, ChannelBand.Lower),
                _ => null
            },
            PriceHeadleyAccelerationBandsSpecOptions phab => ComputePriceHeadleyAccelerationBandsFast(data, context, phab.Length, phab.Factor, phab.MaType),
            PriceLineChannelSpecOptions plc => spec.OutputKey switch
            {
                null or "MiddleBand" => ComputePriceLineChannelFast(data, context, plc.Length, plc.MaType),
                "UpperBand" => ComputePriceLineChannelFast(data, context, plc.Length, plc.MaType, ChannelBand.Upper),
                "LowerBand" => ComputePriceLineChannelFast(data, context, plc.Length, plc.MaType, ChannelBand.Lower),
                _ => null
            },
            RateOfChangeBandsSpecOptions rocb => ComputeRateOfChangeBandsFast(data, context, rocb.Length, rocb.SmoothLength, rocb.MaType),

            // Batch 18 - Strength and Zone Indicators
            AbsoluteStrengthMTFIndicatorSpecOptions asmtf => ComputeAbsoluteStrengthMTFFast(data, context, asmtf.Length, asmtf.SmoothLength, asmtf.MaType),
            AdaptivePriceZoneIndicatorSpecOptions apz => ComputeAdaptivePriceZoneFast(data, context, apz.Length, apz.Pct, apz.MaType),
            DynamicSupportAndResistanceSpecOptions dsar => spec.OutputKey switch
            {
                null or "MiddleBand" => ComputeDynamicSupportAndResistanceFast(data, context, dsar.Length, dsar.MaType),
                "Support" => ComputeDynamicSupportAndResistanceFast(data, context, dsar.Length, dsar.MaType,
                    SupportResistanceBand.Support),
                "Resistance" => ComputeDynamicSupportAndResistanceFast(data, context, dsar.Length, dsar.MaType,
                    SupportResistanceBand.Resistance),
                _ => null
            },
            ApirineSlowRelativeStrengthIndexSpecOptions asrsi => ComputeApirineSlowRsiFast(data, context, asrsi.Length, asrsi.SmoothLength, asrsi.MaType),
            ElderSafeZoneStopsSpecOptions eszs => ComputeElderSafeZoneStopsFast(data, context, eszs.Length, eszs.Mult, eszs.MaType),
            EnhancedIndexSpecOptions ei => ComputeEnhancedIndexFast(data, context, ei.Length, ei.SignalLength, ei.MaType),
            FastandSlowKurtosisOscillatorSpecOptions fsko => ComputeFastAndSlowKurtosisFast(data, context, fsko.Length, fsko.Ratio, fsko.MaType),
            FearAndGreedIndicatorSpecOptions fgi => ComputeFearAndGreedFast(data, context, fgi.FastLength, fgi.SlowLength, fgi.SmoothLength, fgi.MaType),

            // Batch 19 - Volume and Movement Indicators
            FiniteVolumeElementsSpecOptions fve => ComputeFiniteVolumeElementsFast(data, context, fve.Length, fve.Factor, fve.MaType),
            FibonacciRetraceSpecOptions fr => ComputeFibonacciRetraceFast(data, context, fr.Length1, fr.Length2, fr.Factor, fr.MaType),
            FreedomOfMovementSpecOptions fom => ComputeFreedomOfMovementFast(data, context, fom.Length, fom.MaType),
            FXSniperIndicatorSpecOptions fxs => ComputeFXSniperFast(data, context, fxs.CciLength, fxs.T3Length, fxs.B, fxs.MaType),
            GroverLlorensActivatorSpecOptions gla => ComputeGroverLlorensActivatorFast(data, context, gla.Length, gla.Mult, gla.MaType),
            KaseIndicatorSpecOptions ki => ComputeKaseIndicatorFast(data, context, ki.Length, ki.MaType),
            GuppyDistanceIndicatorSpecOptions gdi => ComputeGuppyDistanceFast(data, context, gdi.Length1, gdi.MaType),
            GuppyMultipleMovingAverageSpecOptions gmma => ComputeGuppyMultipleMaFast(data, context, gmma.Length1, gmma.MaType),

            // Batch 20 - Statistical and Correlation Indicators
            HirashimaSugitaRSSpecOptions hsrs => spec.OutputKey switch
            {
                null or "MiddleBand" => ComputeHirashimaSugitaRSFast(data, context, hsrs.Length, hsrs.MaType),
                "UpperBand1" => ComputeHirashimaSugitaRSFast(data, context, hsrs.Length, hsrs.MaType, 1),
                "UpperBand2" => ComputeHirashimaSugitaRSFast(data, context, hsrs.Length, hsrs.MaType, 2),
                "LowerBand1" => ComputeHirashimaSugitaRSFast(data, context, hsrs.Length, hsrs.MaType, -1),
                "LowerBand2" => ComputeHirashimaSugitaRSFast(data, context, hsrs.Length, hsrs.MaType, -2),
                _ => null
            },
            InverseFisherFastZScoreSpecOptions iffz => ComputeInverseFisherFastZScoreFast(data, context, iffz.Length, iffz.MaType),
            InverseFisherZScoreSpecOptions ifz => ComputeInverseFisherZScoreFast(data, context, ifz.Length, ifz.MaType),
            JapaneseCorrelationCoefficientSpecOptions jcc => ComputeJapaneseCorrelationCoefficientFast(data, context, jcc.Length, jcc.MaType),
            JrcFractalDimensionSpecOptions jfd => ComputeJrcFractalDimensionFast(data, context, jfd.Length1, jfd.Length2, jfd.SmoothLength, jfd.MaType),
            KaseConvergenceDivergenceSpecOptions kcd => ComputeKaseConvergenceDivergenceFast(data, context, kcd.Length1, kcd.Length2, kcd.Length3, kcd.MaType),
            KaseDevStopV2SpecOptions kds2 => ComputeKaseDevStopV2Fast(data, context, kds2.FastLength, kds2.SlowLength, kds2.Length, kds2.StdDev1, kds2.StdDev2, kds2.StdDev3, kds2.StdDev4, kds2.MaType),
            KwanIndicatorSpecOptions kwi => ComputeKwanIndicatorFast(data, context, kwi.Length, kwi.SmoothLength, kwi.MaType),
            LBRPaintBarsSpecOptions lbr => ComputeLBRPaintBarsFast(data, context, lbr.Length, lbr.LbLength, lbr.AtrMult, lbr.MaType),

            // Batch 21 - MACD-like and Directional Indicators
            MacZIndicatorSpecOptions macz => ComputeMacZIndicatorFast(data, context, macz.FastLength, macz.SlowLength, macz.SignalLength, macz.Length, macz.Gamma, macz.Mult, macz.MaType),
            MacZVwapIndicatorSpecOptions maczvwap => ComputeMacZVwapIndicatorFast(data, context, maczvwap.FastLength, maczvwap.SlowLength, maczvwap.SignalLength, maczvwap.Length1, maczvwap.Length2, maczvwap.Gamma, maczvwap.MaType),
            MassThrustIndicatorSpecOptions mti => ComputeMassThrustIndicatorFast(data, context, mti.Length, mti.MaType),
            ModifiedGannHiloActivatorSpecOptions mgha => ComputeModifiedGannHiloActivatorFast(data, context, mgha.LookbackLength, mgha.Length, mgha.MaType),
            ModifiedPriceVolumeTrendSpecOptions mpvt => ComputeModifiedPriceVolumeTrendFast(data, context, mpvt.Length, mpvt.MaType),
            MultiVoteOnBalanceVolumeSpecOptions mvobv => ComputeMultiVoteOnBalanceVolumeFast(data, context, mvobv.Length, mvobv.MaType),
            NaturalDirectionalComboSpecOptions ndc => ComputeNaturalDirectionalComboFast(data, context, ndc.Length, ndc.SmoothLength, ndc.MaType),
            NaturalDirectionalIndexSpecOptions ndi => ComputeNaturalDirectionalIndexFast(data, context, ndi.Length, ndi.SmoothLength, ndi.MaType),

            // Batch 22 - Market and Volume Indicators
            NaturalMarketMirrorSpecOptions nmm => ComputeNaturalMarketMirrorFast(data, context, nmm.Length, nmm.MaType),
            NaturalMarketRiverSpecOptions nmr => ComputeNaturalMarketRiverFast(data, context, nmr.Length, nmr.MaType),
            NaturalMarketComboSpecOptions nmc => ComputeNaturalMarketComboFast(data, context, nmc.Length, nmc.SmoothLength, nmc.MaType),
            NaturalStochasticIndicatorSpecOptions nsi => ComputeNaturalStochasticIndicatorFast(data, context, nsi.Length, nsi.SmoothLength, nsi.MaType),
            NegativeVolumeDisparityIndicatorSpecOptions nvdi => ComputeNegativeVolumeDisparityFast(data, context, nvdi.Length, nvdi.SignalLength, nvdi.Top, nvdi.Bottom, nvdi.MaType),
            OceanIndicatorSpecOptions oi => ComputeOceanIndicatorFast(data, context, oi.Length, oi.MaType),
            OCHistogramSpecOptions och => ComputeOCHistogramFast(data, context, och.Length, och.MaType),
            OnBalanceVolumeModifiedSpecOptions obvmod => ComputeOnBalanceVolumeModifiedFast(data, context, obvmod.Length1, obvmod.Length2, obvmod.MaType),

            // Batch 23 - Volume and Statistical Indicators
            OnBalanceVolumeReflexSpecOptions obvr => ComputeOnBalanceVolumeReflexFast(data, context, obvr.Length, obvr.SignalLength, obvr.MaType),
            PivotPointAverageSpecOptions ppa => ComputePivotPointAverageFast(data, context, ppa.Length, ppa.MaType),
            PriceVolumeRankSpecOptions pvr => ComputePriceVolumeRankFast(data, context, pvr.FastLength, pvr.SlowLength, pvr.MaType),
            PringSpecialKSpecOptions psk => ComputePringSpecialKFast(data, context, psk.SmoothLength, psk.MaType),
            ProjectionBandwidthSpecOptions pb => ComputeProjectionBandwidthFast(data, context, pb.Length, pb.MaType),
            QuasiWhiteNoiseSpecOptions qwn => ComputeQuasiWhiteNoiseFast(data, context, qwn.Length, qwn.NoiseLength, qwn.Divisor, qwn.MaType),
            RapidRelativeStrengthIndexSpecOptions rrsi => ComputeRapidRsiFast(data, context, rrsi.Length, rrsi.MaType),
            ReallySimpleIndicatorSpecOptions rsi2 => ComputeReallySimpleIndicatorFast(data, context, rsi2.Length, rsi2.SmoothLength, rsi2.MaType),

            // Batch 24 - Complex Oscillators and Ehlers Indicators
            AdaptiveErgodicCandlestickOscillatorSpecOptions aeco => ComputeAdaptiveErgodicCandlestickOscillatorFast(data, context, aeco.SmoothLength, aeco.SignalLength, aeco.MaType),
            ConfluenceIndicatorSpecOptions ci2 => ComputeConfluenceIndicatorFast(data, context, ci2.Length, ci2.MaType),
            ConstanceBrownCompositeIndexSpecOptions cbci => ComputeConstanceBrownCompositeIndexFast(data, context, cbci.SmoothLength, cbci.MaType),
            EhlersAMDetectorSpecOptions eamd => ComputeEhlersAMDetectorFast(data, context, eamd.Length1, eamd.Length2, eamd.MaType),
            EhlersAnticipateIndicatorSpecOptions eai => ComputeEhlersAnticipateIndicatorFast(data, context, eai.Length, eai.MaType),
            EhlersAutoCorrelationReversalsSpecOptions eacr => ComputeEhlersAutoCorrelationReversalsFast(data, context, eacr.Length1, eacr.Length2, eacr.Length3, eacr.MaType),
            EhlersEmpiricalModeDecompositionSpecOptions eemd => ComputeEhlersEmpiricalModeDecompositionFast(data, context, eemd.Length1, eemd.Length2, eemd.Delta, eemd.Fraction, eemd.MaType),
            EhlersFMDemodulatorIndicatorSpecOptions efmd => ComputeEhlersFMDemodulatorFast(data, context, efmd.FastLength, efmd.SlowLength, efmd.MaType),

            // Batch 25 - More Ehlers Indicators
            // MaType smooths the phase into the Signal line only; the bound series is the raw phase.
            EhlersPhaseCalculationSpecOptions epc => ComputeEhlersPhaseCalculationFast(data, context, epc.Length),
            EhlersRestoringPullIndicatorSpecOptions erpi => ComputeEhlersRestoringPullIndicatorFast(data, context, erpi.MinLength, erpi.MaxLength, erpi.Length1, erpi.Length2, erpi.MaType),
            EhlersRocketRelativeStrengthIndexSpecOptions errsi => ComputeEhlersRocketRsiFast(data, context, errsi.Length1, errsi.MaType),
            EhlersSimpleWindowIndicatorSpecOptions eswi => ComputeEhlersSimpleWindowIndicatorFast(data, context, eswi.Length, eswi.MaType),
            EhlersSmoothedAdaptiveMomentumSpecOptions esam => ComputeEhlersSmoothedAdaptiveMomentumFast(data, context, esam.Length1, esam.Length2, esam.MaType),
            EhlersSnakeUniversalTradingFilterSpecOptions esutf => ComputeEhlersSnakeUniversalTradingFilterFast(data, context, esutf.Length1, esutf.Length2, esutf.Bw, esutf.MaType),
            EhlersTrendExtractionSpecOptions ete => ComputeEhlersTrendExtractionFast(data, context, ete.Length, ete.Delta, ete.MaType),
            EhlersTripleDelayLineDetrenderSpecOptions etdld => ComputeEhlersTripleDelayLineDetrenderFast(data, context, etdld.Length, etdld.MaType),

            // Batch 26 - Ehlers V2 and Universal Trading Filter
            EhlersUniversalTradingFilterSpecOptions eutf => ComputeEhlersUniversalTradingFilterFast(data, context, eutf.Length1, eutf.Length2, eutf.Mult, eutf.MaType),
            EhlersAdaptiveCommodityChannelIndexV2SpecOptions eacciv2 => ComputeEhlersAdaptiveCommodityChannelIndexV2Fast(data, context, eacciv2.Length1, eacciv2.Length2, eacciv2.Length3, eacciv2.MaType),
            EhlersAdaptiveRelativeStrengthIndexV2SpecOptions earsiv2 => ComputeEhlersAdaptiveRelativeStrengthIndexV2Fast(data, context, earsiv2.Length1, earsiv2.Length2, earsiv2.Length3, earsiv2.MaType),
            EhlersAdaptiveRsiFisherTransformV2SpecOptions earftv2 => ComputeEhlersAdaptiveRsiFisherTransformV2Fast(data, context, earftv2.Length1, earftv2.Length2, earftv2.Length3, earftv2.MaType),
            EhlersAdaptiveStochasticIndicatorV2SpecOptions easiv2 => ComputeEhlersAdaptiveStochasticIndicatorV2Fast(data, context, easiv2.Length1, easiv2.Length2, easiv2.Length3, easiv2.MaType),
            EhlersMesaPredictIndicatorV2SpecOptions empiv2 => ComputeEhlersMesaPredictIndicatorV2Fast(data, context, empiv2.Length1, empiv2.Length2, empiv2.Length3, empiv2.Length4, empiv2.MaType),
            EhlersSignalToNoiseRatioV1SpecOptions esnrv1 => ComputeEhlersSignalToNoiseRatioV1Fast(data, context, esnrv1.Length, esnrv1.MaType),
            EhlersSignalToNoiseRatioV2SpecOptions esnrv2 => ComputeEhlersSignalToNoiseRatioV2Fast(data, context, esnrv2.Length, esnrv2.MaType),

            // Batch 27 - Trend and Volatility Indicators
            TrendExhaustionIndicatorSpecOptions tei => ComputeTrendExhaustionIndicatorFast(data, context, tei.Length, tei.MaType),
            TrendImpulseFilterSpecOptions tif => ComputeTrendImpulseFilterFast(data, context, tif.Length1, tif.Length2, tif.MaType),
            TrendDirectionForceIndexSpecOptions tdfi => ComputeTrendDirectionForceIndexFast(data, context, tdfi.Length1, tdfi.Length2, tdfi.MaType),
            TrendAnalysisIndexSpecOptions tai => ComputeTrendAnalysisIndexFast(data, context, tai.Length1, tai.Length2, tai.MaType),
            TrendAnalysisIndicatorSpecOptions tai2 => ComputeTrendAnalysisIndicatorFast(data, context, tai2.Length1, tai2.Length2, tai2.MaType),
            TrenderSpecOptions tr => ComputeTrenderFast(data, context, tr.Length, tr.AtrMult, tr.MaType),
            TurboStochasticsFastSpecOptions tsf => ComputeTurboStochasticsFastFast(data, context, tsf.Length1, tsf.Length2, tsf.TurboLength, tsf.MaType),

            // Batch 28 - Volume and Volatility Indicators
            TurboStochasticsSlowSpecOptions tss => ComputeTurboStochasticsSlowFast(data, context, tss.Length1, tss.Length2, tss.TurboLength, tss.MaType),
            VolumeFlowIndicatorSpecOptions vfi => ComputeVolumeFlowIndicatorFast(data, context, vfi.Length1, vfi.Length2, vfi.SignalLength, vfi.SmoothLength, vfi.MaType),
            VolatilityQualityIndexSpecOptions vqi => ComputeVolatilityQualityIndexFast(data, context, vqi.FastLength, vqi.SlowLength, vqi.MaType),
            VolatilityBasedMomentumSpecOptions vbm => ComputeVolatilityBasedMomentumFast(data, context, vbm.Length1, vbm.Length2, vbm.MaType),
            VolatilitySwitchIndicatorSpecOptions vsi => ComputeVolatilitySwitchIndicatorFast(data, context, vsi.Length, vsi.MaType),
            VortexBandsSpecOptions vb => ComputeVortexBandsFast(data, context, vb.Length, vb.MaType),
            VostroIndicatorSpecOptions vi => ComputeVostroIndicatorFast(data, context, vi.Length1, vi.Length2, vi.Level, vi.MaType),

            // Batch 29 - Ergodic and Momentum Indicators
            ErgodicCommoditySelectionIndexSpecOptions ecsi => ComputeErgodicCommoditySelectionIndexFast(data, context, ecsi.Length, ecsi.SmoothLength, ecsi.PointValue, ecsi.MaType),
            ErgodicMovingAverageConvergenceDivergenceSpecOptions emacd => ComputeErgodicMacdFast(data, context, emacd.Length1, emacd.Length2, emacd.Length3, emacd.MaType),
            ErgodicTrueStrengthIndexV1SpecOptions etsiv1 => ComputeErgodicTsiV1Fast(data, context, etsiv1.Length1, etsiv1.Length2, etsiv1.Length3, etsiv1.SignalLength, etsiv1.MaType),
            ErgodicTrueStrengthIndexV2SpecOptions etsiv2 => ComputeErgodicTsiV2Fast(data, context, etsiv2.Length1, etsiv2.Length2, etsiv2.Length3, etsiv2.SignalLength, etsiv2.MaType),
            SMIErgodicIndicatorSpecOptions smie => ComputeSMIErgodicIndicatorFast(data, context, smie.FastLength, smie.SlowLength, smie.SignalLength, smie.MaType),
            InsyncIndexSpecOptions ii => ComputeInsyncIndexFast(data, context, ii.FastLength, ii.SlowLength, ii.SignalLength, ii.MaType),
            SqueezeMomentumIndicatorSpecOptions smi => ComputeSqueezeMomentumIndicatorFast(data, context, smi.Length, smi.MaType),
            StochasticConnorsRelativeStrengthIndexSpecOptions scrsi => ComputeStochasticConnorsRsiFast(data, context, scrsi.Length1, scrsi.Length2, scrsi.Length3, scrsi.SmoothLength1, scrsi.SmoothLength2, scrsi.MaType),

            // Batch 30 - Stochastic Regular
            StochasticRegularSpecOptions sr => ComputeStochasticRegularFast(data, context, sr.Length1, sr.Length2, sr.MaType),

            // Batch 31 - Relative and Statistical Indicators
            RecursiveRelativeStrengthIndexSpecOptions rrsi => ComputeRecursiveRelativeStrengthIndexFast(data, context, rrsi.Length, rrsi.MaType),
            RelativeSpreadStrengthSpecOptions rss => ComputeRelativeSpreadStrengthFast(data, context, rss.FastLength, rss.SlowLength, rss.Length, rss.SmoothLength, rss.MaType),
            RelativeVolatilityIndexV2SpecOptions rviv2 => ComputeRelativeVolatilityIndexV2Fast(data, context, rviv2.Length, rviv2.SmoothLength, rviv2.MaType),
            RelativeVolumeIndicatorSpecOptions rvi => ComputeRelativeVolumeIndicatorFast(data, context, rvi.Length, rvi.MaType),
            SelfAdjustingRelativeStrengthIndexSpecOptions sarsi => ComputeSelfAdjustingRsiFast(data, context, sarsi.Length, sarsi.SmoothingLength, sarsi.Mult, sarsi.MaType),
            SmoothedWilliamsAccumulationDistributionSpecOptions swad => ComputeSmoothedWilliamsAccumulationDistributionFast(data, context, swad.Length, swad.MaType),
            StatisticalVolatilitySpecOptions sv => ComputeStatisticalVolatilityFast(data, context, sv.Length1, sv.Length2, sv.MaType),
            TradersDynamicIndexSpecOptions tdi => ComputeTradersDynamicIndexFast(data, context, tdi.Length1, tdi.Length2, tdi.Length3, tdi.Length4, tdi.MaType),

            // Batch 32 - Remaining Indicators (Part 1)
            FunctionToCandlesSpecOptions ftc => ComputeFunctionToCandlesFast(data, context, ftc.Length, ftc.MaType),
            PeakValleyEstimationSpecOptions pve => ComputePeakValleyEstimationFast(data, context, pve.Length, pve.SmoothLength, pve.MaType),
            PhaseChangeIndexSpecOptions pci => ComputePhaseChangeIndexFast(data, context, pci.Length, pci.SmoothLength, pci.MaType),
            PseudoPolynomialChannelSpecOptions ppc => ComputePseudoPolynomialChannelFast(data, context, ppc.Length, ppc.Morph, ppc.MaType),
            RecursiveDifferenciatorSpecOptions rd => ComputeRecursiveDifferenciatorFast(data, context, rd.Length, rd.Alpha, rd.MaType),
            ReversalPointsSpecOptions rp => ComputeReversalPointsFast(data, context, rp.Length, rp.MaType),
            RSINGIndicatorSpecOptions rsing => ComputeRSINGIndicatorFast(data, context, rsing.Length, rsing.MaType),
            RunningEquitySpecOptions re => ComputeRunningEquityFast(data, context, re.Length, re.MaType),

            // Batch 33 - Remaining Indicators (Part 2)
            SigmaSpikesSpecOptions ss => ComputeSigmaSpikesFast(data, context, ss.Length, ss.MaType),
            StandardDevationSpecOptions sd => ComputeStandardDevationFast(data, context, sd.Length, sd.MaType),
            StationaryExtrapolatedLevelsSpecOptions sel => spec.OutputKey switch
            {
                null or "Deviation" => ComputeStationaryExtrapolatedLevelsFast(data, context, sel.Length, sel.MaType),
                "UpperBand" => ComputeStationaryExtrapolatedLevelsFast(data, context, sel.Length, sel.MaType,
                    ExtrapolatedLevelSeries.Upper),
                "MiddleBand" => ComputeStationaryExtrapolatedLevelsFast(data, context, sel.Length, sel.MaType,
                    ExtrapolatedLevelSeries.Middle),
                "LowerBand" => ComputeStationaryExtrapolatedLevelsFast(data, context, sel.Length, sel.MaType,
                    ExtrapolatedLevelSeries.Lower),
                _ => null
            },
            SupportResistanceSpecOptions sr2 => ComputeSupportResistanceFast(data, context, sr2.Length, sr2.MaType),
            SurfaceRoughnessEstimatorSpecOptions sre => ComputeSurfaceRoughnessEstimatorFast(data, context, sre.Length, sre.MaType),
            TechnicalRatingsSpecOptions tr => ComputeTechnicalRatingsFast(data, context, tr.AoLength1, tr.AoLength2, tr.RsiLength, tr.StochLength1, tr.StochLength2, tr.StochLength3, tr.MaType),
            TFSMboIndicatorSpecOptions tfsm => ComputeTFSMboIndicatorFast(data, context, tfsm.FastLength, tfsm.SlowLength, tfsm.SignalLength, tfsm.MaType),
            TheRangeIndicatorSpecOptions tri => ComputeTheRangeIndicatorFast(data, context, tri.Length, tri.SmoothLength, tri.MaType),

            // Batch 34 - Remaining Indicators (Part 3)
            TimeAndMoneyChannelSpecOptions tmc => ComputeTimeAndMoneyChannelFast(data, context, tmc.Length1, tmc.Length2, tmc.MaType),
            TopsAndBottomsFinderSpecOptions tbf => ComputeTopsAndBottomsFinderFast(data, context, tbf.Length, tbf.MaType),
            TraderPressureIndexSpecOptions tpi => ComputeTraderPressureIndexFast(data, context, tpi.Length1, tpi.Length2, tpi.SmoothLength, tpi.MaType),
            UhlMaCrossoverSystemSpecOptions umcs => ComputeUhlMaCrossoverSystemFast(data, context, umcs.Length, umcs.MaType),
            UltimateVolatilityIndicatorSpecOptions uvi => ComputeUltimateVolatilityIndicatorFast(data, context, uvi.Length, uvi.MaType),
            UniChannelSpecOptions uc => ComputeUniChannelFast(data, context, uc.Length, uc.UbFac, uc.LbFac, uc.Type1, uc.MaType),
            VixTradingSystemSpecOptions vts => ComputeVixTradingSystemFast(data, context, vts.Length, vts.MaxCount, vts.MinCount, vts.MaType),
            WilsonRelativePriceChannelSpecOptions wrpc => ComputeWilsonRelativePriceChannelFast(data, context, wrpc.Length, wrpc.SmoothLength, wrpc.MaType),
            WoodieCommodityChannelIndexSpecOptions wcci => ComputeWoodieCommodityChannelIndexFast(data, context, wcci.FastLength, wcci.SlowLength, wcci.MaType),

            _ => null
        };
    }

    #region Moving Averages

    /// <summary>
    /// Computes Simple Moving Average using zero-allocation fast path.
    /// Uses MovingAverageCore with span-based computation directly into pooled buffer.
    /// </summary>
    internal static ComputeBuffer ComputeSmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.SimpleMovingAverage(inputSpan, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Exponential Moving Average using zero-allocation fast path.
    /// Uses MovingAverageCore with span-based computation directly into pooled buffer.
    /// </summary>
    internal static ComputeBuffer ComputeEmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.ExponentialMovingAverage(inputSpan, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Weighted Moving Average using zero-allocation fast path.
    /// Uses MovingAverageCore with span-based computation directly into pooled buffer.
    /// </summary>
    internal static ComputeBuffer ComputeWmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.WeightedMovingAverage(inputSpan, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Double Exponential Moving Average using zero-allocation fast path.
    /// Uses MovingAverageCore with span-based computation directly into pooled buffer.
    /// </summary>
    internal static ComputeBuffer ComputeDemaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.DoubleExponentialMovingAverage(inputSpan, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Triple Exponential Moving Average using zero-allocation fast path.
    /// Uses MovingAverageCore with span-based computation directly into pooled buffer.
    /// </summary>
    internal static ComputeBuffer ComputeTemaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.TripleExponentialMovingAverage(inputSpan, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Hull Moving Average using zero-allocation fast path.
    /// Uses MovingAverageCore with span-based computation directly into pooled buffer.
    /// </summary>
    internal static ComputeBuffer ComputeHmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.HullMovingAverage(inputSpan, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Triangular Moving Average using zero-allocation fast path.
    /// Uses MovingAverageCore with span-based computation directly into pooled buffer.
    /// </summary>
    internal static ComputeBuffer ComputeTmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.TriangularMovingAverage(inputSpan, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Welles Wilder Moving Average using zero-allocation fast path.
    /// Uses MovingAverageCore with span-based computation directly into pooled buffer.
    /// </summary>
    internal static ComputeBuffer ComputeWwmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.WellesWilderMovingAverage(inputSpan, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Linear Regression using zero-allocation fast path.
    /// Uses MovingAverageCore with span-based computation directly into pooled buffer.
    /// </summary>
    internal static ComputeBuffer ComputeLinRegFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.LinearRegression(inputSpan, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Kaufman Adaptive Moving Average using zero-allocation fast path.
    /// Uses MovingAverageCore with span-based computation directly into pooled buffer.
    /// </summary>
    internal static ComputeBuffer ComputeKamaFast(StockData data, ComputeContext context, int length = 10)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.KaufmanAdaptiveMovingAverage(inputSpan, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Zero-Lag EMA using zero-allocation fast path.
    /// Uses MovingAverageCore with span-based computation directly into pooled buffer.
    /// </summary>
    internal static ComputeBuffer ComputeZlemaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.ZeroLagEma(inputSpan, buffer.WritableSpan, length);

        return buffer;
    }

    #endregion

    #region Oscillators

    /// <summary>
    /// Computes Relative Strength Index using zero-allocation fast path.
    /// Uses OscillatorCore with span-based computation directly into pooled buffer.
    /// </summary>
    internal static ComputeBuffer ComputeRsiFast(StockData data, ComputeContext context, int length = 14,
        MovingAvgType maType = MovingAvgType.WildersSmoothingMethod)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var count = inputList.Count;
        var buffer = context.Rent(count);

        if (maType == MovingAvgType.WildersSmoothingMethod)
        {
            OscillatorCore.RelativeStrengthIndex(inputSpan, buffer.WritableSpan, length);
            return buffer;
        }

        // CalculateRelativeStrengthIndex smooths its gains and losses with whichever average it was given, and
        // only Wilders has a closed form in the core. Every other average goes through the same dispatch the
        // batch helper uses, so the two read the same series rather than agreeing only at Wilders.
        var pool = ArrayPool<double>.Shared;
        var gainArray = pool.Rent(count);
        var lossArray = pool.Rent(count);
        var avgGainArray = pool.Rent(count);
        var avgLossArray = pool.Rent(count);

        try
        {
            var gains = gainArray.AsSpan(0, count);
            var losses = lossArray.AsSpan(0, count);

            for (var i = 0; i < count; i++)
            {
                var change = i >= 1 ? inputSpan[i] - inputSpan[i - 1] : 0;
                gains[i] = change > 0 ? change : 0;
                losses[i] = change < 0 ? -change : 0;
            }

            var avgGains = avgGainArray.AsSpan(0, count);
            var avgLosses = avgLossArray.AsSpan(0, count);
            MovingAverage(data, maType, length, gains, avgGains);
            MovingAverage(data, maType, length, losses, avgLosses);

            var output = buffer.WritableSpan;
            for (var i = 0; i < count; i++)
            {
                var avgGain = avgGains[i];
                var avgLoss = avgLosses[i];
                var rs = avgLoss != 0 ? avgGain / avgLoss : 0;

                output[i] = avgLoss == 0 ? 100 : avgGain == 0 ? 0
                    : Math.Min(100, Math.Max(0, 100 - (100 / (1 + rs))));
            }

            return buffer;
        }
        finally
        {
            pool.Return(gainArray);
            pool.Return(lossArray);
            pool.Return(avgGainArray);
            pool.Return(avgLossArray);
        }
    }

    /// <summary>
    /// Writes <paramref name="maType"/> over <paramref name="input"/> into <paramref name="output"/>, the same
    /// way the indicator's own helper would.
    /// </summary>
    /// <remarks>
    /// Types with a verified span implementation are written straight into the caller's buffer and allocate
    /// nothing. The rest are computed by their own indicator, which is what the batch helper does for them too -
    /// an arm that computed them a second way here would be a different average under the same name.
    /// </remarks>
    internal static void MovingAverage(StockData data, MovingAvgType maType, int length, ReadOnlySpan<double> input,
        Span<double> output)
    {
        if (CalculationsHelper.TryComputeMovingAverage(data, maType, length, input, output))
        {
            return;
        }

        var values = new List<double>(input.Length);
        for (var i = 0; i < input.Length; i++)
        {
            values.Add(input[i]);
        }

        var computed = CalculationsHelper.GetMovingAverageList(data, maType, length, values);
        for (var i = 0; i < output.Length; i++)
        {
            output[i] = i < computed.Count ? computed[i] : 0;
        }
    }

    /// <summary>
    /// Computes Rate of Change using zero-allocation fast path.
    /// Uses OscillatorCore with span-based computation directly into pooled buffer.
    /// </summary>
    internal static ComputeBuffer ComputeRocFast(StockData data, ComputeContext context, int length = 12)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        OscillatorCore.RateOfChange(inputSpan, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Momentum using zero-allocation fast path.
    /// Uses OscillatorCore with span-based computation directly into pooled buffer.
    /// </summary>
    internal static ComputeBuffer ComputeMomentumFast(StockData data, ComputeContext context, int length = 10)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        OscillatorCore.Momentum(inputSpan, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Williams %R using zero-allocation fast path.
    /// Uses OscillatorCore with span-based computation directly into pooled buffer.
    /// </summary>
    internal static ComputeBuffer ComputeWilliamsRFast(StockData data, ComputeContext context, int length = 14)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;

        // Extract OHLC data into spans
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];

        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
        }

        var buffer = context.Rent(count);
        OscillatorCore.WilliamsR(high, low, close, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Commodity Channel Index using zero-allocation fast path.
    /// Uses OscillatorCore with span-based computation directly into pooled buffer.
    /// </summary>
    internal static ComputeBuffer ComputeCciFast(StockData data, ComputeContext context, int length = 20)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;

        // Extract OHLC data into spans
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];

        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
        }

        var buffer = context.Rent(count);
        OscillatorCore.CommodityChannelIndex(high, low, close, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Stochastic %K using zero-allocation fast path.
    /// Uses OscillatorCore with span-based computation directly into pooled buffer.
    /// </summary>
    internal static ComputeBuffer ComputeStochasticKFast(StockData data, ComputeContext context, int length = 14)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;

        // Extract OHLC data into spans
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];

        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
        }

        var buffer = context.Rent(count);
        OscillatorCore.StochasticK(high, low, close, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Average Directional Index using zero-allocation fast path.
    /// Uses OscillatorCore with span-based computation directly into pooled buffer.
    /// </summary>
    private static (ComputeBuffer Plus, ComputeBuffer Minus) DirectionalIndicators(StockData data,
        ComputeContext context, int length, MovingAvgType maType)
    {
        // The pair CalculateAverageDirectionalIndex publishes as DiPlus and DiMinus: how much of the smoothed
        // true range each direction's movement accounts for. The index itself and every indicator that reads
        // the pair start here, so they cannot drift apart.
        var (inputList, highList, lowList, _, _) = CalculationsHelper.GetInputValuesList(data);
        var count = inputList.Count;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var high = SpanCompat.AsReadOnlySpan(highList);
        var low = SpanCompat.AsReadOnlySpan(lowList);

        using var dmPlusBuffer = context.Rent(count);
        using var dmMinusBuffer = context.Rent(count);
        using var trBuffer = context.Rent(count);
        var dmPlus = dmPlusBuffer.WritableSpan;
        var dmMinus = dmMinusBuffer.WritableSpan;
        var trueRange = trBuffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            var currentHigh = high[i];
            var currentLow = low[i];
            var prevHigh = i >= 1 ? high[i - 1] : 0;
            var prevLow = i >= 1 ? low[i - 1] : 0;

            // The first bar has no previous close, and the current one keeps its true range from opening
            // at the whole day's move.
            var prevClose = i >= 1 ? input[i - 1] : input[i];
            var highDiff = currentHigh - prevHigh;
            var lowDiff = prevLow - currentLow;

            dmPlus[i] = highDiff > lowDiff ? Math.Max(highDiff, 0) : 0;
            dmMinus[i] = highDiff < lowDiff ? Math.Max(lowDiff, 0) : 0;
            trueRange[i] = CalculationsHelper.CalculateTrueRange(currentHigh, currentLow, prevClose);
        }

        using var smoothedPlusBuffer = context.Rent(count);
        using var smoothedMinusBuffer = context.Rent(count);
        using var smoothedRangeBuffer = context.Rent(count);
        var smoothedPlus = smoothedPlusBuffer.WritableSpan;
        var smoothedMinus = smoothedMinusBuffer.WritableSpan;
        var smoothedRange = smoothedRangeBuffer.WritableSpan;
        MovingAverage(data, maType, length, dmPlus, smoothedPlus);
        MovingAverage(data, maType, length, dmMinus, smoothedMinus);
        MovingAverage(data, maType, length, trueRange, smoothedRange);

        var plusBuffer = context.Rent(count);
        var minusBuffer = context.Rent(count);
        var diPlus = plusBuffer.WritableSpan;
        var diMinus = minusBuffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            var range = smoothedRange[i];
            diPlus[i] = range != 0 ? MathHelper.MinOrMax(100 * smoothedPlus[i] / range, 100, 0) : 0;
            diMinus[i] = range != 0 ? MathHelper.MinOrMax(100 * smoothedMinus[i] / range, 100, 0) : 0;
        }

        return (plusBuffer, minusBuffer);
    }

    internal static ComputeBuffer ComputeAdxFast(StockData data, ComputeContext context, int length = 14,
        MovingAvgType maType = MovingAvgType.WildersSmoothingMethod)
    {
        // CalculateAverageDirectionalIndex smooths how far apart the two directional indicators are with
        // whichever average it was given, so the core's hardcoded Wilder's smoothing answered for one type
        // only.
        var (plusBuffer, minusBuffer) = DirectionalIndicators(data, context, length, maType);
        using var diPlusBuffer = plusBuffer;
        using var diMinusBuffer = minusBuffer;
        var diPlus = diPlusBuffer.Span;
        var diMinus = diMinusBuffer.Span;
        var count = diPlus.Length;

        using var diBuffer = context.Rent(count);
        var di = diBuffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            var diSum = diPlus[i] + diMinus[i];
            di[i] = diSum != 0
                ? MathHelper.MinOrMax(100 * Math.Abs(diPlus[i] - diMinus[i]) / diSum, 100, 0)
                : 0;
        }

        var buffer = context.Rent(count);
        MovingAverage(data, maType, length, di, buffer.WritableSpan);

        return buffer;
    }

    /// <summary>
    /// Computes Chande Momentum Oscillator using zero-allocation fast path.
    /// Uses OscillatorCore with span-based computation directly into pooled buffer.
    /// </summary>
    internal static ComputeBuffer ComputeCmoFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        OscillatorCore.ChandeMomentumOscillator(inputSpan, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Percentage Price Oscillator using zero-allocation fast path.
    /// Uses OscillatorCore with span-based computation directly into pooled buffer.
    /// </summary>
    internal static ComputeBuffer ComputePpoFast(StockData data, ComputeContext context, int fastLength = 12, int slowLength = 26)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        OscillatorCore.PercentagePriceOscillator(inputSpan, buffer.WritableSpan, fastLength, slowLength);

        return buffer;
    }

    /// <summary>
    /// Computes Absolute Price Oscillator using zero-allocation fast path.
    /// Uses OscillatorCore with span-based computation directly into pooled buffer.
    /// </summary>
    internal static ComputeBuffer ComputeApoFast(StockData data, ComputeContext context, int fastLength = 12, int slowLength = 26)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        OscillatorCore.AbsolutePriceOscillator(inputSpan, buffer.WritableSpan, fastLength, slowLength);

        return buffer;
    }

    /// <summary>
    /// Computes Ultimate Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeUltimateOscillatorFast(StockData data, ComputeContext context, int length1 = 7, int length2 = 14, int length3 = 28)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
        }
        var buffer = context.Rent(count);
        OscillatorCore.UltimateOscillator(high, low, close, buffer.WritableSpan, length1, length2, length3);
        return buffer;
    }

    /// <summary>
    /// Computes True Strength Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeTsiFast(StockData data, ComputeContext context, int longLength = 25, int shortLength = 13)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.TrueStrengthIndex(inputSpan, buffer.WritableSpan, longLength, shortLength);
        return buffer;
    }

    /// <summary>
    /// Computes Aroon Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAroonOscillatorFast(StockData data, ComputeContext context, int length = 25)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
        }
        var buffer = context.Rent(count);
        OscillatorCore.AroonOscillator(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Detrended Price Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDpoFast(StockData data, ComputeContext context, int length = 20)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.DetrendedPriceOscillator(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes TRIX indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeTrixFast(StockData data, ComputeContext context, int length = 15)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.Trix(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Mass Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMassIndexFast(StockData data, ComputeContext context, int emaLength = 9, int sumLength = 25)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
        }
        var buffer = context.Rent(count);
        OscillatorCore.MassIndex(high, low, buffer.WritableSpan, emaLength, sumLength);
        return buffer;
    }

    #endregion

    #region Volume

    /// <summary>
    /// Computes On Balance Volume using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeObvFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length;
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var close = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            close[i] = (double)tickerList[i].Close;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.OnBalanceVolume(close, volume, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Accumulation/Distribution Line using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAdlFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length;
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.AccumulationDistributionLine(high, low, close, volume, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Chaikin Money Flow using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeCmfFast(StockData data, ComputeContext context, int length = 20)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.ChaikinMoneyFlow(high, low, close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Force Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeForceIndexFast(StockData data, ComputeContext context, int length = 13)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var close = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            close[i] = (double)tickerList[i].Close;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.ForceIndex(close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Volume Rate of Change using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVrocFast(StockData data, ComputeContext context, int length = 12)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.VolumeRateOfChange(volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Negative Volume Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeNviFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length;
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var close = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            close[i] = (double)tickerList[i].Close;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.NegativeVolumeIndex(close, volume, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Positive Volume Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePviFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length;
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var close = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            close[i] = (double)tickerList[i].Close;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.PositiveVolumeIndex(close, volume, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Price Volume Trend using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePvtFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length;
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var close = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            close[i] = (double)tickerList[i].Close;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.PriceVolumeTrend(close, volume, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes VWAP using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVwapFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length;
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.VolumeWeightedAveragePrice(high, low, close, volume, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Chaikin Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChaikinOscillatorFast(StockData data, ComputeContext context, int fastLength = 3, int slowLength = 10)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.ChaikinOscillator(high, low, close, volume, buffer.WritableSpan, fastLength, slowLength);
        return buffer;
    }

    /// <summary>
    /// Computes Ease of Movement using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEmvFast(StockData data, ComputeContext context, int length = 14)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.EaseOfMovement(high, low, volume, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Volatility

    /// <summary>
    /// Computes Average True Range using zero-allocation fast path.
    /// Uses VolatilityCore with span-based computation directly into pooled buffer.
    /// </summary>
    internal static ComputeBuffer ComputeAtrFast(StockData data, ComputeContext context, int length = 14,
        MovingAvgType maType = MovingAvgType.WildersSmoothingMethod)
    {
        // CalculateAverageTrueRange smooths the true range with whichever average it was given, over the whole
        // series and from the first bar. The core here builds its first reading from a simple mean of the
        // opening window and blanks everything before it, which is a different average under the same name and
        // published nothing at all until the lookback had arrived.
        var trueRange = CalculationsHelper.GetTrueRangeList(data);
        var buffer = context.Rent(trueRange.Count);
        MovingAverage(data, maType, length, SpanCompat.AsReadOnlySpan(trueRange), buffer.WritableSpan);

        return buffer;
    }

    /// <summary>
    /// Computes Standard Deviation using zero-allocation fast path.
    /// Uses VolatilityCore with span-based computation directly into pooled buffer.
    /// </summary>
    internal static ComputeBuffer ComputeStdDevFast(StockData data, ComputeContext context, int length = 20)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        VolatilityCore.StandardDeviation(inputSpan, buffer.WritableSpan, length);

        return buffer;
    }

    #endregion

    #region Trend

    /// <summary>
    /// Computes Parabolic SAR using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeParabolicSarFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length;
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
        }
        var buffer = context.Rent(count);
        TrendCore.ParabolicSar(high, low, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes SuperTrend using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSuperTrendFast(StockData data, ComputeContext context, int length = 10)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
        }
        var buffer = context.Rent(count);
        TrendCore.SuperTrend(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Donchian Channel Middle using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDonchianChannelFast(StockData data, ComputeContext context, int length = 20)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
        }
        var buffer = context.Rent(count);
        TrendCore.DonchianChannelMiddle(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Highest High using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeHighestHighFast(StockData data, ComputeContext context, int length = 14)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
        }
        var buffer = context.Rent(count);
        TrendCore.HighestHigh(high, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Lowest Low using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeLowestLowFast(StockData data, ComputeContext context, int length = 14)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var low = new double[count];
        for (var i = 0; i < count; i++)
        {
            low[i] = (double)tickerList[i].Low;
        }
        var buffer = context.Rent(count);
        TrendCore.LowestLow(low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Average Day Range using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAdrFast(StockData data, ComputeContext context, int length = 14)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
        }
        var buffer = context.Rent(count);
        TrendCore.AverageDayRange(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Typical Price using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeTypicalPriceFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length;
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
        }
        var buffer = context.Rent(count);
        TrendCore.TypicalPrice(high, low, close, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Median Price using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMedianPriceFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length;
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
        }
        var buffer = context.Rent(count);
        TrendCore.MedianPrice(high, low, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Weighted Close using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeWeightedCloseFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length;
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
        }
        var buffer = context.Rent(count);
        TrendCore.WeightedClose(high, low, close, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Percentage Change using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePercentageChangeFast(StockData data, ComputeContext context, int length = 1)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        TrendCore.PercentageChange(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Linear Regression Slope using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeLinRegSlopeFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        TrendCore.LinearRegressionSlope(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes R-Squared using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeRSquaredFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        TrendCore.RSquared(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Standard Error using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeStandardErrorFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        TrendCore.StandardError(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Vertical Horizontal Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVhfFast(StockData data, ComputeContext context, int length = 28)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        TrendCore.VerticalHorizontalFilter(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Volatility Indicators (Additional)

    /// <summary>
    /// Computes Historical Volatility using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeHistoricalVolatilityFast(StockData data, ComputeContext context, int length = 20)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        VolatilityCore.HistoricalVolatility(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Chaikin Volatility using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChaikinVolatilityFast(StockData data, ComputeContext context, int length1 = 10,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length2 = 12)
    {
        // CalculateChaikinVolatility averages the high-low range with whichever average it was given and then
        // reads that average's rate of change over length2 bars, so the core's hardcoded average answered for
        // one type only. It also copied the highs and lows into two fresh arrays on a path whose purpose is
        // not to allocate.
        var (_, highList, lowList, _, _) = CalculationsHelper.GetInputValuesList(data);
        var count = highList.Count == lowList.Count ? highList.Count : 0;

        using var rangeBuffer = context.Rent(count);
        var highLow = rangeBuffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            highLow[i] = highList[i] - lowList[i];
        }

        using var averageBuffer = context.Rent(count);
        var highLowAverage = averageBuffer.WritableSpan;
        MovingAverage(data, maType, length1, highLow, highLowAverage);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            // Before length2 bars there is nothing to compare against, and the batch indicator reports no
            // change rather than an unbounded one.
            var previous = i >= length2 ? highLowAverage[i - length2] : 0;
            output[i] = previous != 0 ? (highLowAverage[i] - previous) / previous * 100 : 0;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Ulcer Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeUlcerIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        VolatilityCore.UlcerIndex(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Normalized ATR using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeNormalizedAtrFast(StockData data, ComputeContext context, int length = 14)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
        }
        var buffer = context.Rent(count);
        VolatilityCore.NormalizedAtr(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Variance using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVarianceFast(StockData data, ComputeContext context, int length = 20)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        VolatilityCore.Variance(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Coefficient of Variation using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeCoefficientOfVariationFast(StockData data, ComputeContext context, int length = 20)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        VolatilityCore.CoefficientOfVariation(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes True Range using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeTrueRangeFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length;
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
        }
        var buffer = context.Rent(count);
        VolatilityCore.TrueRange(high, low, close, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Bollinger Bands Middle using zero-allocation fast path.
    /// </summary>
    /// <summary>
    /// Computes one Bollinger band: the moving average of the input offset by a multiple of the population
    /// standard deviation of the same window about its own mean. A negative multiple gives the lower band.
    /// </summary>
    /// <remarks>
    /// CalculateBollingerBands takes its deviation from GetStandardDeviationList, which measures the window
    /// about its own mean rather than about the moving average. The two agree only while that average is
    /// simple, so a band built on the average alone parts company as soon as a caller names another one.
    /// </remarks>
    private static ComputeBuffer BollingerBand(StockData data, ComputeContext context, int length, double multiplier,
        MovingAvgType maType)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = inputList.Count;
        length = Math.Max(length, 1);

        using var middle = context.Rent(count);
        MovingAverage(data, maType, length, input, middle.WritableSpan);
        var ma = middle.Span;

        using var deviation = context.Rent(count);
        VolatilityCore.StandardDeviation(input, deviation.WritableSpan, length);
        var stdDev = deviation.Span;

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            output[i] = ma[i] + (multiplier * stdDev[i]);
        }

        return buffer;
    }

    internal static ComputeBuffer ComputeBollingerBandsFast(StockData data, ComputeContext context, int length = 20,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // The middle band is the moving average of the chained series, taken through the same helper the batch
        // reaches GetMovingAverageList for; the registry average this used runs the opening window differently.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var buffer = context.Rent(inputList.Count);
        MovingAverage(data, maType, Math.Max(length, 1), SpanCompat.AsReadOnlySpan(inputList), buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Bollinger Bands Middle band using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeBollingerMiddleFast(StockData data, ComputeContext context, int length = 20, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        return ComputeBollingerBandsFast(data, context, length, maType);
    }

    /// <summary>
    /// Computes Bollinger Bands Upper band using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeBollingerUpperFast(StockData data, ComputeContext context, int length = 20, double multiplier = 2,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        return BollingerBand(data, context, length, multiplier, maType);
    }

    /// <summary>
    /// Computes Bollinger Bands Lower band using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeBollingerLowerFast(StockData data, ComputeContext context, int length = 20, double multiplier = 2,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        return BollingerBand(data, context, length, -multiplier, maType);
    }

    #endregion

    #region Additional Volume Indicators

    /// <summary>
    /// Computes Klinger Volume Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeKlingerVolumeFast(StockData data, ComputeContext context, int length = 34)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.KlingerVolumeOscillator(high, low, close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Volume Price Confirmation Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVpciFast(StockData data, ComputeContext context, int length = 5)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var close = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            close[i] = (double)tickerList[i].Close;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.VolumePriceConfirmationIndicator(close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Additional Oscillators

    /// <summary>
    /// Computes Money Flow Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMoneyFlowIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        OscillatorCore.MoneyFlowIndex(high, low, close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Balance of Power using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeBalanceOfPowerFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length;
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var open = new double[count];
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];
        for (var i = 0; i < count; i++)
        {
            open[i] = (double)tickerList[i].Open;
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
        }
        var buffer = context.Rent(count);
        OscillatorCore.BalanceOfPower(open, high, low, close, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Relative Vigor Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeRelativeVigorIndexFast(StockData data, ComputeContext context, int length = 10)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var open = new double[count];
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];
        for (var i = 0; i < count; i++)
        {
            open[i] = (double)tickerList[i].Open;
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
        }
        var buffer = context.Rent(count);
        OscillatorCore.RelativeVigorIndex(open, high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Aroon Up using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAroonUpFast(StockData data, ComputeContext context, int length = 25)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
        }
        var buffer = context.Rent(count);
        OscillatorCore.AroonUp(high, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Aroon Down using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAroonDownFast(StockData data, ComputeContext context, int length = 25)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var low = new double[count];
        for (var i = 0; i < count; i++)
        {
            low[i] = (double)tickerList[i].Low;
        }
        var buffer = context.Rent(count);
        OscillatorCore.AroonDown(low, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Additional Trend Indicators

    /// <summary>
    /// Computes Keltner Channel Middle using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeKeltnerChannelMiddleFast(StockData data, ComputeContext context, int length = 20)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        TrendCore.KeltnerChannelMiddle(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Trend Detection using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeTrendDetectionFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        TrendCore.TrendDetection(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Price Channel Middle using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePriceChannelMiddleFast(StockData data, ComputeContext context, int length = 20)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
        }
        var buffer = context.Rent(count);
        TrendCore.PriceChannelMiddle(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Additional Oscillators (Batch 2)

    /// <summary>
    /// Computes Stochastic D using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeStochasticDFast(StockData data, ComputeContext context, int length = 14)
    {
        return ComputeStochasticDFast(data, context, length, 3);
    }

    /// <summary>
    /// Computes Stochastic D using zero-allocation fast path with configurable K and D lengths.
    /// </summary>
    internal static ComputeBuffer ComputeStochasticDFast(StockData data, ComputeContext context, int kLength, int dLength)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.StochasticD(high, low, close, buffer.WritableSpan, kLength, dLength);
        return buffer;
    }

    /// <summary>
    /// Computes Awesome Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAwesomeOscillatorFast(StockData data, ComputeContext context, int fastLength = 5,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int slowLength = 34)
    {
        // CalculateAwesomeOscillator is the gap between two averages of the median price, both of whichever
        // type it was given. GetDerivedSeriesList is the same median the indicator reads, including the rule
        // that follows the high and low around a chained close.
        var median = CalculationsHelper.GetDerivedSeriesList(data, DerivedSeriesKind.Hl2);
        var count = median.Count;
        var medianSpan = SpanCompat.AsReadOnlySpan(median);

        using var fastBuffer = context.Rent(count);
        using var slowBuffer = context.Rent(count);
        var fast = fastBuffer.WritableSpan;
        var slow = slowBuffer.WritableSpan;
        MovingAverage(data, maType, fastLength, medianSpan, fast);
        MovingAverage(data, maType, slowLength, medianSpan, slow);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            output[i] = fast[i] - slow[i];
        }

        return buffer;
    }

    /// <summary>
    /// Computes Accelerator Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAcceleratorOscillatorFast(StockData data, ComputeContext context, int fastLength = 5,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int slowLength = 34, int smoothLength = 5)
    {
        // CalculateAcceleratorOscillator is the awesome oscillator less an average of itself, so it is built
        // from the awesome oscillator rather than from a second reading of the price.
        using var awesome = ComputeAwesomeOscillatorFast(data, context, fastLength, maType, slowLength);
        var count = awesome.Length;

        using var smoothedBuffer = context.Rent(count);
        var smoothed = smoothedBuffer.WritableSpan;
        MovingAverage(data, maType, smoothLength, awesome.Span, smoothed);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            output[i] = awesome.Span[i] - smoothed[i];
        }

        return buffer;
    }

    /// <summary>
    /// Computes Percentage Volume Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePvoFast(StockData data, ComputeContext context, int length = 12)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        OscillatorCore.PercentageVolumeOscillator(volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Fisher Transform using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeFisherTransformFast(StockData data, ComputeContext context, int length = 10)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
        }
        var buffer = context.Rent(count);
        OscillatorCore.FisherTransform(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Connors RSI using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeConnorsRsiFast(StockData data, ComputeContext context, int length = 3)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.ConnorsRsi(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Price Momentum Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePmoFast(StockData data, ComputeContext context, int length = 35)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.PriceMomentumOscillator(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Know Sure Thing using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeKstFast(StockData data, ComputeContext context, int length = 10)
    {
        _ = length;
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.KnowSureThing(inputSpan, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Percent Rank using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePercentRankFast(StockData data, ComputeContext context, int length = 100)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.PercentRank(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Choppiness Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChoppinessIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
        }
        var buffer = context.Rent(count);
        OscillatorCore.ChoppinessIndex(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Additional Moving Averages (Batch 2)

    /// <summary>
    /// Computes Smoothed Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSmmaFast(StockData data, ComputeContext context, int length = 14)
    {
        // The smoothed moving average and Welles Wilder's are the same average under two names, and this spec
        // is bound to CalculateWellesWilderMovingAverage, which delegates to the routine below.
        // MovingAverageCore.SmoothedMovingAverage waits for a full window before it publishes anything, so it
        // read zero where the batch was already running.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.WellesWilderMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes McGinley Dynamic using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMcGinleyDynamicFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.McGinleyDynamic(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes T3 Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeT3Fast(StockData data, ComputeContext context, int length = 5)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.T3MovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Vidya using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVidyaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.Vidya(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Variable Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.VariableMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes MACD Line using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMacdLineFast(StockData data, ComputeContext context, int fastLength = 12, int slowLength = 26)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.MacdLine(inputSpan, buffer.WritableSpan, fastLength, slowLength);
        return buffer;
    }

    /// <summary>
    /// Computes MACD Signal using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMacdSignalFast(StockData data, ComputeContext context, int fastLength = 12, int slowLength = 26, int signalLength = 9)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.MacdSignal(inputSpan, buffer.WritableSpan, fastLength, slowLength, signalLength);
        return buffer;
    }

    /// <summary>
    /// Computes MACD Histogram using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMacdHistogramFast(StockData data, ComputeContext context, int fastLength = 12, int slowLength = 26, int signalLength = 9)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.MacdHistogram(inputSpan, buffer.WritableSpan, fastLength, slowLength, signalLength);
        return buffer;
    }

    /// <summary>
    /// Computes Absolute Strength Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAbsoluteStrengthIndexFast(StockData data, ComputeContext context, int length = 10)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.AbsoluteStrengthIndex(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Relative Momentum Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeRelativeMomentumIndexFast(StockData data, ComputeContext context, int length = 14, int momentum = 4)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.RelativeMomentumIndex(inputSpan, buffer.WritableSpan, length, momentum);
        return buffer;
    }

    /// <summary>
    /// Computes Intraday Momentum Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeIntradayMomentumIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.IntradayMomentumIndex(open, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Swing Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSwingIndexFast(StockData data, ComputeContext context, double limitMove = 0)
    {
        // CalculateSwingIndex runs Wilder's swing index over the chained series and the range that series is
        // measured against, and has no value at the first bar because it needs the bar before it.
        // OscillatorCore.SwingIndex read the close and published something at bar zero.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var opens = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var count = inputList.Count;

        using var highSeries = context.Rent(count);
        using var lowSeries = context.Rent(count);
        CustomRange(data, input, highSeries.WritableSpan, lowSeries.WritableSpan);
        var highs = highSeries.Span;
        var lows = lowSeries.Span;

        var buffer = context.Rent(count);
        var swingIndex = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            swingIndex[i] = i >= 1
                ? WilderSwingIndex.Compute(opens[i], highs[i], lows[i], input[i], opens[i - 1], input[i - 1], limitMove)
                : 0;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Accumulative Swing Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAccumulativeSwingIndexFast(StockData data, ComputeContext context, double limitMove = 0)
    {
        // CalculateAccumulativeSwingIndex accumulates Wilder's swing index, whose numerator runs from the
        // previous close to today's and whose K and R take the sizes of the moves rather than their signs.
        // The core still carried the older reading, so the two parted company from the second bar on.
        var (inputList, highList, lowList, openList, _) = CalculationsHelper.GetInputValuesList(data);
        var count = inputList.Count;

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        var accumulated = 0d;

        for (var i = 0; i < count; i++)
        {
            // The first bar has no previous bar to swing from, so it contributes nothing.
            accumulated += i >= 1
                ? WilderSwingIndex.Compute(openList[i], highList[i], lowList[i], inputList[i], openList[i - 1],
                    inputList[i - 1], limitMove)
                : 0;
            output[i] = accumulated;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Coppock Curve using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeCoppockCurveFast(StockData data, ComputeContext context, int length = 10,
        MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int fastLength = 11, int slowLength = 14)
    {
        // CalculateCoppockCurve averages the sum of two rates of change over length bars, with whichever
        // average it was given. The length was discarded here as decoration on a curve with fixed parameters,
        // when it is the only window the indicator smooths over.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var count = inputList.Count;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        using var totalBuffer = context.Rent(count);
        var total = totalBuffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            total[i] = RateOfChange(inputSpan, i, fastLength) + RateOfChange(inputSpan, i, slowLength);
        }

        var buffer = context.Rent(count);
        MovingAverage(data, maType, length, total, buffer.WritableSpan);

        return buffer;
    }

    /// <summary>
    /// The rate of change at <paramref name="index"/> over <paramref name="length"/> bars, as
    /// CalculateRateOfChange reports it.
    /// </summary>
    /// <remarks>
    /// A bar that has not arrived reads as zero, and a zero there is no change rather than a division.
    /// </remarks>
    private static double RateOfChange(ReadOnlySpan<double> input, int index, int length)
    {
        var prevValue = index >= length ? input[index - length] : 0;
        return prevValue != 0 ? (input[index] - prevValue) / prevValue * 100 : 0;
    }

    /// <summary>
    /// Computes Chande Forecast Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChandeForecastOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.ChandeForecastOscillator(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Bull Power using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeBullPowerFast(StockData data, ComputeContext context, int length = 14)
    {
        // The mirror of the bear reading: how much of the bar the buyers took. Only its signal line is
        // smoothed, so length does not reach the series this arm is bound to.
        _ = length;
        var (inputList, highList, lowList, openList, _) = CalculationsHelper.GetInputValuesList(data);
        var count = inputList.Count;

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            var close = inputList[i];

            // There is no previous close on the first bar.
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var open = openList[i];
            var high = highList[i];
            var low = lowList[i];

            output[i] = close < open ? Math.Max(high - open, close - low) : prevClose < open ? Math.Max(high - prevClose, close - low) :
                close > open ? Math.Max(open - prevClose, high - low) : prevClose > open ? high - low :
                high - close > close - low ? high - open : prevClose < open ? Math.Max(high - prevClose, close - low) :
                high - close < close - low ? Math.Max(open - close, high - low) : prevClose > open ? high - low :
                prevClose > open ? Math.Max(high - open, close - low) : prevClose < open ? Math.Max(open - close, high - low) : high - low;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Bear Power using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeBearPowerFast(StockData data, ComputeContext context, int length = 14)
    {
        // CalculateBearPowerIndicator reads how much of the bar the sellers took, from the open, the previous
        // close and where the close landed inside the range. Only its signal line is smoothed, so length does
        // not reach the series this arm is bound to.
        _ = length;
        var (inputList, highList, lowList, openList, _) = CalculationsHelper.GetInputValuesList(data);
        var count = inputList.Count;

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            var close = inputList[i];

            // There is no previous close on the first bar.
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var open = openList[i];
            var high = highList[i];
            var low = lowList[i];

            output[i] = close < open ? high - low : prevClose > open ? Math.Max(close - open, high - low) :
                close > open ? Math.Max(open - low, high - close) : prevClose > open ? Math.Max(prevClose - low, high - close) :
                high - close > close - low ? high - low : prevClose > open ? Math.Max(prevClose - open, high - low) :
                high - close < close - low ? open - low : close > open ? Math.Max(close - low, high - close) :
                close > open ? Math.Max(prevClose - open, high - close) : prevClose < open ? Math.Max(open - low, high - close) : high - low;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Polarized Fractal Efficiency using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePolarizedFractalEfficiencyFast(StockData data, ComputeContext context, int length = 10)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.PolarizedFractalEfficiency(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Schaff Trend Cycle using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSchaffTrendCycleFast(StockData data, ComputeContext context, int length = 10)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.SchaffTrendCycle(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Price Zone Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePriceZoneOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.PriceZoneOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Elder Force Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeElderForceIndexFast(StockData data, ComputeContext context, int length = 13)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ElderForceIndex(close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Pretty Good Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePrettyGoodOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.PrettyGoodOscillator(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Relative Volatility Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeRelativeVolatilityIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.RelativeVolatilityIndex(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Qstick using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeQstickFast(StockData data, ComputeContext context, int length = 14)
    {
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.Qstick(open, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Special K using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSpecialKFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length; // Special K uses fixed parameters
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.SpecialK(inputSpan, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Arnaud Legoux Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAlmaFast(StockData data, ComputeContext context, int length = 9)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.ArnaudLegouxMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Least Squares Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeLsmaFast(StockData data, ComputeContext context, int length = 25)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.LeastSquaresMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Fractal Adaptive Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeFramaFast(StockData data, ComputeContext context, int length = 20)
    {
        // CalculateEhlersFractalAdaptiveMovingAverage measures the high-low range over the window, over half of
        // it, and over the half window that ended half a window ago. The ratio between those three is a fractal
        // dimension, and the chained series is smoothed by an alpha that falls away as that dimension rises.
        // MovingAverageCore.FractalAdaptiveMovingAverage warms up differently and reads the close, so it
        // matched neither the first bar nor the rest.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var highs = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var lows = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var count = inputList.Count;
        length = Math.Max(length, 1);

        // The half period is clamped the way the batch clamps any length: never shorter than two bars.
        var halfP = MathHelper.MinOrMax((int)Math.Ceiling((double)length / 2));

        // The half-window extremes are read a second time half a window back, so they are kept rather than
        // streamed; the full-window pair are only ever read at the current bar.
        using var halfHighs = context.Rent(count);
        using var halfLows = context.Rent(count);
        var highest2 = halfHighs.WritableSpan;
        var lowest2 = halfLows.WritableSpan;

        var buffer = context.Rent(count);
        var filter = buffer.WritableSpan;

        var fullHighWindow = new RollingMinMax(length);
        var fullLowWindow = new RollingMinMax(length);
        var halfHighWindow = new RollingMinMax(halfP);
        var halfLowWindow = new RollingMinMax(halfP);

        double previousFilter = 0;
        for (var i = 0; i < count; i++)
        {
            fullHighWindow.Add(highs[i]);
            fullLowWindow.Add(lows[i]);
            halfHighWindow.Add(highs[i]);
            halfLowWindow.Add(lows[i]);
            highest2[i] = halfHighWindow.Max;
            lowest2[i] = halfLowWindow.Min;

            var currentValue = input[i];
            var prevFilter = i >= 1 ? previousFilter : currentValue;
            var lagIndex = Math.Max(i - halfP, 0);
            var n3 = (fullHighWindow.Max - fullLowWindow.Min) / length;
            var n1 = (highest2[i] - lowest2[i]) / halfP;
            var n2 = (highest2[lagIndex] - lowest2[lagIndex]) / halfP;
            var dm = n1 > 0 && n2 > 0 && n3 > 0 ? (Math.Log(n1 + n2) - Math.Log(n3)) / Math.Log(2) : 0;

            var alpha = MathHelper.MinOrMax(MathHelper.Exp(-4.6 * (dm - 1)), 1, 0.01);
            previousFilter = (alpha * currentValue) + ((1 - alpha) * prevFilter);
            filter[i] = previousFilter;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Adaptive Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAmaFast(StockData data, ComputeContext context, int length = 10)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.AdaptiveMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Sine Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSineWmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.SineWeightedMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Hamming Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeHammingMaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.HammingMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Geometric Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeGeoMaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.GeometricMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Regularized EMA using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeRegularizedEmaFast(StockData data, ComputeContext context, int length = 14,
        double lambda = 0.5)
    {
        // CalculateRegularizedExponentialMovingAverage pulls an exponential average towards the straight line
        // carried on from its own last two values, with lambda setting how hard it is pulled.
        // MovingAverageCore.RegularizedEma seeds its first bar with the input rather than starting from
        // nothing, so the two series never meet.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = inputList.Count;
        length = Math.Max(length, 1);

        var buffer = context.Rent(count);
        var rema = buffer.WritableSpan;

        var alpha = (double)2 / (length + 1);
        for (var i = 0; i < count; i++)
        {
            var previousRema1 = i >= 1 ? rema[i - 1] : 0;
            var previousRema2 = i >= 2 ? rema[i - 2] : 0;
            rema[i] = (previousRema1 + (alpha * (input[i] - previousRema1)) + (lambda * ((2 * previousRema1) - previousRema2)))
                / (lambda + 1);
        }

        return buffer;
    }

    /// <summary>
    /// Computes Modified Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeModifiedMaFast(StockData data, ComputeContext context, int length = 14)
    {
        // The modified moving average is Welles Wilder's under another name, and this spec is bound to
        // CalculateWellesWilderMovingAverage, which delegates to the routine below.
        // MovingAverageCore.ModifiedMovingAverage waits for a full window before it publishes anything, so it
        // read zero where the batch was already running.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.WellesWilderMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes ZigZag using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeZigZagFast(StockData data, ComputeContext context, int length = 5)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        TrendCore.ZigZag(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Chandelier Exit Long using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChandelierExitLongFast(StockData data, ComputeContext context, int length = 22,
        MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, double mult = 3)
    {
        // CalculateChandelierExit hangs its long stop a multiple of the average true range below the
        // highest high of the window, with whichever average it was given.
        var (_, highList, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        var count = highList.Count;

        using var atr = ComputeAtrFast(data, context, length, maType);
        using var highestBuffer = context.Rent(count);
        using var lowestBuffer = context.Rent(count);
        var highest = highestBuffer.WritableSpan;
        HighestAndLowest(data, highest, lowestBuffer.WritableSpan, length);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            output[i] = highest[i] - (atr.Span[i] * mult);
        }

        return buffer;
    }

    /// <summary>
    /// Computes Chandelier Exit Short using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChandelierExitShortFast(StockData data, ComputeContext context, int length = 22,
        MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, double mult = 3)
    {
        // The short stop is the same distance above the lowest low.
        var (_, _, lowList, _, _) = CalculationsHelper.GetInputValuesList(data);
        var count = lowList.Count;

        using var atr = ComputeAtrFast(data, context, length, maType);
        using var highestBuffer = context.Rent(count);
        using var lowestBuffer = context.Rent(count);
        var lowest = lowestBuffer.WritableSpan;
        HighestAndLowest(data, highestBuffer.WritableSpan, lowest, length);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            output[i] = lowest[i] + (atr.Span[i] * mult);
        }

        return buffer;
    }

    /// <summary>
    /// The highest high and the lowest low of the last <paramref name="length"/> bars at each index, as
    /// <see cref="CalculationsHelper.GetMaxAndMinValuesList(List{double}, List{double}, int)"/> reports them.
    /// </summary>
    /// <remarks>
    /// The window counts the current bar, and one that has not filled reports the extremes of what it holds
    /// rather than nothing.
    /// </remarks>
    private static void HighestAndLowest(StockData data, Span<double> highest, Span<double> lowest, int length)
    {
        var (_, highList, lowList, _, _) = CalculationsHelper.GetInputValuesList(data);
        HighestAndLowest(highList, lowList, highest, lowest, length);
    }

    /// <inheritdoc cref="HighestAndLowest(StockData, Span{double}, Span{double}, int)"/>
    private static void HighestAndLowest(List<double> highList, List<double> lowList, Span<double> highest,
        Span<double> lowest, int length)
    {
        var count = highList.Count == lowList.Count ? highList.Count : 0;
        var highWindow = new RollingMinMax(length);
        var lowWindow = new RollingMinMax(length);

        for (var i = 0; i < count; i++)
        {
            highWindow.Add(highList[i]);
            lowWindow.Add(lowList[i]);
            highest[i] = highWindow.Max;
            lowest[i] = lowWindow.Min;
        }
    }

    /// <summary>
    /// Computes Trend Intensity Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeTrendIntensityIndexFast(StockData data, ComputeContext context, int length = 30)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.TrendIntensityIndex(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Average Price using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAveragePriceFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length; // Average price doesn't use length
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.AveragePrice(open, close, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Pivot Point using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePivotPointFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length; // Pivot point doesn't use length
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        // This spec is bound to CalculateFloorPivotPoints, whose pivot is drawn from the preceding bar.
        // TrendCore.PivotPoint is the typical price of the arriving bar, which is a different series.
        TrendCore.FloorPivotPoint(high, low, close, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Range using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeRangeFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length; // Range doesn't use length
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        TrendCore.Range(high, low, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Price Momentum using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePriceMomentumFast(StockData data, ComputeContext context, int length = 10)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.PriceMomentum(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Midpoint using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMidpointFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.Midpoint(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Midprice using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMidpriceFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        TrendCore.Midprice(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Volume Fast Path Methods (Additional)

    /// <summary>
    /// Computes Money Flow Index using zero-allocation fast path via VolumeCore.
    /// </summary>
    internal static ComputeBuffer ComputeMfiCoreFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.MoneyFlowIndex(high, low, close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Trade Volume Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeTradeVolumeIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length; // TVI uses minTickValue, not length
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.TradeVolumeIndex(close, volume, buffer.WritableSpan, 0.5);
        return buffer;
    }

    /// <summary>
    /// Computes Volume Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVolumeOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.VolumeOscillator(volume, buffer.WritableSpan, 5, length);
        return buffer;
    }

    /// <summary>
    /// Computes Volume Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVwmaFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.VolumeWeightedMovingAveragePrice(close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Twiggs Money Flow using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeTwiggsMoneyFlowFast(StockData data, ComputeContext context, int length = 21)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.TwiggsMoneyFlow(high, low, close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Volume Zone Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVolumeZoneOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.VolumeZoneOscillator(close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Demand Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDemandIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length; // Demand Index doesn't use length
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.DemandIndex(high, low, close, volume, buffer.WritableSpan);
        return buffer;
    }

    #endregion

    #region Oscillator Fast Path Methods (Additional Batch 5)

    /// <summary>
    /// Computes Disparity Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDisparityIndexFast(StockData data, ComputeContext context, int length = 14,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // CalculateDisparityIndex measures the input series against an average of it, of whichever type it was
        // given. This read the close rather than the input series as well, so a chained indicator measured a
        // price the caller had already replaced.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var count = inputList.Count;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        using var averageBuffer = context.Rent(count);
        var average = averageBuffer.WritableSpan;
        MovingAverage(data, maType, length, inputSpan, average);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            output[i] = average[i] != 0 ? (inputSpan[i] - average[i]) / average[i] * 100 : 0;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Directional Trend Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDirectionalTrendIndexFast(StockData data, ComputeContext context,
        int length1 = 14, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length2 = 10,
        int length3 = 5)
    {
        // CalculateDirectionalTrendIndex nets how far each bar reached beyond the last bar's range in each
        // direction, then smooths that net and its magnitude three times over, and reports what share of the
        // movement was directional.
        var (_, highList, lowList, _, _) = CalculationsHelper.GetInputValuesList(data);
        var count = highList.Count;

        using var netBuffer = context.Rent(count);
        using var magnitudeBuffer = context.Rent(count);
        var net = netBuffer.WritableSpan;
        var magnitude = magnitudeBuffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;

            var highMovedUp = currentHigh - prevHigh > 0 ? currentHigh - prevHigh : 0;
            var lowMovedDown = currentLow - prevLow < 0 ? (currentLow - prevLow) * -1 : 0;

            net[i] = highMovedUp - lowMovedDown;
            magnitude[i] = Math.Abs(net[i]);
        }

        // The three smoothings alternate between the two pairs of buffers rather than renting six.
        using var smoothedNetBuffer = context.Rent(count);
        using var smoothedMagnitudeBuffer = context.Rent(count);
        var smoothedNet = smoothedNetBuffer.WritableSpan;
        var smoothedMagnitude = smoothedMagnitudeBuffer.WritableSpan;

        MovingAverage(data, maType, length1, net, smoothedNet);
        MovingAverage(data, maType, length1, magnitude, smoothedMagnitude);
        MovingAverage(data, maType, length2, smoothedNet, net);
        MovingAverage(data, maType, length2, smoothedMagnitude, magnitude);
        MovingAverage(data, maType, length3, net, smoothedNet);
        MovingAverage(data, maType, length3, magnitude, smoothedMagnitude);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            output[i] = smoothedMagnitude[i] != 0
                ? MathHelper.MinOrMax(100 * smoothedNet[i] / smoothedMagnitude[i], 100, -100)
                : 0;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Double Smoothed Stochastic using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDoubleSmoothedStochasticFast(StockData data, ComputeContext context,
        int length1 = 2, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length2 = 3,
        int length3 = 15)
    {
        // CalculateDoubleSmoothedStochastic smooths the stochastic's numerator and denominator separately -
        // twice each - and divides only then, so it is not a smoothed stochastic of a smoothed stochastic.
        var (inputList, highList, lowList, _, _) = CalculationsHelper.GetInputValuesList(data);
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var high = SpanCompat.AsReadOnlySpan(highList);
        var low = SpanCompat.AsReadOnlySpan(lowList);
        var count = inputList.Count;

        using var numeratorBuffer = context.Rent(count);
        using var denominatorBuffer = context.Rent(count);
        using var smoothedNumeratorBuffer = context.Rent(count);
        using var smoothedDenominatorBuffer = context.Rent(count);
        var numerator = numeratorBuffer.WritableSpan;
        var denominator = denominatorBuffer.WritableSpan;
        var highWindow = new RollingMinMax(length1);
        var lowWindow = new RollingMinMax(length1);

        for (var i = 0; i < count; i++)
        {
            highWindow.Add(high[i]);
            lowWindow.Add(low[i]);
            numerator[i] = input[i] - lowWindow.Min;
            denominator[i] = highWindow.Max - lowWindow.Min;
        }

        MovingAverage(data, maType, length2, numerator, smoothedNumeratorBuffer.WritableSpan);
        MovingAverage(data, maType, length2, denominator, smoothedDenominatorBuffer.WritableSpan);
        MovingAverage(data, maType, length3, smoothedNumeratorBuffer.Span, numerator);
        MovingAverage(data, maType, length3, smoothedDenominatorBuffer.Span, denominator);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            output[i] = denominator[i] != 0
                ? MathHelper.MinOrMax(100 * numerator[i] / denominator[i], 100, 0)
                : 0;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Dynamic Momentum Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDynamicMomentumIndexFast(StockData data, ComputeContext context,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 5, int length2 = 10,
        int length3 = 14, int upLimit = 30, int dnLimit = 5)
    {
        // CalculateDynamicMomentumIndex is an RSI whose lookback is chosen bar by bar: length3 divided by the
        // smoothed deviation of the input, clamped between dnLimit and upLimit. A quiet market lengthens it.
        var (inputList, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = inputList.Count;

        using var deviationBuffer = context.Rent(count);
        using var smoothedDeviationBuffer = context.Rent(count);
        using var gainBuffer = context.Rent(count);
        using var lossBuffer = context.Rent(count);

        // The deviation of the window about its own mean, which is what GetStandardDeviationList computes
        // for the batch, not the residual from a moving average.
        VolatilityCore.StandardDeviation(input, deviationBuffer.WritableSpan, Math.Max(1, length1));
        MovingAverage(data, maType, length2, deviationBuffer.Span, smoothedDeviationBuffer.WritableSpan);

        var smoothedDeviation = smoothedDeviationBuffer.Span;
        var gain = gainBuffer.WritableSpan;
        var loss = lossBuffer.WritableSpan;
        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            var deviation = smoothedDeviation[i];

            // Clamped before the cast rather than after it: on a near-flat window length3 / deviation runs
            // past int.MaxValue, which is the overflow the batch guards with a try around the same cast.
            var period = deviation != 0
                ? (int)Math.Min(upLimit, Math.Ceiling(length3 / deviation))
                : 0;
            var lookback = Math.Max(Math.Min(period, upLimit), dnLimit);

            var change = CalculationsHelper.MinPastValues(i, 1, input[i] - (i >= 1 ? input[i - 1] : 0));
            gain[i] = change > 0 ? change : 0;
            loss[i] = change < 0 ? Math.Abs(change) : 0;

            var window = Math.Min(lookback, i + 1);
            double gainSum = 0;
            double lossSum = 0;
            for (var j = i - window + 1; j <= i; j++)
            {
                gainSum += gain[j];
                lossSum += loss[j];
            }

            var averageGain = gainSum / window;
            var averageLoss = lossSum / window;
            var strength = averageLoss != 0 ? averageGain / averageLoss : 0;

            output[i] = averageLoss == 0 ? 100 : averageGain == 0 ? 0 : 100 - (100 / (1 + strength));
        }

        return buffer;
    }

    /// <summary>
    /// Computes Ergodic Candlestick Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeErgodicCandlestickOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ErgodicCandlestickOscillator(open, close, buffer.WritableSpan, 5, length);
        return buffer;
    }

    /// <summary>
    /// Computes Demarker Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDemarkerFast(StockData data, ComputeContext context, int length = 20,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // CalculateDemarker asks what share of the recent movement was upwards: how far each bar reached
        // above the last high against how far it also fell below the last low. Both are smoothed with
        // whichever average it was given.
        var (_, highList, lowList, _, _) = CalculationsHelper.GetInputValuesList(data);
        var count = highList.Count;

        using var upBuffer = context.Rent(count);
        using var downBuffer = context.Rent(count);
        var up = upBuffer.WritableSpan;
        var down = downBuffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;

            up[i] = currentHigh > prevHigh ? currentHigh - prevHigh : 0;
            down[i] = currentLow < prevLow ? prevLow - currentLow : 0;
        }

        using var averageUpBuffer = context.Rent(count);
        using var averageDownBuffer = context.Rent(count);
        var averageUp = averageUpBuffer.WritableSpan;
        var averageDown = averageDownBuffer.WritableSpan;
        MovingAverage(data, maType, length, up, averageUp);
        MovingAverage(data, maType, length, down, averageDown);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            var total = averageUp[i] + averageDown[i];
            output[i] = total != 0 ? MathHelper.MinOrMax(averageUp[i] / total * 100, 100, 0) : 0;
        }

        return buffer;
    }

    #endregion

    #region Volatility Fast Path Methods (Additional)

    /// <summary>
    /// Computes Standard Error using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeStandardErrorCoreFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.StandardError(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Keltner Channel Width using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeKeltnerChannelWidthFast(StockData data, ComputeContext context, int length = 20)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.KeltnerChannelWidth(high, low, close, buffer.WritableSpan, length, 2);
        return buffer;
    }

    /// <summary>
    /// Computes Bollinger Bands Width using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeBollingerBandsWidthFast(StockData data, ComputeContext context, int length = 20,
        double stdDevMult = 2, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // CalculateBollingerBandsWidth publishes the span between the bands as a fraction of the middle band,
        // not as a percentage of it, and measures the chained series the bands were built on, not the close.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = inputList.Count;
        length = Math.Max(length, 1);

        using var middle = context.Rent(count);
        MovingAverage(data, maType, length, input, middle.WritableSpan);
        var ma = middle.Span;

        using var deviation = context.Rent(count);
        VolatilityCore.StandardDeviation(input, deviation.WritableSpan, length);
        var stdDev = deviation.Span;

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            output[i] = ma[i] != 0 ? 2 * stdDevMult * stdDev[i] / ma[i] : 0;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Relative Volatility Index using zero-allocation fast path via VolatilityCore.
    /// </summary>
    internal static ComputeBuffer ComputeRviVolatilityFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.RelativeVolatilityIndex(close, buffer.WritableSpan, length, 10);
        return buffer;
    }

    /// <summary>
    /// Computes Donchian Channel Width using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDonchianChannelWidthFast(StockData data, ComputeContext context, int length = 20)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.DonchianChannelWidth(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Mass Index using zero-allocation fast path via VolatilityCore.
    /// </summary>
    internal static ComputeBuffer ComputeMassIndexCoreFast(StockData data, ComputeContext context, int length = 25)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.MassIndex(high, low, buffer.WritableSpan, length, 9);
        return buffer;
    }

    /// <summary>
    /// Computes Close-to-Close Volatility using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeCloseToCloseVolatilityFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.CloseToCloseVolatility(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Parkinson Volatility using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeParkinsonVolatilityFast(StockData data, ComputeContext context, int length = 20)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.ParkinsonVolatility(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Garman-Klass Volatility using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeGarmanKlassVolatilityFast(StockData data, ComputeContext context, int length = 20)
    {
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.GarmanKlassVolatility(open, high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Moving Average Fast Path Methods (Additional Batch 3)

    /// <summary>
    /// Computes Jurik Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeJmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.JurikMovingAverage(close, buffer.WritableSpan, length, 0);
        return buffer;
    }

    /// <summary>
    /// Computes Butterworth Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeButterworthFilterFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.ButterworthFilter(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes SuperSmoother Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSuperSmootherFast(StockData data, ComputeContext context, int length = 10)
    {
        // CalculateEhlersSuperSmootherFilter runs its two-pole recursion from the very first bar with no
        // history at all, so the opening bars are filtered rather than copied through.
        // MovingAverageCore.SuperSmoother seeds the first two bars with the input instead, and it read the
        // close rather than the chained series, so neither the warm-up nor the input matched. Walking the
        // streaming engine keeps all three engines on one answer by construction.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = inputList.Count;

        var buffer = context.Rent(count);
        var filt = buffer.WritableSpan;

        var engine = new Streaming.EhlersSuperSmootherFilterEngine(length);
        for (var i = 0; i < count; i++)
        {
            filt[i] = engine.Next(input[i], isFinal: true);
        }

        return buffer;
    }

    /// <summary>
    /// Computes EndPoint Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEndPointMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EndpointMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Cubic Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeCubicWmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.CubicWeightedMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Natural Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeNaturalMaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.NaturalMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Elliott Wave Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeElliottWaveOscillatorFast(StockData data, ComputeContext context, int fastLength = 5, int slowLength = 35)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ElliottWaveOscillator(close, buffer.WritableSpan, fastLength, slowLength);
        return buffer;
    }

    /// <summary>
    /// Computes Forecast Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeForecastOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ForecastOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Derivative Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDerivativeOscillatorFast(StockData data, ComputeContext context,
        int length1 = 14, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length2 = 9,
        int length3 = 5, int length4 = 3)
    {
        // CalculateDerivativeOscillator smooths the relative strength index twice and then reports how far
        // that sits above its own length2 average, so it reads as a histogram around zero.
        using var rsi = ComputeRsiFast(data, context, length1, maType);
        var count = rsi.Span.Length;

        using var firstBuffer = context.Rent(count);
        MovingAverage(data, maType, length3, rsi.Span, firstBuffer.WritableSpan);

        using var secondBuffer = context.Rent(count);
        MovingAverage(data, maType, length4, firstBuffer.Span, secondBuffer.WritableSpan);
        var smoothed = secondBuffer.Span;

        var smoothedSum = new RollingSum();

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            smoothedSum.Add(smoothed[i]);
            output[i] = smoothed[i] - smoothedSum.Average(length2);
        }

        return buffer;
    }

    /// <summary>
    /// Computes Gator Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeGatorOscillatorFast(StockData data, ComputeContext context, int length = 13)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.GatorOscillator(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Fractal Chaos Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeFractalChaosOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length; // Not used in this oscillator
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.FractalChaosOscillator(high, low, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Rahul Mohindar Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeRahulMohindarOscillatorFast(StockData data, ComputeContext context, int length = 10)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.RahulMohindarOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Premier Stochastic Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePremierStochasticFast(StockData data, ComputeContext context, int length = 8)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.PremierStochastic(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Repulse Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeRepulseFast(StockData data, ComputeContext context, int length = 5)
    {
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.Repulse(open, high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Gann HiLo Activator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeGannHiLoActivatorFast(StockData data, ComputeContext context, int length = 3)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.GannHiLoActivator(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes HalfTrend indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeHalfTrendFast(StockData data, ComputeContext context, int length = 2)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.HalfTrend(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Vortex Indicator Positive using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVortexPositiveFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.VortexPositive(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Vortex Indicator Negative using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVortexNegativeFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.VortexNegative(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Linear Regression Intercept using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeLinRegInterceptFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.LinearRegressionIntercept(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Elder Impulse System using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeElderImpulseSystemFast(StockData data, ComputeContext context, int length = 13)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.ElderImpulseSystem(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ichimoku Tenkan-sen using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeIchimokuTenkanSenFast(StockData data, ComputeContext context, int length = 9)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        TrendCore.IchimokuTenkanSen(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ichimoku Kijun-sen using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeIchimokuKijunSenFast(StockData data, ComputeContext context, int length = 26)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        TrendCore.IchimokuKijunSen(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Mass Thrust Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMassThrustFast(StockData data, ComputeContext context, int length = 10)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        TrendCore.MassThrust(close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Volume Indicators - Additional Batch 2

    /// <summary>
    /// Computes Williams Accumulation/Distribution using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeWilliamsADFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolumeCore.WilliamsAD(high, low, close, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Net Volume using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeNetVolumeFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.NetVolume(close, volume, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Cumulative Volume Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeCumulativeVolumeIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.CumulativeVolumeIndex(close, volume, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Volume Momentum using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVolumeMomentumFast(StockData data, ComputeContext context, int length = 10)
    {
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.VolumeMomentum(volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Volume Price Trend using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVolumePriceTrendFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.VolumePriceTrend(close, volume, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Elder Ray Bull Power using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeElderRayBullPowerFast(StockData data, ComputeContext context, int length = 13)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolumeCore.ElderRayBullPower(high, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Elder Ray Bear Power using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeElderRayBearPowerFast(StockData data, ComputeContext context, int length = 13)
    {
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolumeCore.ElderRayBearPower(low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Normalized Volume using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeNormalizedVolumeFast(StockData data, ComputeContext context, int length = 20)
    {
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.NormalizedVolume(volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Volume Weighted RSI using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVolumeWeightedRsiFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.VolumeWeightedRsi(close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Oscillators - Additional Batch 7

    /// <summary>
    /// Computes Chande Composite Momentum Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChandeCompositeMomentumIndexFast(StockData data, ComputeContext context,
        int length1 = 5, int length2 = 10, int length3 = 20,
        MovingAvgType maType = MovingAvgType.DoubleExponentialMovingAverage, int smoothLength = 3)
    {
        // CalculateChandeCompositeMomentumIndex weighs three momentum oscillators of different lengths by how
        // volatile the price was over the matching window, then publishes an exponential average of that
        // weighted reading. The three oscillators are smoothed with whichever average it was given.
        var (inputList, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        var count = inputList.Count;
        var input = SpanCompat.AsReadOnlySpan(inputList);

        using var deviation1Buffer = context.Rent(count);
        using var deviation2Buffer = context.Rent(count);
        using var deviation3Buffer = context.Rent(count);
        var deviation1 = deviation1Buffer.WritableSpan;
        var deviation2 = deviation2Buffer.WritableSpan;
        var deviation3 = deviation3Buffer.WritableSpan;
        VolatilityCore.StandardDeviation(input, deviation1, Math.Max(1, length1));
        VolatilityCore.StandardDeviation(input, deviation2, Math.Max(1, length2));
        VolatilityCore.StandardDeviation(input, deviation3, Math.Max(1, length3));

        using var ratio1Buffer = context.Rent(count);
        using var ratio2Buffer = context.Rent(count);
        using var ratio3Buffer = context.Rent(count);
        var ratio1 = ratio1Buffer.WritableSpan;
        var ratio2 = ratio2Buffer.WritableSpan;
        var ratio3 = ratio3Buffer.WritableSpan;

        var gainSum = new RollingSum();
        var lossSum = new RollingSum();

        for (var i = 0; i < count; i++)
        {
            var currentValue = input[i];

            // There is nothing to move from on the first bar.
            var previousValue = i >= 1 ? input[i - 1] : 0;
            gainSum.Add(currentValue > previousValue ? CalculationsHelper.MinPastValues(i, 1, currentValue - previousValue) : 0);
            lossSum.Add(currentValue < previousValue ? CalculationsHelper.MinPastValues(i, 1, previousValue - currentValue) : 0);

            ratio1[i] = MomentumRatio(gainSum.Sum(length1), lossSum.Sum(length1));
            ratio2[i] = MomentumRatio(gainSum.Sum(length2), lossSum.Sum(length2));
            ratio3[i] = MomentumRatio(gainSum.Sum(length3), lossSum.Sum(length3));
        }

        using var smoothed1Buffer = context.Rent(count);
        using var smoothed2Buffer = context.Rent(count);
        using var smoothed3Buffer = context.Rent(count);
        var smoothed1 = smoothed1Buffer.WritableSpan;
        var smoothed2 = smoothed2Buffer.WritableSpan;
        var smoothed3 = smoothed3Buffer.WritableSpan;
        MovingAverage(data, maType, smoothLength, ratio1, smoothed1);
        MovingAverage(data, maType, smoothLength, ratio2, smoothed2);
        MovingAverage(data, maType, smoothLength, ratio3, smoothed3);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            var weight = deviation1[i] + deviation2[i] + deviation3[i];
            var index = weight != 0
                ? MathHelper.MinOrMax(((deviation1[i] * smoothed1[i]) + (deviation2[i] * smoothed2[i])
                    + (deviation3[i] * smoothed3[i])) / weight, 100, -100)
                : 0;

            // The exponential average starts from nothing rather than from the first reading.
            var previous = i >= 1 ? output[i - 1] : 0;
            output[i] = CalculationsHelper.CalculateEMA(index, previous, smoothLength);
        }

        return buffer;
    }

    /// <summary>
    /// Scales the gains and losses of one momentum window into the range the Chande oscillators publish.
    /// </summary>
    private static double MomentumRatio(double gains, double losses)
    {
        return gains + losses != 0 ? MathHelper.MinOrMax(100 * (gains - losses) / (gains + losses), 100, -100) : 0;
    }

    /// <summary>
    /// Computes Chande Kroll R-Squared Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChandeKrollRSquaredIndexFast(StockData data, ComputeContext context,
        int length = 14, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int smoothLength = 3)
    {
        // CalculateChandeKrollRSquaredIndex squares the correlation between the price and the bar number, so
        // it reads how straight the last length bars have been, and then smooths that with whichever average
        // it was given.
        var (inputList, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        var count = inputList.Count;

        using var rawBuffer = context.Rent(count);
        var raw = rawBuffer.WritableSpan;
        var correlation = new RollingCorrelation();

        for (var i = 0; i < count; i++)
        {
            correlation.Add(i, inputList[i]);
            var rSquared = correlation.RSquared(length);
            raw[i] = MathHelper.IsValueNullOrInfinity(rSquared) ? 0 : rSquared;
        }

        var buffer = context.Rent(count);
        MovingAverage(data, maType, smoothLength, raw, buffer.WritableSpan);

        return buffer;
    }

    /// <summary>
    /// Computes Bayesian Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeBayesianOscillatorFast(StockData data, ComputeContext context, int length = 20,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, double stdDevMult = 2.5)
    {
        // CalculateBayesianOscillator counts how often the price sat above and below its upper Bollinger band
        // and its basis, turns those counts into probabilities and combines them. The bands take whichever
        // average they were given, so a hardcoded one could only ever answer for itself. This arm is bound to
        // the downward sigma probability, which is the series the indicator publishes first.
        var (inputList, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        var count = inputList.Count;
        var input = SpanCompat.AsReadOnlySpan(inputList);

        using var basisBuffer = context.Rent(count);
        using var deviationBuffer = context.Rent(count);
        var basisSeries = basisBuffer.WritableSpan;
        var deviation = deviationBuffer.WritableSpan;
        MovingAverage(data, maType, length, input, basisSeries);
        VolatilityCore.StandardDeviation(input, deviation, Math.Max(1, length));

        var upperAbove = new RollingSum();
        var upperBelow = new RollingSum();
        var basisAbove = new RollingSum();
        var basisBelow = new RollingSum();

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            var currentValue = input[i];
            var basis = basisSeries[i];
            var upperBand = basis + (deviation[i] * stdDevMult);

            upperAbove.Add(currentValue > upperBand ? 1 : 0);
            upperBelow.Add(currentValue < upperBand ? 1 : 0);
            var aboveUpper = upperAbove.Average(length);
            var belowUpper = upperBelow.Average(length);
            var probUpBbUpper = aboveUpper + belowUpper != 0 ? aboveUpper / (aboveUpper + belowUpper) : 0;

            basisAbove.Add(currentValue > basis ? 1 : 0);
            basisBelow.Add(currentValue < basis ? 1 : 0);
            var aboveBasis = basisAbove.Average(length);
            var belowBasis = basisBelow.Average(length);
            var probUpBbBasis = aboveBasis + belowBasis != 0 ? aboveBasis / (aboveBasis + belowBasis) : 0;

            output[i] = probUpBbUpper != 0 && probUpBbBasis != 0
                ? ((probUpBbUpper * probUpBbBasis) / (probUpBbUpper * probUpBbBasis))
                    + ((1 - probUpBbUpper) * (1 - probUpBbBasis))
                : 0;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Anchored Momentum using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAnchoredMomentumFast(StockData data, ComputeContext context,
        int momentumLength = 10, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int smoothLength = 7)
    {
        // CalculateAnchoredMomentum measures a short average of the price against a simple average anchored
        // over (2 * momentumLength) + 1 bars, and the short average takes whichever type it was given.
        var (inputList, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        var count = inputList.Count;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var anchorLength = MathHelper.MinOrMax((2 * momentumLength) + 1);

        using var smoothBuffer = context.Rent(count);
        var smoothed = smoothBuffer.WritableSpan;
        MovingAverage(data, maType, smoothLength, input, smoothed);

        var anchorWindow = new RollingSum();
        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            anchorWindow.Add(input[i]);

            // An anchor window that has not filled averages what it holds rather than nothing.
            var anchor = anchorWindow.Average(anchorLength);
            output[i] = anchor != 0 ? 100 * ((smoothed[i] / anchor) - 1) : 0;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Chartmill Value Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChartmillValueIndicatorFast(StockData data, ComputeContext context,
        int length = 5, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // CalculateChartmillValueIndicator measures how far the close sits from an average of the median
        // price, in units of the average true range widened by the square root of the length. Both the
        // average and the range take whichever type the indicator was given.
        var (inputList, _, _, _, closeList, _) =
            CalculationsHelper.GetInputValuesList(InputName.MedianPrice, data);
        var count = inputList.Count;

        using var atr = ComputeAtrFast(data, context, length, maType);
        var atrSpan = atr.Span;

        using var fBuffer = context.Rent(count);
        var f = fBuffer.WritableSpan;
        MovingAverage(data, maType, length, SpanCompat.AsReadOnlySpan(inputList), f);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            var v = atrSpan[i];
            output[i] = v != 0
                ? MathHelper.MinOrMax((closeList[i] - f[i]) / (v * MathHelper.Pow(length, 0.5)), 1, -1)
                : 0;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Center of Linearity using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeCenterOfLinearityFast(StockData data, ComputeContext context, int length = 14)
    {
        // CalculateCenterOfLinearity weights the distance between the bar a whole length ago and the bar just
        // gone by the bar number itself, and sums that over the window - a running total whose weights grow
        // with the series rather than resetting inside the window. OscillatorCore.CenterOfLinearity read the
        // close and weighted by position within the window instead.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = inputList.Count;
        length = Math.Max(length, 1);

        var buffer = context.Rent(count);
        var col = buffer.WritableSpan;

        var weightedSum = new RollingSum();
        for (var i = 0; i < count; i++)
        {
            var previousValue = i >= 1 ? input[i - 1] : 0;
            var priorValue = i >= length ? input[i - length] : 0;

            weightedSum.Add((i + 1) * (priorValue - previousValue));
            col[i] = weightedSum.Sum(length);
        }

        return buffer;
    }

    /// <summary>
    /// Computes Breakout RSI using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeBreakoutRsiFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.BreakoutRsi(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Asymmetrical RSI using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAsymmetricalRsiFast(StockData data, ComputeContext context, int upLength = 14, int downLength = 7)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.AsymmetricalRsi(close, buffer.WritableSpan, upLength, downLength);
        return buffer;
    }

    /// <summary>
    /// Computes Adaptive Stochastic using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAdaptiveStochasticFast(StockData data, ComputeContext context, int minLength = 5, int maxLength = 20)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.AdaptiveStochastic(high, low, close, buffer.WritableSpan, minLength, maxLength);
        return buffer;
    }

    /// <summary>
    /// Computes Adaptive RSI using zero-allocation fast path.
    /// </summary>
    #endregion

    #region Trend - Additional Batch 4

    /// <summary>
    /// Computes Chande Trend Score using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChandeTrendScoreFast(StockData data, ComputeContext context,
        int startLength = 11, int endLength = 20)
    {
        // CalculateChandeTrendScore scores one point for every lookback between startLength and endLength the
        // price now sits above, and one against for every one it sits below. Before the series is that long
        // the missing bar counts as zero, so the price is above it and the point is scored.
        var (inputList, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        var count = inputList.Count;

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            var currentValue = inputList[i];
            var score = 0d;

            for (var j = startLength; j <= endLength; j++)
            {
                var priorValue = i >= j ? inputList[i - j] : 0;
                score += currentValue >= priorValue ? 1 : -1;
            }

            output[i] = score;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Chop Zone using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChopZoneFast(StockData data, ComputeContext context, int length1 = 30,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length2 = 34)
    {
        // CalculateChopZone measures the angle of an average of the close, scaled by where the typical price
        // sits inside the length1 range, and the average takes the type it was given.
        var (inputList, highList, lowList, _, closeList, _) =
            CalculationsHelper.GetInputValuesList(InputName.TypicalPrice, data);
        var count = inputList.Count;

        using var highestBuffer = context.Rent(count);
        using var lowestBuffer = context.Rent(count);
        var highest = highestBuffer.WritableSpan;
        var lowest = lowestBuffer.WritableSpan;
        HighestAndLowest(highList, lowList, highest, lowest, length1);

        using var averageBuffer = context.Rent(count);
        var average = averageBuffer.WritableSpan;
        MovingAverage(data, maType, length2, SpanCompat.AsReadOnlySpan(closeList), average);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            var span = highest[i] - lowest[i];
            var range = span != 0 ? 25 / span * lowest[i] : 0;
            var avg = inputList[i];
            var previous = i >= 1 ? average[i - 1] : 0;
            var y = avg != 0 && range != 0 ? (previous - average[i]) / avg * range : 0;
            var c = Math.Sqrt(1 + (y * y));
            var angle = c != 0 ? Math.Round(Math.Acos(1 / c).ToDegrees()) : 0;

            // A falling average tilts the angle the other way.
            output[i] = y > 0 ? -angle : angle;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Auto Line using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAutoLineFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.AutoLine(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Auto Line with Drift using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAutoLineWithDriftFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.AutoLineWithDrift(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Auto Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAutoFilterFast(StockData data, ComputeContext context, int length = 500,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // CalculateAutoFilter regresses the price on a stepped copy of itself that only moves when the price
        // leaves a standard deviation band around the last step, and both of its averages take whichever type
        // it was given.
        var (inputList, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        var count = inputList.Count;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var window = Math.Max(1, length);

        using var deviationBuffer = context.Rent(count);
        var deviation = deviationBuffer.WritableSpan;
        VolatilityCore.StandardDeviation(input, deviation, window);

        using var stepBuffer = context.Rent(count);
        using var correlationBuffer = context.Rent(count);
        var step = stepBuffer.WritableSpan;
        var correlations = correlationBuffer.WritableSpan;
        var correlation = new RollingCorrelation();

        for (var i = 0; i < count; i++)
        {
            var currentValue = input[i];

            // The step starts on the price itself and then holds until the price leaves the band.
            var previousStep = i >= 1 ? step[i - 1] : currentValue;
            step[i] = currentValue > previousStep + deviation[i] || currentValue < previousStep - deviation[i]
                ? currentValue
                : previousStep;

            correlation.Add(currentValue, step[i]);
            var r = correlation.R(length);
            correlations[i] = MathHelper.IsValueNullOrInfinity(r) ? 0 : r;
        }

        using var priceAverageBuffer = context.Rent(count);
        using var stepAverageBuffer = context.Rent(count);
        using var stepDeviationBuffer = context.Rent(count);
        var priceAverage = priceAverageBuffer.WritableSpan;
        var stepAverage = stepAverageBuffer.WritableSpan;
        var stepDeviation = stepDeviationBuffer.WritableSpan;
        MovingAverage(data, maType, length, input, priceAverage);
        MovingAverage(data, maType, length, step, stepAverage);
        VolatilityCore.StandardDeviation(step, stepDeviation, window);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            var slope = stepDeviation[i] != 0 ? correlations[i] * (deviation[i] / stepDeviation[i]) : 0;
            var intercept = priceAverage[i] - (slope * stepAverage[i]);
            output[i] = (step[i] * slope) + intercept;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Buff Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeBuffAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.BuffAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Bryant Adaptive Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeBryantAdaptiveMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.BryantAdaptiveMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes ATR Trailing Stops using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAtrTrailingStopsFast(StockData data, ComputeContext context, int length2 = 21,
        double factor = 3, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        // Both specs name IndicatorName.AverageTrueRangeTrailingStops, so there is one answer to give.
        return ComputeAverageTrueRangeTrailingStopsFast(data, context, length2, factor, maType);
    }

    /// <summary>
    /// Computes Compound Ratio Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeCompoundRatioMovingAverageFast(StockData data, ComputeContext context,
        int length = 20, MovingAvgType maType = MovingAvgType.WeightedMovingAverage)
    {
        // CalculateCompoundRatioMovingAverage weights the window by a compounding ratio rather than by
        // position, and then smooths that raw wave over the square root of the length with whichever average
        // it was given. The series the spec is bound to is the smoothed one.
        var (inputList, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        var count = inputList.Count;

        var r = MathHelper.Pow(length, ((double)1 / (length - 1)) - 1);
        var smoothLength = Math.Max((int)Math.Round(Math.Sqrt(length)), 1);
        var bas = 1 + (r * 2);

        using var rawBuffer = context.Rent(count);
        var raw = rawBuffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var weight = MathHelper.Pow(bas, length - j);

                // Bars before the start of the series count as zero, which is what the batch does.
                var previousValue = i >= j ? inputList[i - j] : 0;

                sum += previousValue * weight;
                weightedSum += weight;
            }

            raw[i] = weightedSum != 0 ? sum / weightedSum : 0;
        }

        var buffer = context.Rent(count);
        MovingAverage(data, maType, smoothLength, raw, buffer.WritableSpan);

        return buffer;
    }

    /// <summary>
    /// Computes Conditional Accumulator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeConditionalAccumulatorFast(StockData data, ComputeContext context,
        double increment = 1)
    {
        // CalculateConditionalAccumulator adds an increment for every bar that gaps clear of the previous
        // bar's range and subtracts one for every bar that gaps below it, and the close never enters it. The
        // length and the average type it also takes only smooth its signal line, which is a different series.
        var (_, highList, lowList, _, _) = CalculationsHelper.GetInputValuesList(data);
        var count = highList.Count;

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        double value = 0;

        for (var i = 0; i < count; i++)
        {
            // The first bar has no predecessor and therefore cannot have gapped.
            if (i >= 1)
            {
                if (lowList[i] > highList[i - 1])
                {
                    value += increment;
                }
                else if (highList[i] < lowList[i - 1])
                {
                    value -= increment;
                }
            }

            output[i] = value;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Ahrens Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAhrensMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        // CalculateAhrensMovingAverage steps its own last value towards the chained series by a length-th of
        // the distance from the midpoint between that last value and the one a whole length ago. Before a full
        // length has passed the prior value is the current bar itself, so the average starts from nothing
        // rather than from price. TrendCore.AhrensMovingAverage read the close and seeded with it.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = inputList.Count;
        length = Math.Max(length, 1);

        var buffer = context.Rent(count);
        var ahma = buffer.WritableSpan;

        double previousAhma = 0;
        for (var i = 0; i < count; i++)
        {
            var currentValue = input[i];
            var priorAhma = i >= length ? ahma[i - length] : currentValue;

            previousAhma += (currentValue - ((previousAhma + priorAhma) / 2)) / length;
            ahma[i] = previousAhma;
        }

        return buffer;
    }

    #endregion

    #region Volatility - Additional Batch 2

    /// <summary>
    /// Computes Rogers-Satchell Volatility using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeRogersSatchellVolatilityFast(StockData data, ComputeContext context, int length = 20)
    {
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.RogersSatchellVolatility(open, high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Yang-Zhang Volatility using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeYangZhangVolatilityFast(StockData data, ComputeContext context, int length = 20)
    {
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.YangZhangVolatility(open, high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Calmar Ratio using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeCalmarRatioFast(StockData data, ComputeContext context, int length = 252)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.CalmarRatio(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Downside Deviation using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDownsideDeviationFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.DownsideDeviation(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes ATR Channel Width using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAtrChannelWidthFast(StockData data, ComputeContext context, int length = 14, double multiplier = 2)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.AtrChannelWidth(high, low, close, buffer.WritableSpan, length, multiplier);
        return buffer;
    }

    /// <summary>
    /// Computes Commodity Selection Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeCommoditySelectionIndexFast(StockData data, ComputeContext context,
        int length = 14, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, double pointValue = 50,
        double margin = 3000, double commission = 10)
    {
        // CalculateCommoditySelectionIndex scales the average true range by the trend strength the average
        // directional index reports and by a constant built from the contract's economics, so the arm reuses
        // the two arms that already answer for those indicators rather than smoothing anything itself.
        var k = 100 * (pointValue / MathHelper.Sqrt(margin) / (150 + commission));

        using var atr = ComputeAtrFast(data, context, length, maType);
        using var adx = ComputeAdxFast(data, context, length, maType);
        var atrSpan = atr.Span;
        var adxSpan = adx.Span;
        var count = atrSpan.Length;

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            output[i] = k * atrSpan[i] * adxSpan[i];
        }

        return buffer;
    }

    #endregion

    #region Moving Averages - Additional Batch 4

    /// <summary>
    /// Computes Alpha Decreasing EMA using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAlphaDecreasingEmaFast(StockData data, ComputeContext context)
    {
        // CalculateAlphaDecreasingExponentialMovingAverage takes no length at all: its smoothing constant is
        // two over the bar number, which starts at two and decays for the whole series. That is why the spec
        // marks its length as having no effect, and why it is no longer passed here.
        // MovingAverageCore.AlphaDecreasingEma is a length-driven average of the close, a different series
        // from its first bar on.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = inputList.Count;

        var buffer = context.Rent(count);
        var ema = buffer.WritableSpan;

        double previousEma = 0;
        for (var i = 0; i < count; i++)
        {
            var alpha = (double)2 / (i + 1);
            previousEma = (alpha * input[i]) + ((1 - alpha) * previousEma);
            ema[i] = previousEma;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Adaptive EMA using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAdaptiveEmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.AdaptiveExponentialMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Autonomous Recursive MA using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAutonomousRecursiveMaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.AutonomousRecursiveMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Adaptive Least Squares using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAdaptiveLeastSquaresFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.AdaptiveLeastSquares(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes ATR Filtered EMA using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAtrFilteredEmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.AtrFilteredEma(close, high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Median Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMedianMaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.MedianMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Volume Adjusted Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVolumeAdjustedMaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.VolumeAdjustedMovingAverage(close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Quadratic Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeQuadraticWmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.QuadraticWeightedMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Parabolic Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeParabolicWmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.ParabolicWeightedMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Oscillators - Additional Batch 8

    internal static ComputeBuffer ComputeSmoothedWilliamsRFast(StockData data, ComputeContext context, int length = 14, int smoothLength = 3)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.SmoothedWilliamsR(high, low, close, buffer.WritableSpan, length, smoothLength);
        return buffer;
    }

    internal static ComputeBuffer ComputePriceOscillatorPercentFast(StockData data, ComputeContext context, int shortLength = 10, int longLength = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.PriceOscillatorPercent(close, buffer.WritableSpan, shortLength, longLength);
        return buffer;
    }

    internal static ComputeBuffer ComputeNormalizedMacdFast(StockData data, ComputeContext context, int fastLength = 12, int slowLength = 26)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.NormalizedMacd(close, buffer.WritableSpan, fastLength, slowLength);
        return buffer;
    }

    internal static ComputeBuffer ComputeRelativeVigorIndexSignalFast(StockData data, ComputeContext context, int length = 10, int signalLength = 4)
    {
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.RelativeVigorIndexSignal(open, high, low, close, buffer.WritableSpan, length, signalLength);
        return buffer;
    }

    internal static ComputeBuffer ComputeVolumeMomentumOscillatorFast(StockData data, ComputeContext context, int shortLength = 5, int longLength = 20)
    {
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        OscillatorCore.VolumeMomentumOscillator(volume, buffer.WritableSpan, shortLength, longLength);
        return buffer;
    }

    internal static ComputeBuffer ComputeTrendContinuationFactorFast(StockData data, ComputeContext context, int length = 35)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.TrendContinuationFactor(close, buffer.WritableSpan, length);
        return buffer;
    }

    internal static ComputeBuffer ComputeTrendPersistenceRateFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.TrendPersistenceRate(close, buffer.WritableSpan, length);
        return buffer;
    }

    internal static ComputeBuffer ComputeInertiaFast(StockData data, ComputeContext context, int length = 20, int rviLength = 14,
        MovingAvgType maType = MovingAvgType.LinearRegression)
    {
        // CalculateInertiaIndicator smooths the second relative volatility index - taken over its own default
        // ten-bar window, with rviLength as its smoothing length - by a linear regression over the length.
        // OscillatorCore.Inertia measured something else from the close, and the two overloads this replaced
        // disagreed with each other as well as with the batch: one dropped the moving average type outright
        // and the other read the ticker list rather than the prices already on hand.
        using var relativeVolatility = ComputeRelativeVolatilityIndexV2Fast(data, context,
            smoothLength: Math.Max(1, rviLength));

        var buffer = context.Rent(data.Count);
        MovingAverage(data, maType, length, relativeVolatility.Span, buffer.WritableSpan);

        return buffer;
    }

    #endregion

    #region Volatility - Additional Batch 3

    internal static ComputeBuffer ComputeStandardDeviationChannelFast(StockData data, ComputeContext context, int length = 20,
        double stdDevMult = 2, ChannelBand band = ChannelBand.Middle)
    {
        // CalculateStandardDeviationChannel centres its channel on the linear regression of the chained
        // series - the fitted value at the current bar, taken through the same RollingLeastSquares the batch
        // uses so the opening window is fitted identically - and offsets the outer bands by a multiple of the
        // population standard deviation of that window. VolatilityCore.StandardDeviationChannel took the
        // close and measured something else entirely.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = inputList.Count;
        length = Math.Max(length, 1);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        using (var regression = new RollingLeastSquares(length))
        {
            for (var i = 0; i < count; i++)
            {
                output[i] = regression.Next(input[i], isFinal: true).Last;
            }
        }

        if (band == ChannelBand.Middle)
        {
            return buffer;
        }

        using var deviation = context.Rent(count);
        VolatilityCore.StandardDeviation(input, deviation.WritableSpan, length);
        var stdDev = deviation.Span;
        var multiplier = band == ChannelBand.Upper ? stdDevMult : -stdDevMult;
        for (var i = 0; i < count; i++)
        {
            output[i] += multiplier * stdDev[i];
        }

        return buffer;
    }

    internal static ComputeBuffer ComputeAverageTrueRangeChannelFast(StockData data, ComputeContext context, int length = 14,
        double mult = 2.5, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // The upper band this arm is bound to is the input a multiple of the average true range above
        // itself, rounded to whole units as CalculateAverageTrueRangeChannel rounds it.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var count = inputList.Count;
        var input = SpanCompat.AsReadOnlySpan(inputList);

        using var atr = ComputeAtrFast(data, context, length, maType);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            output[i] = Math.Round(input[i] + (atr.Span[i] * mult));
        }

        return buffer;
    }

    internal static ComputeBuffer ComputeVolatilityRatioFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.VolatilityRatio(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    internal static ComputeBuffer ComputeVolatilityStopFast(StockData data, ComputeContext context, int length = 14, double multiplier = 2)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.VolatilityStop(high, low, close, buffer.WritableSpan, length, multiplier);
        return buffer;
    }

    /// <summary>
    /// Computes Bollinger Bands %B using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeBollingerBandsPercentBFast(StockData data, ComputeContext context, int length = 20, double multiplier = 2)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        VolatilityCore.BollingerBandsPercentB(inputSpan, buffer.WritableSpan, length, multiplier);
        return buffer;
    }

    /// <summary>
    /// Computes Bollinger Bands with ATR using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeBollingerBandsAtrFast(StockData data, ComputeContext context, int length = 55,
        double stdDevMult = 2, int atrLength = 22, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // CalculateBollingerBandsAvgTrueRange divides the average true range by the span between the bands and
        // publishes nothing while that span is still closed, which is the whole of the opening window.
        var count = data.Count;

        using var upperBand = BollingerBand(data, context, length, stdDevMult, maType);
        using var lowerBand = BollingerBand(data, context, length, -stdDevMult, maType);
        using var averageTrueRange = ComputeAtrFast(data, context, Math.Max(atrLength, 1), maType);
        var upper = upperBand.Span;
        var lower = lowerBand.Span;
        var atr = averageTrueRange.Span;

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            var bbDiff = upper[i] - lower[i];
            output[i] = bbDiff != 0 ? atr[i] / bbDiff : 0;
        }

        return buffer;
    }

    #endregion

    #region Additional Oscillators (Batch 1)

    /// <summary>
    /// Computes Absolute Chande Momentum Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChandeMomentumOscillatorAbsoluteFast(StockData data, ComputeContext context,
        int length = 9)
    {
        // CalculateChandeMomentumOscillatorAbsolute measures the whole move over the window against the ground
        // covered bar by bar, so it reads how directly the price travelled rather than which way it went.
        var (inputList, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        var count = inputList.Count;

        var magnitudeSum = new RollingSum();

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            var currentValue = inputList[i];

            // Neither the previous bar nor the bar a whole window back exists at the start of the series.
            var previousValue = i >= 1 ? inputList[i - 1] : 0;
            var priorValue = i >= length ? inputList[i - length] : 0;
            magnitudeSum.Add(Math.Abs(CalculationsHelper.MinPastValues(i, 1, currentValue - previousValue)));

            var travelled = magnitudeSum.Sum(length);
            var moved = Math.Abs(100 * CalculationsHelper.MinPastValues(i, length, currentValue - priorValue));
            output[i] = travelled != 0 ? MathHelper.MinOrMax(moved / travelled, 100, 0) : 0;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Percent Change using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePercentChangeFast(StockData data, ComputeContext context, int length = 1)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.PercentChange(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Price Change using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePriceChangeFast(StockData data, ComputeContext context)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.PriceChange(inputSpan, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Range (High - Low) using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeRangeFast(StockData data, ComputeContext context)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.Range(high, low, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Mid-Range using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMidRangeFast(StockData data, ComputeContext context)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.MidRange(high, low, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes OHLC Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeOhlcAverageFast(StockData data, ComputeContext context)
    {
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.OhlcAverage(open, high, low, close, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes HLC Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeHlcAverageFast(StockData data, ComputeContext context)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.HlcAverage(high, low, close, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Double Smoothed Momenta using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDoubleSmoothedMomentaFast(StockData data, ComputeContext context,
        int length1 = 2, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length2 = 5,
        int length3 = 25)
    {
        // CalculateDoubleSmoothedMomenta measures where the bar sits in its length1 range and how wide that
        // range is, smooths each of those twice, and reads the first as a percentage of the second.
        var (inputList, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = inputList.Count;

        using var topBuffer = context.Rent(count);
        using var bottomBuffer = context.Rent(count);
        using var smoothedTopBuffer = context.Rent(count);
        using var smoothedBottomBuffer = context.Rent(count);
        var top = topBuffer.WritableSpan;
        var bottom = bottomBuffer.WritableSpan;
        var window = new RollingMinMax(Math.Max(length1, 2));

        for (var i = 0; i < count; i++)
        {
            window.Add(input[i]);
            top[i] = input[i] - window.Min;
            bottom[i] = window.Max - window.Min;
        }

        MovingAverage(data, maType, length2, top, smoothedTopBuffer.WritableSpan);
        MovingAverage(data, maType, length2, bottom, smoothedBottomBuffer.WritableSpan);
        MovingAverage(data, maType, length3, smoothedTopBuffer.Span, top);
        MovingAverage(data, maType, length3, smoothedBottomBuffer.Span, bottom);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            output[i] = bottom[i] != 0 ? MathHelper.MinOrMax(100 * top[i] / bottom[i], 100, 0) : 0;
        }

        return buffer;
    }

    /// <summary>
    /// Computes High-Low Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeHighLowIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.HighLowIndex(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Market Facilitation Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMarketFacilitationIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        // Length parameter is unused - MFI doesn't require a period
        _ = length;
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        OscillatorCore.MarketFacilitationIndex(high, low, volume, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Trend Score using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeTrendScoreFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.TrendScore(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Rolling Median using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMedianValueFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.MedianValue(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Log Returns using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeLogReturnsFast(StockData data, ComputeContext context, int length = 1)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.LogReturns(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Simple Returns using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSimpleReturnsFast(StockData data, ComputeContext context, int length = 1)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.SimpleReturns(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Cumulative Sum using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeCumulativeSumFast(StockData data, ComputeContext context)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.CumulativeSum(inputSpan, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Rolling Maximum using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeRollingMaxFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.RollingMax(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Rolling Minimum using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeRollingMinFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.RollingMin(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Price Position using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePricePositionFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.PricePosition(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes ATR Percent using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAtrPercentFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.AtrPercent(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Chande Momentum Oscillator Absolute Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChandeMomentumOscillatorAbsoluteAverageFast(StockData data,
        ComputeContext context, int length1 = 5, int length2 = 10, int length3 = 20)
    {
        // CalculateChandeMomentumOscillatorAbsoluteAverage is the averaged reading with its sign discarded, so
        // it says how strong the move was without saying which way it went.
        var buffer = context.Rent(data.Count);
        var output = buffer.WritableSpan;
        MomentumOscillatorWindowAverage(data, output, length1, length2, length3);

        for (var i = 0; i < output.Length; i++)
        {
            output[i] = Math.Abs(output[i]);
        }

        return buffer;
    }

    /// <summary>
    /// Writes the mean of the momentum oscillator taken over three windows, the reading the Chande momentum
    /// oscillator averages are both built from.
    /// </summary>
    private static void MomentumOscillatorWindowAverage(StockData data, Span<double> output, int length1,
        int length2, int length3)
    {
        var (inputList, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        var count = inputList.Count;

        var differenceSum = new RollingSum();
        var magnitudeSum = new RollingSum();

        for (var i = 0; i < count; i++)
        {
            // The first bar counts its whole price as an advance, which is what the batch indicator does.
            var previousValue = i >= 1 ? inputList[i - 1] : 0;
            var difference = inputList[i] - previousValue;
            differenceSum.Add(difference);
            magnitudeSum.Add(Math.Abs(difference));

            var first = WindowShare(differenceSum.Sum(length1), magnitudeSum.Sum(length1));
            var second = WindowShare(differenceSum.Sum(length2), magnitudeSum.Sum(length2));
            var third = WindowShare(differenceSum.Sum(length3), magnitudeSum.Sum(length3));
            output[i] = 100 * ((first + second + third) / 3);
        }
    }

    /// <summary>
    /// Gives the share of one window's movement that went in the same direction, between minus one and one.
    /// </summary>
    private static double WindowShare(double difference, double magnitude)
    {
        return magnitude != 0 ? MathHelper.MinOrMax(difference / magnitude, 1, -1) : 0;
    }

    /// <summary>
    /// Computes Chande Momentum Oscillator Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChandeMomentumOscillatorAverageFast(StockData data, ComputeContext context,
        int length1 = 5, int length2 = 10, int length3 = 20)
    {
        // CalculateChandeMomentumOscillatorAverage averages the momentum reading over three windows. Unlike
        // its siblings it does not hold back the first bar, so the opening move counts as a full advance.
        var buffer = context.Rent(data.Count);
        MomentumOscillatorWindowAverage(data, buffer.WritableSpan, length1, length2, length3);

        return buffer;
    }

    /// <summary>
    /// Computes Double Stochastic Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDoubleStochasticOscillatorFast(StockData data, ComputeContext context,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14, int smoothLength = 3)
    {
        // CalculateDoubleStochasticOscillator rescales a raw stochastic against its own range and smooths
        // that once. The second smoothing there is the Signal line, not the series this arm is bound to.
        var (inputList, highList, lowList, _, _) = CalculationsHelper.GetInputValuesList(data);
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var high = SpanCompat.AsReadOnlySpan(highList);
        var low = SpanCompat.AsReadOnlySpan(lowList);
        var count = inputList.Count;

        using var stochasticBuffer = context.Rent(count);
        var stochastic = stochasticBuffer.WritableSpan;
        var highWindow = new RollingMinMax(length);
        var lowWindow = new RollingMinMax(length);

        for (var i = 0; i < count; i++)
        {
            highWindow.Add(high[i]);
            lowWindow.Add(low[i]);

            var range = highWindow.Max - lowWindow.Min;
            stochastic[i] = range != 0
                ? MathHelper.MinOrMax((input[i] - lowWindow.Min) / range * 100, 100, 0)
                : 0;
        }

        using var doubleKBuffer = context.Rent(count);
        var doubleK = doubleKBuffer.WritableSpan;
        var stochasticWindow = new RollingMinMax(Math.Max(length, 2));

        for (var i = 0; i < count; i++)
        {
            stochasticWindow.Add(stochastic[i]);

            var range = stochasticWindow.Max - stochasticWindow.Min;
            doubleK[i] = range != 0
                ? MathHelper.MinOrMax((stochastic[i] - stochasticWindow.Min) / range * 100, 100, 0)
                : 0;
        }

        var buffer = context.Rent(count);
        MovingAverage(data, maType, smoothLength, doubleK, buffer.WritableSpan);

        return buffer;
    }

    /// <summary>
    /// Computes DTOscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDTOscillatorFast(StockData data, ComputeContext context, int length1 = 13,
        MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length2 = 8, int length3 = 5)
    {
        // CalculateDTOscillator rescales a smoothed price against its own length2 range and averages that
        // over length3. The further average it takes is its signal line, which is a different series.
        var (inputList, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        var count = inputList.Count;

        using var smoothedBuffer = context.Rent(count);
        var smoothed = smoothedBuffer.WritableSpan;
        MovingAverage(data, maType, length1, SpanCompat.AsReadOnlySpan(inputList), smoothed);

        var window = new RollingMinMax(Math.Max(length2, 2));
        var stochasticSum = new RollingSum();

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            var value = smoothed[i];
            window.Add(value);

            var range = window.Max - window.Min;
            var stochastic = range != 0
                ? MathHelper.MinOrMax(100 * (value - window.Min) / range, 100, 0)
                : 0;

            stochasticSum.Add(stochastic);
            output[i] = stochasticSum.Average(length3);
        }

        return buffer;
    }

    /// <summary>
    /// Computes Compare Price Momentum Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeComparePriceMomentumOscillatorFast(StockData data, ComputeContext context, int length = 35)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.ComparePriceMomentumOscillator(inputSpan, buffer.WritableSpan, length, 10, 10);
        return buffer;
    }

    /// <summary>
    /// Computes Daily Average Price Delta using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDailyAveragePriceDeltaFast(StockData data, ComputeContext context,
        int length = 21, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // CalculateDailyAveragePriceDelta widens each bar by how far the average high sits above the average
        // low, and this spec is bound to the upper band: the high widened upwards. The close never enters it.
        var (_, highList, lowList, _, _) = CalculationsHelper.GetInputValuesList(data);
        var count = highList.Count;
        var high = SpanCompat.AsReadOnlySpan(highList);

        using var averageHighBuffer = context.Rent(count);
        using var averageLowBuffer = context.Rent(count);
        var averageHigh = averageHighBuffer.WritableSpan;
        var averageLow = averageLowBuffer.WritableSpan;
        MovingAverage(data, maType, length, high, averageHigh);
        MovingAverage(data, maType, length, SpanCompat.AsReadOnlySpan(lowList), averageLow);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            output[i] = high[i] + (averageHigh[i] - averageLow[i]);
        }

        return buffer;
    }

    /// <summary>
    /// Computes Demand Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDemandOscillatorFast(StockData data, ComputeContext context,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 10, int length2 = 2,
        int length3 = 20)
    {
        // CalculateDemandOscillator splits each bar's volume into the part that bought and the part that
        // sold, using how far the price moved against the average range, and then smooths the difference.
        var (inputList, highList, lowList, _, _) = CalculationsHelper.GetInputValuesList(data);
        var count = inputList.Count;
        var volumes = SpanCompat.AsReadOnlySpan(data.Volumes);

        using var rangeBuffer = context.Rent(count);
        var range = rangeBuffer.WritableSpan;
        var highWindow = new RollingMinMax(length2);
        var lowWindow = new RollingMinMax(length2);

        for (var i = 0; i < count; i++)
        {
            highWindow.Add(highList[i]);
            lowWindow.Add(lowList[i]);
            range[i] = highWindow.Max - lowWindow.Min;
        }

        using var averageRangeBuffer = context.Rent(count);
        var averageRange = averageRangeBuffer.WritableSpan;
        MovingAverage(data, maType, length1, range, averageRange);

        using var oscillatorBuffer = context.Rent(count);
        var oscillator = oscillatorBuffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            var currentValue = inputList[i];
            var previousValue = i >= 1 ? inputList[i - 1] : 0;
            var percentChange = previousValue != 0
                ? CalculationsHelper.MinPastValues(i, 1, currentValue - previousValue) / Math.Abs(previousValue) * 100
                : 0;

            var volume = volumes[i];
            var k = averageRange[i] != 0 ? 3 * currentValue / averageRange[i] : 0;
            var percentK = percentChange * k;
            var volumePerPercentK = percentK != 0 ? volume / percentK : 0;

            var buyingPower = currentValue > previousValue ? volume : volumePerPercentK;
            var sellingPower = currentValue > previousValue ? volumePerPercentK : volume;
            oscillator[i] = buyingPower - sellingPower;
        }

        var buffer = context.Rent(count);
        MovingAverage(data, maType, length3, oscillator, buffer.WritableSpan);

        return buffer;
    }

    /// <summary>
    /// Computes Double Smoothed Relative Strength Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDoubleSmoothedRelativeStrengthIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.DoubleSmoothedRelativeStrengthIndex(inputSpan, buffer.WritableSpan, length, 5);
        return buffer;
    }

    /// <summary>
    /// Computes Dynamic Momentum Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDynamicMomentumOscillatorFast(StockData data, ComputeContext context,
        int length1 = 10, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length2 = 20)
    {
        // CalculateDynamicMomentumOscillator smooths a length1 stochastic by length1 and again by length2,
        // then swings the midpoint of the running range of the faster line by the gap between the two.
        var (inputList, highList, lowList, _, _) = CalculationsHelper.GetInputValuesList(data);
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var high = SpanCompat.AsReadOnlySpan(highList);
        var low = SpanCompat.AsReadOnlySpan(lowList);
        var count = inputList.Count;

        using var fastBuffer = context.Rent(count);
        var fast = fastBuffer.WritableSpan;
        var highWindow = new RollingMinMax(length1);
        var lowWindow = new RollingMinMax(length1);

        for (var i = 0; i < count; i++)
        {
            highWindow.Add(high[i]);
            lowWindow.Add(low[i]);

            var range = highWindow.Max - lowWindow.Min;
            fast[i] = range != 0 ? MathHelper.MinOrMax((input[i] - lowWindow.Min) / range * 100, 100, 0) : 0;
        }

        using var smoothedBuffer = context.Rent(count);
        using var signalBuffer = context.Rent(count);
        MovingAverage(data, maType, length1, fast, smoothedBuffer.WritableSpan);
        MovingAverage(data, maType, length2, smoothedBuffer.Span, signalBuffer.WritableSpan);

        var smoothed = smoothedBuffer.Span;
        var signal = signalBuffer.Span;
        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        double highest = 0;
        var lowest = double.MaxValue;

        for (var i = 0; i < count; i++)
        {
            var value = smoothed[i];
            highest = value > highest ? value : highest;
            lowest = value < lowest ? value : lowest;

            var midpoint = MathHelper.MinOrMax((lowest + highest) / 2, 100, 0);
            output[i] = MathHelper.MinOrMax(midpoint - (signal[i] - value), 100, 0);
        }

        return buffer;
    }

    /// <summary>
    /// Computes Average Money Flow Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAverageMoneyFlowOscillatorFast(StockData data, ComputeContext context,
        int length = 5, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int smoothLength = 3)
    {
        // CalculateAverageMoneyFlowOscillator scales the log of the averaged volume times the averaged price
        // change into its own recent range, and every one of its three averages takes the given type.
        var (inputList, _, _, _, volumeList) = CalculationsHelper.GetInputValuesList(data);
        var count = inputList.Count;
        var input = SpanCompat.AsReadOnlySpan(inputList);

        using var averageVolumeBuffer = context.Rent(count);
        var averageVolume = averageVolumeBuffer.WritableSpan;
        MovingAverage(data, maType, length, SpanCompat.AsReadOnlySpan(volumeList), averageVolume);

        using var changeBuffer = context.Rent(count);
        var change = changeBuffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            // The first bar has nothing to change from.
            change[i] = i >= 1 ? input[i] - input[i - 1] : 0;
        }

        using var averageChangeBuffer = context.Rent(count);
        var averageChange = averageChangeBuffer.WritableSpan;
        MovingAverage(data, maType, length, change, averageChange);

        using var scaledBuffer = context.Rent(count);
        var scaled = scaledBuffer.WritableSpan;
        var flowWindow = new RollingMinMax(length);

        for (var i = 0; i < count; i++)
        {
            var magnitude = Math.Abs(averageVolume[i] * averageChange[i]);
            var flow = magnitude > 0 ? Math.Log(magnitude) * Math.Sign(averageChange[i]) : 0;
            flowWindow.Add(flow);

            var highest = flowWindow.Max;
            var lowest = flowWindow.Min;
            var position = highest != lowest ? (flow - lowest) / (highest - lowest) * 100 : 0;
            scaled[i] = (position * 2) - 100;
        }

        var buffer = context.Rent(count);
        MovingAverage(data, maType, smoothLength, scaled, buffer.WritableSpan);

        return buffer;
    }

    /// <summary>
    /// Computes DMI Stochastic using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDMIStochasticFast(StockData data, ComputeContext context,
        int length1 = 10, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length2 = 10,
        int length3 = 3, int length4 = 3)
    {
        // CalculateDMIStochastic rescales the gap between the two directional indicators against its own
        // length2 range and then smooths that twice, with whichever average it was given.
        var (plusBuffer, minusBuffer) = DirectionalIndicators(data, context, length1, maType);
        using var diPlusBuffer = plusBuffer;
        using var diMinusBuffer = minusBuffer;
        var diPlus = diPlusBuffer.Span;
        var diMinus = diMinusBuffer.Span;
        var count = diPlus.Length;

        using var fastKBuffer = context.Rent(count);
        var fastK = fastKBuffer.WritableSpan;
        var window = new RollingMinMax(Math.Max(length2, 2));

        for (var i = 0; i < count; i++)
        {
            var oscillator = diMinus[i] - diPlus[i];
            window.Add(oscillator);

            var range = window.Max - window.Min;
            fastK[i] = range != 0 ? MathHelper.MinOrMax((oscillator - window.Min) / range * 100, 100, 0) : 0;
        }

        using var slowKBuffer = context.Rent(count);
        MovingAverage(data, maType, length3, fastK, slowKBuffer.WritableSpan);

        var buffer = context.Rent(count);
        MovingAverage(data, maType, length4, slowKBuffer.Span, buffer.WritableSpan);

        return buffer;
    }

    /// <summary>
    /// Computes CCT Stoch RSI using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeCCTStochRelativeStrengthIndexFast(StockData data, ComputeContext context,
        int length2 = 8, int length3 = 13, int length5 = 21,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        // CalculateCCTStochRSI stochasticises five relative strength indexes of different lengths and
        // publishes seven readings. The series this arm is bound to is the first of them: the longest index
        // placed between its own low over length2 and its own range over length3. The other lengths and both
        // smoothing lengths only reach the readings the arm does not serve.
        using var rsi = ComputeRsiFast(data, context, length5, maType);
        var strength = rsi.Span;
        var count = strength.Length;

        var lowWindow = new RollingMinMax(length2);
        var rangeWindow = new RollingMinMax(length3);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            var currentRsi = strength[i];
            lowWindow.Add(currentRsi);
            rangeWindow.Add(currentRsi);

            var lowest = lowWindow.Min;
            var range = rangeWindow.Max - rangeWindow.Min;
            output[i] = range != 0 ? (currentRsi - lowest) / range * 100 : 0;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Bilateral Stochastic Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeBilateralStochasticOscillatorFast(StockData data, ComputeContext context,
        int length = 100, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // CalculateBilateralStochasticOscillator stochasticises an average of the price against its own range
        // in both directions and publishes the stronger of the two, and both of its averages take the given
        // type. The high and low here are of that average, not of the bar.
        var (inputList, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        var count = inputList.Count;
        var input = SpanCompat.AsReadOnlySpan(inputList);

        using var averageBuffer = context.Rent(count);
        var average = averageBuffer.WritableSpan;
        MovingAverage(data, maType, length, input, average);

        using var highestBuffer = context.Rent(count);
        using var lowestBuffer = context.Rent(count);
        using var rangeBuffer = context.Rent(count);
        var highest = highestBuffer.WritableSpan;
        var lowest = lowestBuffer.WritableSpan;
        var range = rangeBuffer.WritableSpan;
        var window = new RollingMinMax(Math.Max(length, 2));

        for (var i = 0; i < count; i++)
        {
            window.Add(average[i]);
            highest[i] = window.Max;
            lowest[i] = window.Min;
            range[i] = highest[i] - lowest[i];
        }

        using var rangeAverageBuffer = context.Rent(count);
        var rangeAverage = rangeAverageBuffer.WritableSpan;
        MovingAverage(data, maType, length, range, rangeAverage);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            var scale = rangeAverage[i];
            var bull = scale != 0 ? (average[i] / scale) - (lowest[i] / scale) : 0;
            var bear = scale != 0 ? Math.Abs((average[i] / scale) - (highest[i] / scale)) : 0;
            output[i] = Math.Max(bull, bear);
        }

        return buffer;
    }

    #region Batch 5 - Additional Oscillators

    /// <summary>
    /// Computes Chande Momentum Oscillator Average Disparity Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChandeMomentumOscillatorAverageDisparityIndexFast(StockData data,
        ComputeContext context, int length1 = 200, int length2 = 50, int length3 = 20,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        // CalculateChandeMomentumOscillatorAverageDisparityIndex averages how far the price sits above three
        // averages of it, each as a percentage of the price itself.
        var (inputList, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        var count = inputList.Count;
        var input = SpanCompat.AsReadOnlySpan(inputList);

        using var first = context.Rent(count);
        using var second = context.Rent(count);
        using var third = context.Rent(count);
        MovingAverage(data, maType, length1, input, first.WritableSpan);
        MovingAverage(data, maType, length2, input, second.WritableSpan);
        MovingAverage(data, maType, length3, input, third.WritableSpan);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            var currentValue = input[i];
            var firstDisparity = currentValue != 0 ? (currentValue - first.Span[i]) / currentValue * 100 : 0;
            var secondDisparity = currentValue != 0 ? (currentValue - second.Span[i]) / currentValue * 100 : 0;
            var thirdDisparity = currentValue != 0 ? (currentValue - third.Span[i]) / currentValue * 100 : 0;
            output[i] = (firstDisparity + secondDisparity + thirdDisparity) / 3;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Chande Momentum Oscillator Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChandeMomentumOscillatorFilterFast(StockData data, ComputeContext context,
        int length = 9, double filter = 3)
    {
        // CalculateChandeMomentumOscillatorFilter is the momentum oscillator with every move larger than the
        // filter thrown away rather than clipped, so a big bar counts for nothing at all. Only its signal line
        // is smoothed, so no average reaches the series this arm is bound to.
        var (inputList, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        var count = inputList.Count;

        var differenceSum = new RollingSum();
        var magnitudeSum = new RollingSum();

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            // There is nothing to move from on the first bar.
            var previousValue = i >= 1 ? inputList[i - 1] : 0;
            var difference = CalculationsHelper.MinPastValues(i, 1, inputList[i] - previousValue);
            var magnitude = Math.Abs(difference);

            if (magnitude > filter)
            {
                difference = 0;
                magnitude = 0;
            }

            differenceSum.Add(difference);
            magnitudeSum.Add(magnitude);

            var total = magnitudeSum.Sum(length);
            output[i] = total != 0 ? MathHelper.MinOrMax(100 * differenceSum.Sum(length) / total, 100, -100) : 0;
        }

        return buffer;
    }

    /// <summary>
    /// Computes DiNapoli Percentage Price Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDiNapoliPercentagePriceOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        // Length parameter is unused - DiNapoli uses fixed periods (3, 7)
        _ = length;
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DiNapoliPercentagePriceOscillator(close, buffer.WritableSpan, 3, 7);
        return buffer;
    }

    /// <summary>
    /// Computes DiNapoli Preferred Stochastic Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDiNapoliPreferredStochasticOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DiNapoliPreferredStochasticOscillator(high, low, close, buffer.WritableSpan, length, 3, 3);
        return buffer;
    }

    /// <summary>
    /// Computes Ergodic Percentage Price Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeErgodicPercentagePriceOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        // Length parameter maps to short length; others use defaults
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ErgodicPercentagePriceOscillator(close, buffer.WritableSpan, length, 20, 5);
        return buffer;
    }

    /// <summary>
    /// Computes Fast and Slow Kurtosis Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeFastSlowKurtosisOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        // Length parameter is unused - uses fixed fast/slow periods
        _ = length;
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.FastSlowKurtosisOscillator(close, buffer.WritableSpan, 5, 20);
        return buffer;
    }

    /// <summary>
    /// Computes Fast and Slow RSI Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeFastSlowRsiOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.FastSlowRsiOscillator(close, buffer.WritableSpan, length / 2, length);
        return buffer;
    }

    #endregion

    #region Batch 6 - More Oscillators

    /// <summary>
    /// Computes Fast and Slow Stochastic Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeFastSlowStochasticOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.FastSlowStochasticOscillator(high, low, close, buffer.WritableSpan, length / 2, length);
        return buffer;
    }

    /// <summary>
    /// Computes G-Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeGOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.GOscillator(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Gann Swing Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeGannSwingOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        // CalculateGannSwingOscillator reads its swings from the rolling extremes of the last length bars,
        // so the length is the indicator, not decoration on it.
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.GannSwingOscillator(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Gann Trend Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeGannTrendOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.GannTrendOscillator(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Firefly Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeFireflyOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.FireflyOscillator(high, low, close, buffer.WritableSpan, length, 3);
        return buffer;
    }

    /// <summary>
    /// Computes Fisher Transform Stochastic Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeFisherTransformStochasticOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.FisherTransformStochasticOscillator(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Karobein Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeKarobeinOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.KarobeinOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Grover Llorens Cycle Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeGroverLlorensCycleOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.GroverLlorensCycleOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Batch 7 - More Oscillators

    /// <summary>
    /// Computes Impulse Percentage Price Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeImpulsePercentagePriceOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ImpulsePercentagePriceOscillator(close, buffer.WritableSpan, 12, 26, length);
        return buffer;
    }

    /// <summary>
    /// Computes Linda Raschke 3/10 Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeLindaRaschke310OscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        // Length parameter maps to signal length; fast/slow use 3/10
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.LindaRaschke310Oscillator(close, buffer.WritableSpan, 3, 10, length);
        return buffer;
    }

    /// <summary>
    /// Computes Midpoint Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMidpointOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.MidpointOscillator(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Mirrored Percentage Price Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMirroredPercentagePriceOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        // Length parameter maps to long length
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.MirroredPercentagePriceOscillator(close, buffer.WritableSpan, length / 2, length);
        return buffer;
    }

    /// <summary>
    /// Computes Mobility Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMobilityOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.MobilityOscillator(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Percent Change Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePercentChangeOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.PercentChangeOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Price Cycle Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePriceCycleOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.PriceCycleOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Price Volume Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePriceVolumeOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        OscillatorCore.PriceVolumeOscillator(close, volume, buffer.WritableSpan, length / 2, length);
        return buffer;
    }

    #endregion

    #region Batch 8 - More Oscillators

    /// <summary>
    /// Computes Projection Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeProjectionOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ProjectionOscillator(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Rainbow Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeRainbowOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.RainbowOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Regression Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeRegressionOscillatorFast(StockData data, ComputeContext context, int length = 63)
    {
        // CalculateRegressionOscillator is how far the chained series stands above or below the linear
        // regression fitted at the same bar, as a percentage of that fit. OscillatorCore.RegressionOscillator
        // read the close and fitted a different window, so it agreed at no bar.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = inputList.Count;

        var buffer = context.Rent(count);
        var rosc = buffer.WritableSpan;

        using var regression = new RollingLeastSquares(length);
        for (var i = 0; i < count; i++)
        {
            var linReg = regression.Next(input[i], isFinal: true).Last;
            rosc[i] = linReg != 0 ? 100 * ((input[i] / linReg) - 1) : 0;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Rex Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeRexOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.RexOscillator(open, high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Sentiment Zone Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSentimentZoneOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        OscillatorCore.SentimentZoneOscillator(close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Wave Trend Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeWaveTrendOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.WaveTrendOscillator(high, low, close, buffer.WritableSpan, length, 21);
        return buffer;
    }

    /// <summary>
    /// Computes WAMI Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeWamiOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.WamiOscillator(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Volume Accumulation Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVolumeAccumulationOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        OscillatorCore.VolumeAccumulationOscillator(high, low, close, volume, buffer.WritableSpan, length / 2, length);
        return buffer;
    }

    #endregion

    #region Additional Oscillators (Batch 9)

    /// <summary>
    /// Computes Kase Peak Oscillator V1 using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeKasePeakOscillatorV1Fast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.KasePeakOscillatorV1(high, low, close, buffer.WritableSpan, length > 1 ? length : 30);
        return buffer;
    }

    /// <summary>
    /// Computes Varadi Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVaradiOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.VaradiOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Prime Number Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePrimeNumberOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.PrimeNumberOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Trigonometric Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeTrigonometricOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.TrigonometricOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ultimate Trader Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeUltimateTraderOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.UltimateTraderOscillator(high, low, close, buffer.WritableSpan, length / 2, length, length * 2);
        return buffer;
    }

    /// <summary>
    /// Computes Smoothed Delta Ratio Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSmoothedDeltaRatioOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.SmoothedDeltaRatioOscillator(close, buffer.WritableSpan, length, 3);
        return buffer;
    }

    /// <summary>
    /// Computes Fast Slow Degree Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeFastSlowDegreeOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.FastSlowDegreeOscillator(close, buffer.WritableSpan, length / 2, length);
        return buffer;
    }

    /// <summary>
    /// Computes Robust Weighting Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeRobustWeightingOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.RobustWeightingOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Additional Oscillators (Batch 10)

    /// <summary>
    /// Computes Kase Peak Oscillator V2 using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeKasePeakOscillatorV2Fast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.KasePeakOscillatorV2(high, low, close, buffer.WritableSpan, length > 1 ? length : 30);
        return buffer;
    }

    /// <summary>
    /// Computes Stochastic Custom Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeStochasticCustomOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.StochasticCustomOscillator(high, low, close, buffer.WritableSpan, length, 3, 3);
        return buffer;
    }

    /// <summary>
    /// Computes Pivot Detector Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePivotDetectorOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.PivotDetectorOscillator(high, low, close, buffer.WritableSpan, length > 2 ? length / 2 : 5);
        return buffer;
    }

    /// <summary>
    /// Computes Tick Line Momentum Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeTickLineMomentumOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.TickLineMomentumOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Support and Resistance Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSupportAndResistanceOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length; // The indicator reads one bar plus the previous close, so there is no lookback to set.
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.SupportAndResistanceOscillator(open, high, low, close, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Trading Made More Simpler Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeTradingMadeMoreSimplerOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.TradingMadeMoreSimplerOscillator(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Nth Order Differencing Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeNthOrderDifferencingOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.NthOrderDifferencingOscillator(close, buffer.WritableSpan, length, 2);
        return buffer;
    }

    /// <summary>
    /// Computes Osc Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeOscOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.OscOscillator(close, buffer.WritableSpan, length / 2, length);
        return buffer;
    }

    #endregion

    #region Additional Oscillators (Batch 11) - Ehlers Oscillators

    /// <summary>
    /// Computes Ehlers Center of Gravity Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersCenterOfGravityOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.EhlersCenterOfGravityOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Decycler Oscillator V1 using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersDecyclerOscillatorV1Fast(StockData data, ComputeContext context, int fastLength = 100,
        double fastMult = 1.2)
    {
        // The bound key is FastEdo, so only the fast oscillator is published; the slow length and its
        // multiplier reach the SlowEdo line alone. The decycler is the input less its own high pass,
        // and the oscillator is the high pass of that decycler scaled by price.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = data.Count;
        fastLength = Math.Max(fastLength, 1);

        using var highPass = EhlersHighPassFilterV1(context, input, fastLength, 1);
        var hp = highPass.Span;

        using var decycler = context.Rent(count);
        var dec = decycler.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            dec[i] = input[i] - hp[i];
        }

        using var filtered = EhlersHighPassFilterV1(context, decycler.Span, fastLength, 0.5);
        var filt = filtered.Span;

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            output[i] = input[i] != 0 ? 100 * fastMult * filt[i] / input[i] : 0;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Hilbert Oscillator using zero-allocation fast path.
    /// </summary>
    /// <summary>
    /// Which of the Hilbert oscillator's two series an arm has been asked for. Both are sums of the same
    /// quadrature component over a window taken from the measured cycle, so one walk produces both.
    /// </summary>
    internal enum EhlersHilbertOutput
    {
        Quadrature,
        InPhase
    }

    internal static ComputeBuffer ComputeEhlersHilbertOscillatorFast(StockData data, ComputeContext context, int length = 7,
        EhlersHilbertOutput output = EhlersHilbertOutput.Quadrature)
    {
        // CalculateEhlersHilbertOscillator measures the dominant cycle with the mother of adaptive moving
        // averages, takes the quadrature component of its smoothed series, and sums that component back over
        // half a cycle for I3 and a quarter of one for IQ. The length reaches only the signals, which is why
        // neither series moves with it. OscillatorCore computed something else entirely from the close.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = inputList.Count;
        _ = length;

        using var smoothed = context.Rent(count);
        using var periods = context.Rent(count);
        using var quadratures = context.Rent(count);
        var smooth = smoothed.WritableSpan;
        var smoothPeriod = periods.WritableSpan;
        var q3 = quadratures.WritableSpan;

        using (var engine = new Streaming.EhlersMotherOfAdaptiveMovingAveragesEngine(0.5, 0.05))
        {
            for (var i = 0; i < count; i++)
            {
                var snapshot = engine.Next(input[i], isFinal: true);
                smooth[i] = snapshot.Smooth;
                smoothPeriod[i] = snapshot.SmoothPeriod;
            }
        }

        for (var i = 0; i < count; i++)
        {
            var previousSmooth = i >= 2 ? smooth[i - 2] : 0;
            q3[i] = 0.5 * (smooth[i] - previousSmooth) * ((0.1759 * smoothPeriod[i]) + 0.4607);
        }

        var buffer = context.Rent(count);
        var values = buffer.WritableSpan;
        var divisor = output == EhlersHilbertOutput.InPhase ? 2 : 4;
        var scale = output == EhlersHilbertOutput.InPhase ? 1.57 : 1.25;
        for (var i = 0; i < count; i++)
        {
            var window = (int)Math.Ceiling(smoothPeriod[i] / divisor);
            double sum = 0;
            for (var j = 0; j <= window - 1; j++)
            {
                sum += i >= j ? q3[i - j] : 0;
            }

            values[i] = window != 0 ? scale * sum / window : sum;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Universal Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersUniversalOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.EhlersUniversalOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Recursive Median Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersRecursiveMedianOscillatorFast(StockData data, ComputeContext context, int length1 = 5, int length2 = 12,
        int length3 = 30)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = data.Count;
        length1 = Math.Max(length1, 1);
        length2 = Math.Max(length2, 1);
        length3 = Math.Max(length3, 1);

        var alpha1Arg = MathHelper.MinOrMax(2 * Math.PI / length2, 0.99, 0.01);
        var alpha1ArgCos = Math.Cos(alpha1Arg);
        var alpha2Arg = MathHelper.MinOrMax(1 / MathHelper.Sqrt(2) * 2 * Math.PI / length3, 0.99, 0.01);
        var alpha2ArgCos = Math.Cos(alpha2Arg);
        var alpha1 = alpha1ArgCos != 0 ? (alpha1ArgCos + Math.Sin(alpha1Arg) - 1) / alpha1ArgCos : 0;
        var alpha2 = alpha2ArgCos != 0 ? (alpha2ArgCos + Math.Sin(alpha2Arg) - 1) / alpha2ArgCos : 0;

        using var median = new RollingMedian(length1);
        using var recursiveMedian = context.Rent(count);
        var rm = recursiveMedian.WritableSpan;

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            median.Add(input[i]);

            var previousRm1 = i >= 1 ? rm[i - 1] : 0;
            var previousRm2 = i >= 2 ? rm[i - 2] : 0;
            var previousRmo1 = i >= 1 ? output[i - 1] : 0;
            var previousRmo2 = i >= 2 ? output[i - 2] : 0;

            rm[i] = (alpha1 * median.Median) + ((1 - alpha1) * previousRm1);
            output[i] = (MathHelper.Pow(1 - (alpha2 / 2), 2) * (rm[i] - (2 * previousRm1) + previousRm2)) +
                (2 * (1 - alpha2) * previousRmo1) - (MathHelper.Pow(1 - alpha2, 2) * previousRmo2);
        }

        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Stochastic Center of Gravity Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersStochasticCenterOfGravityOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.EhlersStochasticCenterOfGravityOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Fisherized Deviation Scaled Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersFisherizedDeviationScaledOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.EhlersFisherizedDeviationScaledOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Adaptive Center of Gravity Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersAdaptiveCenterOfGravityOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.EhlersAdaptiveCenterOfGravityOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Additional Oscillators (Batch 12) - Vervoort and Specialized

    /// <summary>
    /// Computes Vervoort Smoothed Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVervoortSmoothedOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.VervoortSmoothedOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Relative Difference of Squares Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeRelativeDifferenceOfSquaresOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.RelativeDifferenceOfSquaresOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Linear Quadratic Convergence Divergence Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeLinearQuadraticConvergenceDivergenceOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.LinearQuadraticConvergenceDivergenceOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Stationary Extrapolated Levels Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeStationaryExtrapolatedLevelsOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.StationaryExtrapolatedLevelsOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Percentage Price Oscillator Leader using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePercentagePriceOscillatorLeaderFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.PercentagePriceOscillatorLeader(close, buffer.WritableSpan, 12, 26);
        return buffer;
    }

    /// <summary>
    /// Computes Kaufman Adaptive Correlation Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeKaufmanAdaptiveCorrelationOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.KaufmanAdaptiveCorrelationOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Stochastic MACD Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeStochasticMacdOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.StochasticMacdOscillator(close, buffer.WritableSpan, 12, 26, 9, length);
        return buffer;
    }

    /// <summary>
    /// Computes McClellan Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMcClellanOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.McClellanOscillator(close, buffer.WritableSpan, 19, 39);
        return buffer;
    }

    #endregion

    #region Batch 13 - Additional Ehlers and Specialized Oscillators

    /// <summary>
    /// Computes Ehlers Decycler Oscillator V2 using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersDecyclerOscillatorV2Fast(StockData data, ComputeContext context,
        int fastLength = 10, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int slowLength = 20)
    {
        // The slow high-pass filter less the fast one. Both read the caller's own series: the second is not
        // chained onto the first, which is the distinction CalculateEhlersDecyclerOscillatorV2 restores its
        // input series to make.
        using var fastBuffer = EhlersHighPassFilterV2(data, context, fastLength, maType);
        using var slowBuffer = EhlersHighPassFilterV2(data, context, slowLength, maType);
        var fast = fastBuffer.Span;
        var slow = slowBuffer.Span;
        var count = fast.Length;

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            output[i] = slow[i] - fast[i];
        }

        return buffer;
    }

    /// <summary>
    /// Computes Vervoort Heiken Ashi Candlestick Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVervoortHeikenAshiCandlestickOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.VervoortHeikenAshiCandlestickOscillator(high, low, close, open, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Vervoort Heiken Ashi Long Term Candlestick Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVervoortHeikenAshiLongTermCandlestickOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.VervoortHeikenAshiLongTermCandlestickOscillator(high, low, close, open, buffer.WritableSpan, length > 0 ? length * 4 : 55);
        return buffer;
    }

    /// <summary>
    /// Computes Decision Point Breadth Swenlin Trading Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDecisionPointBreadthSwenlinTradingOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DecisionPointBreadthSwenlinTradingOscillator(close, buffer.WritableSpan, Math.Max(1, length / 3), length * 7);
        return buffer;
    }

    /// <summary>
    /// Computes Decision Point Price Momentum Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDecisionPointPriceMomentumOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DecisionPointPriceMomentumOscillator(close, buffer.WritableSpan, length > 0 ? length * 2 + 7 : 35, length > 0 ? length + 6 : 20);
        return buffer;
    }

    /// <summary>
    /// Computes TFS MBO Percentage Price Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeTFSMboPercentagePriceOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.TFSMboPercentagePriceOscillator(close, buffer.WritableSpan, length > 0 ? length + 11 : 25, length > 0 ? length * 14 : 200);
        return buffer;
    }

    /// <summary>
    /// Computes TFS Volume Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeTFSVolumeOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        OscillatorCore.TFSVolumeOscillator(volume, buffer.WritableSpan, length > 0 ? length - 1 : 13, length > 0 ? length * 4 : 55);
        return buffer;
    }

    /// <summary>
    /// Computes Mass Thrust Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMassThrustOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.MassThrustOscillator(close, buffer.WritableSpan, length > 0 ? length - 4 : 10);
        return buffer;
    }

    #endregion

    #region Batch 14 - Additional Moving Averages

    /// <summary>
    /// Computes Ultimate Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeUltimateMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.UltimateMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Symmetrically Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSymmetricallyWeightedMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.SymmetricallyWeightedMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Square Root Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSquareRootWeightedMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.SquareRootWeightedMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Spencer 15-Point Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSpencer15PointMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.Spencer15PointMovingAverage(close, buffer.WritableSpan, 15);
        return buffer;
    }

    /// <summary>
    /// Computes Spencer 21-Point Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSpencer21PointMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.Spencer21PointMovingAverage(close, buffer.WritableSpan, 21);
        return buffer;
    }

    /// <summary>
    /// Computes Slow Smoothed Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSlowSmoothedMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.SlowSmoothedMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Repulsion Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeRepulsionMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.RepulsionMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Quick Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeQuickMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.QuickMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Batch 15 - Ehlers and Specialized Moving Averages

    /// <summary>
    /// Computes Ehlers Better Exponential Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersBetterExponentialMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersBetterExponentialMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Deviation Scaled Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersDeviationScaledMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersDeviationScaledMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Hann Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersHannMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersHannMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Triangle Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersTriangleMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersTriangleMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Elastic Volume Weighted Moving Average V1 using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeElasticVolumeWeightedMovingAverageV1Fast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.ElasticVolumeWeightedMovingAverageV1(close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Holt Exponential Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeHoltExponentialMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.HoltExponentialMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Pentuple Exponential Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePentupleExponentialMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.PentupleExponentialMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Quadruple Exponential Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeQuadrupleExponentialMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.QuadrupleExponentialMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Batch 16 - Trend Indicators (Ichimoku, Fractals, Alligator)

    /// <summary>
    /// Computes Ichimoku Senkou Span A using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeIchimokuSenkouSpanAFast(StockData data, ComputeContext context, int length = 26)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        TrendCore.IchimokuSenkouSpanA(high, low, buffer.WritableSpan, 9, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ichimoku Senkou Span B using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeIchimokuSenkouSpanBFast(StockData data, ComputeContext context, int length = 52)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        TrendCore.IchimokuSenkouSpanB(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ichimoku Chikou Span using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeIchimokuChikouSpanFast(StockData data, ComputeContext context, int length = 26)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.IchimokuChikouSpan(close, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Williams Fractal Up using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeWilliamsFractalUpFast(StockData data, ComputeContext context, int length = 2)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var buffer = context.Rent(data.Count);
        TrendCore.WilliamsFractalUp(high, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Williams Fractal Down using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeWilliamsFractalDownFast(StockData data, ComputeContext context, int length = 2)
    {
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        TrendCore.WilliamsFractalDown(low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Alligator Jaw using zero-allocation fast path.
    /// </summary>
    /// <summary>
    /// Computes one line of the alligator index: a smoothed median price displaced forward by the
    /// line's own offset.
    /// </summary>
    private static ComputeBuffer AlligatorLine(StockData data, ComputeContext context, int length, int offset, MovingAvgType maType)
    {
        var (inputList, _, _, _, _, _) = CalculationsHelper.GetInputValuesList(InputName.MedianPrice, data);
        var count = inputList.Count;

        using var smoothed = context.Rent(count);
        MovingAverage(data, maType, Math.Max(length, 1), SpanCompat.AsReadOnlySpan(inputList), smoothed.WritableSpan);
        var line = smoothed.Span;

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            output[i] = i >= offset ? line[i - offset] : 0;
        }

        return buffer;
    }

    internal static ComputeBuffer ComputeAlligatorJawFast(StockData data, ComputeContext context, int length = 13, int offset = 8,
        MovingAvgType maType = MovingAvgType.WildersSmoothingMethod)
    {
        // The alligator lines average the median price, not the close, and each is displaced forward by
        // its own offset before it is published.
        return AlligatorLine(data, context, length, offset, maType);
    }

    /// <summary>
    /// Computes Alligator Teeth using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAlligatorTeethFast(StockData data, ComputeContext context, int length = 8, int offset = 5,
        MovingAvgType maType = MovingAvgType.WildersSmoothingMethod)
    {
        return AlligatorLine(data, context, length, offset, maType);
    }

    /// <summary>
    /// Computes Alligator Lips using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAlligatorLipsFast(StockData data, ComputeContext context, int length = 5, int offset = 3,
        MovingAvgType maType = MovingAvgType.WildersSmoothingMethod)
    {
        return AlligatorLine(data, context, length, offset, maType);
    }

    #endregion

    #region Batch 17 - Ehlers Laguerre and Related Filters

    /// <summary>
    /// Computes Ehlers Laguerre Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersLaguerreFilterFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        var alpha = 2.0 / (length + 1); // Convert length to alpha
        MovingAverageCore.EhlersLaguerreFilter(close, buffer.WritableSpan, alpha);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Laguerre Relative Strength Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersLaguerreRsiFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        var gamma = 1.0 - (2.0 / (length + 1)); // Convert length to gamma
        MovingAverageCore.EhlersLaguerreRelativeStrengthIndex(close, buffer.WritableSpan, gamma);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Zero Lag EMA using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersZeroLagEmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersZeroLagExponentialMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Fractal Adaptive Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersFramaFast(StockData data, ComputeContext context, int length = 16)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersFractalAdaptiveMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Inverse Fisher Transform using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersInverseFisherTransformFast(StockData data, ComputeContext context, int length1 = 5,
        int length2 = 9, MovingAvgType maType = MovingAvgType.WeightedMovingAverage)
    {
        // The batch transforms a smoothed, rescaled relative strength index, not the raw price series.
        var count = data.Count;

        using var rsi = ComputeRsiFast(data, context, Math.Max(length1, 1), maType);
        var rsiValues = rsi.Span;

        using var rescaled = context.Rent(count);
        var v1 = rescaled.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            v1[i] = 0.1 * (rsiValues[i] - 50);
        }

        using var smoothed = context.Rent(count);
        MovingAverage(data, maType, Math.Max(length2, 1), rescaled.Span, smoothed.WritableSpan);
        var v2 = smoothed.Span;

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            var top = MathHelper.Exp(2 * v2[i]) - 1;
            var bottom = MathHelper.Exp(2 * v2[i]) + 1;
            output[i] = bottom != 0 ? MathHelper.MinOrMax(top / bottom, 1, -1) : 0;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Cyber Cycle using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersCyberCycleFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        var alpha = 2.0 / (length + 1); // Convert length to alpha
        MovingAverageCore.EhlersCyberCycle(close, buffer.WritableSpan, alpha);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Stochastic using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersStochasticFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersStochastic(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Adaptive Laguerre Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersAdaptiveLaguerreFilterFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersAdaptiveLaguerreFilter(close, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Batch 18 - Additional Moving Averages and Filters
    // Note: CubedWeightedMovingAverage uses ComputeCubicWmaFast, EndPointMovingAverage already exists

    /// <summary>
    /// Computes Coral Trend Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeCoralTrendIndicatorFast(StockData data, ComputeContext context, int length = 21)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.CoralTrendIndicator(close, buffer.WritableSpan, length, 0.4);
        return buffer;
    }

    /// <summary>
    /// Computes Damped Sine Wave Weighted Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDampedSineWaveWeightedFilterFast(StockData data, ComputeContext context, int length = 50)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.DampedSineWaveWeightedFilter(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Fibonacci Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeFibonacciWeightedMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.FibonacciWeightedMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Generalized Double Exponential Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeGeneralizedDoubleEmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.GeneralizedDoubleExponentialMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Geometric Mean Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeGeometricMeanMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.GeometricMeanMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Harmonic Mean Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeHarmonicMeanMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.HarmonicMeanMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Batch 19 - Ehlers Butterworth and Super Smoother Filters

    /// <summary>
    /// Computes Ehlers 2-Pole Butterworth Filter V1 using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlers2PoleButterworthFilterV1Fast(StockData data, ComputeContext context, int length = 10)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.Ehlers2PoleButterworthFilterV1(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers 2-Pole Butterworth Filter V2 using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlers2PoleButterworthFilterV2Fast(StockData data, ComputeContext context, int length = 15)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.Ehlers2PoleButterworthFilterV2(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers 3-Pole Butterworth Filter V1 using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlers3PoleButterworthFilterV1Fast(StockData data, ComputeContext context, int length = 10)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.Ehlers3PoleButterworthFilterV1(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers 3-Pole Butterworth Filter V2 using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlers3PoleButterworthFilterV2Fast(StockData data, ComputeContext context, int length = 15)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.Ehlers3PoleButterworthFilterV2(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers 2-Pole Super Smoother Filter V1 using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlers2PoleSuperSmootherFilterV1Fast(StockData data, ComputeContext context, int length = 15)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.Ehlers2PoleSuperSmootherFilterV1(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers 2-Pole Super Smoother Filter V2 using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlers2PoleSuperSmootherFilterV2Fast(StockData data, ComputeContext context, int length = 10)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.Ehlers2PoleSuperSmootherFilterV2(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers 3-Pole Super Smoother Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlers3PoleSuperSmootherFilterFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.Ehlers3PoleSuperSmootherFilter(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Decycler using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersDecyclerFast(StockData data, ComputeContext context, int length = 60)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersDecycler(close, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Batch 20 - Additional Ehlers Filters and Moving Averages

    /// <summary>
    /// Computes Ehlers Hamming Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersHammingMovingAverageFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersHammingMovingAverage(close, buffer.WritableSpan, length, 3);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Leading Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersLeadingIndicatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersLeadingIndicator(close, buffer.WritableSpan, 0.25, 0.33);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers High Pass Filter V1 using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersHighPassFilterV1Fast(StockData data, ComputeContext context, int length = 125)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersHighPassFilterV1(close, buffer.WritableSpan, length, 1);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers High Pass Filter V2 using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersHighPassFilterV2Fast(StockData data, ComputeContext context,
        int length = 20, MovingAvgType maType = MovingAvgType.WeightedMovingAverage)
    {
        return EhlersHighPassFilterV2(data, context, length, maType);
    }

    /// <summary>
    /// The twice-smoothed two-pole high-pass filter that CalculateEhlersHighPassFilterV2 publishes.
    /// </summary>
    /// <remarks>
    /// Shared rather than copied into each caller: the decycler oscillator is the difference of two of these
    /// at different lengths, and a second copy of the recursion would be a second chance to drift from it.
    /// </remarks>
    private static ComputeBuffer EhlersHighPassFilterV2(StockData data, ComputeContext context, int length,
        MovingAvgType maType)
    {
        length = Math.Max(length, 1);
        var (inputList, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = inputList.Count;

        var angle = MathHelper.Sqrt2 * Math.PI / length;
        var a1 = MathHelper.Exp(-angle);
        var c2 = 2 * a1 * Math.Cos(angle);
        var c3 = -a1 * a1;
        var c1 = (1 + c2 - c3) / 4;

        using var highPassBuffer = context.Rent(count);
        using var smoothedBuffer = context.Rent(count);
        var highPass = highPassBuffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            highPass[i] = i < 4
                ? 0
                : (c1 * (input[i] - (2 * input[i - 1]) + input[i - 2]))
                    + (c2 * highPass[i - 1]) + (c3 * highPass[i - 2]);
        }

        MovingAverage(data, maType, length, highPass, smoothedBuffer.WritableSpan);

        var buffer = context.Rent(count);
        MovingAverage(data, maType, length, smoothedBuffer.Span, buffer.WritableSpan);

        return buffer;
    }

    /// <summary>
    /// Computes Distance Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDistanceWeightedMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.DistanceWeightedMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersFilterFast(StockData data, ComputeContext context, int length1 = 15, int length2 = 5)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = data.Count;

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            double numerator = 0, coefficientSum = 0;
            for (var j = 0; j <= length1 - 1; j++)
            {
                var currentPrice = i >= j ? input[i - j] : 0;
                var previousPrice = i >= j + length2 ? input[i - (j + length2)] : 0;
                var priceDiff = Math.Abs(currentPrice - previousPrice);

                numerator += priceDiff * currentPrice;
                coefficientSum += priceDiff;
            }

            output[i] = coefficientSum != 0 ? numerator / coefficientSum : 0;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Finite Impulse Response Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersFirFilterFast(StockData data, ComputeContext context)
    {
        return ComputeEhlersFiniteImpulseResponseFilterFast(data, context);
    }

    /// <summary>
    /// Computes Ehlers Infinite Impulse Response Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersIirFilterFast(StockData data, ComputeContext context, int length = 14)
    {
        return ComputeEhlersInfiniteImpulseResponseFilterFast(data, context, length);
    }

    /// <summary>
    /// Computes Simple Cycle oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSimpleCycleFast(StockData data, ComputeContext context, int length = 50)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.SimpleCycle(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Simple Lines filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSimpleLinesFast(StockData data, ComputeContext context, int length = 10, double mult = 10)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.SimpleLines(close, buffer.WritableSpan, length, mult);
        return buffer;
    }

    /// <summary>
    /// Computes Double Exponential Smoothing using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDoubleExponentialSmoothingFast(StockData data, ComputeContext context)
    {
        // CalculateDoubleExponentialSmoothing takes no length: its two constants are fixed, and the trend it
        // carries forward is the change in its own last two values, damped by gamma. That is why the spec
        // marks its length as having no effect, and why it is no longer passed here.
        // OscillatorCore.DoubleExponentialSmoothing seeded its first bar with the close instead of starting
        // from nothing, so the two series never met.
        const double alpha = 0.01;
        const double gamma = 0.9;

        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = inputList.Count;

        var buffer = context.Rent(count);
        var s = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            var prevS = i >= 1 ? s[i - 1] : 0;
            var prevS2 = i >= 2 ? s[i - 2] : 0;
            var sChg = prevS - prevS2;

            s[i] = (alpha * input[i]) + ((1 - alpha) * (prevS + (gamma * (sChg + ((1 - gamma) * sChg)))));
        }

        return buffer;
    }

    /// <summary>
    /// Computes Detrended Synthetic Price oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDetrendedSyntheticPriceFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DetrendedSyntheticPrice(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Belkhayate Timing oscillator using zero-allocation fast path.
    /// </summary>
    /// <param name="data">Stock data.</param>
    /// <param name="context">Compute context for buffer pooling.</param>
    /// <param name="length">Unused parameter for source generator compatibility.</param>
    internal static ComputeBuffer ComputeBelkhayateTimingFast(StockData data, ComputeContext context, int length = 5)
    {
        _ = length; // Indicator has no configurable parameters
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.BelkhayateTiming(close, high, low, buffer.WritableSpan);
        return buffer;
    }

    #endregion

    #region Batch 22 - Counting and Performance Oscillators

    /// <summary>
    /// Computes Demark Setup Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDemarkSetupIndicatorFast(StockData data, ComputeContext context, int length = 4)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DemarkSetupIndicator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Performance Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePerformanceIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.PerformanceIndex(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Psychological Line using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePsychologicalLineFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.PsychologicalLine(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Move Tracker using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMoveTrackerFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length; // Indicator has no configurable parameters
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.MoveTracker(close, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Multi Level Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMultiLevelIndicatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.MultiLevelIndicator(close, open, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Market Direction Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMarketDirectionIndicatorFast(StockData data, ComputeContext context, int length = 13)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        // Default fastLength=13, slowLength=55
        OscillatorCore.MarketDirectionIndicator(close, buffer.WritableSpan, length, 55);
        return buffer;
    }

    // ComputeNthOrderDifferencingOscillatorFast already implemented in Batch 11

    /// <summary>
    /// Computes Morphed Sine Wave using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMorphedSineWaveFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.MorphedSineWave(close, buffer.WritableSpan, length);
        return buffer;
    }

    // ComputeMarketFacilitationIndexFast and ComputeVolumeAccumulationOscillatorFast already implemented in earlier batches

    #endregion

    #region Batch 23 - Simple Price, Volume, and Statistical Indicators

    /// <summary>
    /// Computes Full Typical Price (OHLC4) using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeFullTypicalPriceFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length; // Indicator has no configurable parameters
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.FullTypicalPrice(open, high, low, close, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Internal Bar Strength Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeInternalBarStrengthIndicatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.InternalBarStrength(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Z-Score using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeZScoreFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ZScore(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Fast Z-Score using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeFastZScoreFast(StockData data, ComputeContext context, int length = 5)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.FastZScore(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Kurtosis Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeKurtosisIndicatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.Kurtosis(close, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Batch 24 - Demark Indicators

    /// <summary>
    /// Computes Demark Range Expansion Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDemarkRangeExpansionIndexFast(StockData data, ComputeContext context, int length = 5)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DemarkRangeExpansionIndex(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Demark Pressure Ratio V1 using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDemarkPressureRatioV1Fast(StockData data, ComputeContext context, int length = 13)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DemarkPressureRatioV1(high, low, open, close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Demark Pressure Ratio V2 using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDemarkPressureRatioV2Fast(StockData data, ComputeContext context, int length = 10)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DemarkPressureRatioV2(high, low, open, close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Demark Reversal Points using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDemarkReversalPointsFast(StockData data, ComputeContext context, int length1 = 9, int length2 = 4)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DemarkReversalPoints(close, buffer.WritableSpan, length1, length2);
        return buffer;
    }

    #endregion

    #region Batch 25 - Additional Moving Averages (Unwired Core Methods)

    /// <summary>
    /// Computes Adaptive Autonomous Recursive Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAdaptiveAutonomousRecursiveMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.AdaptiveAutonomousRecursiveMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Corrected Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeCorrectedMovingAverageFast(StockData data, ComputeContext context,
        int length = 35, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // CalculateCorrectedMovingAverage pulls an average towards the price only as far as the last
        // correction was large compared with the variance of the window, so a quiet window barely moves it.
        // The average underneath takes whichever type the indicator was given.
        var (inputList, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        var count = inputList.Count;
        var input = SpanCompat.AsReadOnlySpan(inputList);

        using var averageBuffer = context.Rent(count);
        var average = averageBuffer.WritableSpan;
        MovingAverage(data, maType, length, input, average);

        using var stdDevBuffer = context.Rent(count);
        var stdDev = stdDevBuffer.WritableSpan;
        VolatilityCore.StandardDeviation(input, stdDev, Math.Max(1, length));

        var tolerance = MathHelper.Pow(10, -5);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            var sma = average[i];
            var previousCma = i >= 1 ? output[i - 1] : sma;
            var v1 = stdDev[i] * stdDev[i];
            var v2 = MathHelper.Pow(previousCma - sma, 2);
            var v3 = v1 == 0 || v2 == 0 ? 1 : v2 / (v1 + v2);

            double err = 1, kPrev = 1, k = 1;
            for (var j = 0; j <= 5000 && err > tolerance; j++)
            {
                k = v3 * kPrev * (2 - kPrev);
                err = kPrev - k;
                kPrev = k;
            }

            // Seeded at the average until the window is full, as the batch does.
            output[i] = i < length ? sma : previousCma + (k * (sma - previousCma));
        }

        return buffer;
    }

    /// <summary>
    /// Computes Cubed Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeCubedWeightedMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.CubedWeightedMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Dynamically Adjustable Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDynamicallyAdjustableFilterFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.DynamicallyAdjustableFilter(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Edge Preserving Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEdgePreservingFilterFast(StockData data, ComputeContext context,
        int length = 200, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int smoothLength = 50)
    {
        // CalculateEdgePreservingFilter averages the input over a run that restarts whenever the regressed
        // distance from the moving average reaches a new high on the side the input is not on. The output is
        // that running mean, so the edge is preserved by the restart rather than by any weighting.
        var (inputList, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = inputList.Count;

        using var averageBuffer = context.Rent(count);
        MovingAverage(data, maType, length, input, averageBuffer.WritableSpan);
        var average = averageBuffer.Span;

        using var offsetBuffer = context.Rent(count);
        using var regressedBuffer = context.Rent(count);
        var offset = offsetBuffer.WritableSpan;
        var regressed = regressedBuffer.WritableSpan;
        using var leastSquares = new RollingLeastSquares(smoothLength);

        for (var i = 0; i < count; i++)
        {
            offset[i] = input[i] - average[i];
            regressed[i] = leastSquares.Next(Math.Abs(offset[i]), isFinal: true).Last;
        }

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        var window = new RollingMinMax(Math.Max(length, 2));
        double previousRatio = 0;
        double runLength = 0;
        double runSum = 0;

        for (var i = 0; i < count; i++)
        {
            window.Add(regressed[i]);

            var ratio = window.Max != 0 ? regressed[i] / window.Max : 0;
            var restart = ratio == 1 && previousRatio != 1 && offset[i] != 0;
            previousRatio = ratio;

            // The run is one bar long before the first bar, and the sum is seeded with that bar's own
            // value, so a bar that does not restart the run counts itself twice - as the batch does.
            var previousRunLength = i >= 1 ? runLength : 1;
            var previousRunSum = i >= 1 ? runSum : input[i];
            runLength = restart ? 1 : previousRunLength + 1;
            runSum = runLength == 1 ? input[i] : previousRunSum + input[i];

            output[i] = runLength != 0 ? runSum / runLength : 0;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Ehlers All Pass Phase Shifter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersAllPassPhaseShifterFast(StockData data, ComputeContext context, int length = 20, double qq = 0.5)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = data.Count;

        var a2 = qq != 0 && length != 0 ? -2 * Math.Cos(2 * Math.PI / length) / qq : 0;
        var a3 = qq != 0 ? MathHelper.Pow(1 / qq, 2) : 0;
        var b2 = length != 0 ? -2 * qq * Math.Cos(2 * Math.PI / length) : 0;
        var b3 = MathHelper.Pow(qq, 2);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            var previousValue1 = i >= 1 ? input[i - 1] : 0;
            var previousValue2 = i >= 2 ? input[i - 2] : 0;
            var previousPhaser1 = i >= 1 ? output[i - 1] : 0;
            var previousPhaser2 = i >= 2 ? output[i - 2] : 0;

            output[i] = (b3 * (input[i] + (a2 * previousValue1) + (a3 * previousValue2))) - (b2 * previousPhaser1) -
                (b3 * previousPhaser2);
        }

        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Average Error Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersAverageErrorFilterFast(StockData data, ComputeContext context, int length = 27)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = data.Count;
        length = Math.Max(length, 1);

        var a1 = MathHelper.Exp(MathHelper.MinOrMax(-MathHelper.Sqrt2 * Math.PI / length, -0.01, -0.99));
        var b1 = 2 * a1 * Math.Cos(MathHelper.MinOrMax(MathHelper.Sqrt2 * Math.PI / length, 0.99, 0.01));
        var c2 = b1;
        var c3 = -1 * a1 * a1;
        var c1 = 1 - c2 - c3;

        using var superSmoothed = context.Rent(count);
        using var errorFilter = context.Rent(count);
        var ssf = superSmoothed.WritableSpan;
        var e1 = errorFilter.WritableSpan;

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            var previousValue = i >= 1 ? input[i - 1] : 0;

            ssf[i] = i < 3 ? input[i] : (0.5 * c1 * (input[i] + previousValue)) + (c2 * ssf[i - 1]) + (c3 * ssf[i - 2]);
            e1[i] = i < 3 ? 0 : (c1 * (input[i] - ssf[i])) + (c2 * e1[i - 1]) + (c3 * e1[i - 2]);
            output[i] = ssf[i] + e1[i];
        }

        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Distance Coefficient Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersDistanceCoefficientFilterFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = data.Count;

        // The squared distance of a bar from its own lookback window depends only on that bar, so it is
        // computed once per bar and reused across the coefficient sums.
        using var distances = context.Rent(count);
        var distanceByBar = distances.WritableSpan;
        for (var p = 0; p < count; p++)
        {
            double distance = 0;
            for (var lookBack = 1; lookBack <= length - 1; lookBack++)
            {
                var back = p >= lookBack ? input[p - lookBack] : 0;
                distance += MathHelper.Pow(input[p] - back, 2);
            }

            distanceByBar[p] = distance;
        }

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            double sourceSum = 0, coefficientSum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var previousValue = i >= j ? input[i - j] : 0;
                var distance = i >= j ? distanceByBar[i - j] : 0;

                sourceSum += distance * previousValue;
                coefficientSum += distance;
            }

            output[i] = coefficientSum != 0 ? sourceSum / coefficientSum : 0;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Kaufman Adaptive Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersKaufmanAdaptiveMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.EhlersKaufmanAdaptiveMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Modified Optimum Elliptic Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersModifiedOptimumEllipticFilterFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.EhlersModifiedOptimumEllipticFilter(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Noise Elimination Technology using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersNoiseEliminationTechnologyFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.EhlersNoiseEliminationTechnology(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Optimum Elliptic Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersOptimumEllipticFilterFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.EhlersOptimumEllipticFilter(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Variable Index Dynamic Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersVariableIndexDynamicAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.EhlersVariableIndexDynamicAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Falling Rising Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeFallingRisingFilterFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.FallingRisingFilter(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Farey Sequence Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeFareySequenceWeightedMovingAverageFast(StockData data, ComputeContext context, int length = 5)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.FareySequenceWeightedMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Fisher Least Squares Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeFisherLeastSquaresMovingAverageFast(StockData data, ComputeContext context, int length = 100)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.FisherLeastSquaresMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Following Adaptive Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeFollowingAdaptiveMovingAverageFast(StockData data, ComputeContext context,
        double fastAlpha = 0.5, double slowAlpha = 0.05)
    {
        // The following adaptive moving average is the mother's second series: the same adaptive smoothing
        // applied again at half the rate, which is what the batch publishes as Fama.
        return ComputeEhlersMotherOfAdaptiveMovingAveragesFast(data, context, fastAlpha, slowAlpha, EhlersMamaOutput.Fama);
    }

    /// <summary>
    /// Computes General Filter Estimator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeGeneralFilterEstimatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.GeneralFilterEstimator(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Henderson Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeHendersonWeightedMovingAverageFast(StockData data, ComputeContext context, int length = 7)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.HendersonWeightedMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Hull Estimate using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeHullEstimateFast(StockData data, ComputeContext context, int length = 50)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.HullEstimate(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Hybrid Convolution Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeHybridConvolutionFilterFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.HybridConvolutionFilter(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes IIR Least Squares Estimate using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeIIRLeastSquaresEstimateFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.IIRLeastSquaresEstimate(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Inverse Distance Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeInverseDistanceWeightedMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.InverseDistanceWeightedMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Inverse Fisher Transform using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeInverseFisherTransformCoreFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.InverseFisherTransform(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Jsa Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeJsaMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        // CalculateJsaMovingAverage is the midpoint between the current bar and the bar a whole length ago,
        // and nothing else; before a full length has passed the older bar counts as zero, so the opening bars
        // are half of price. MovingAverageCore.JsaMovingAverage smoothed instead of pairing.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = inputList.Count;
        length = Math.Max(length, 1);

        var buffer = context.Rent(count);
        var jma = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            var priorValue = i >= length ? input[i - length] : 0;
            jma[i] = (input[i] + priorValue) / 2;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Kalman Smoother using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeKalmanSmootherFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.KalmanSmoother(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Kaufman Adaptive Least Squares Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeKaufmanAdaptiveLeastSquaresMovingAverageFast(StockData data, ComputeContext context, int length = 100)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.KaufmanAdaptiveLeastSquaresMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Leo Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeLeoMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.LeoMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Light Least Squares Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeLightLeastSquaresMovingAverageFast(StockData data, ComputeContext context, int length = 250)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.LightLeastSquaresMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Linear Extrapolation using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeLinearExtrapolationFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.LinearExtrapolation(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Linear Regression Line using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeLinearRegressionLineFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.LinearRegressionLine(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Linear Weighted Moving Average (Core) using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeLinearWeightedMovingAverageCoreFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.LinearWeightedMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes McNicholl Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMcNichollMovingAverageFast(StockData data, ComputeContext context, int length = 20)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.McNichollMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Moving Average Adaptive Q using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMovingAverageAdaptiveQFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.MovingAverageAdaptiveQ(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Moving Average V3 using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMovingAverageV3Fast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.MovingAverageV3(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes One LC Least Squares Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeOneLCLeastSquaresMovingAverageFast(StockData data, ComputeContext context, int length = 32)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.OneLCLeastSquaresMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Optimal Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeOptimalWeightedMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        // CalculateOptimalWeightedMovingAverage raises each bar's distance from the present to the correlation
        // between price and the average's own previous value, so the weighting tightens onto recent bars as the
        // average tracks price and flattens when it does not. MovingAverageCore.OptimalWeightedMovingAverage
        // uses fixed weights and agrees with none of it. RollingWindowCorrelation is the pooled form of the
        // batch's RollingCorrelation, so the arm stays allocation free.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = inputList.Count;
        length = Math.Max(length, 1);

        var buffer = context.Rent(count);
        var owma = buffer.WritableSpan;

        using var correlation = new Streaming.RollingWindowCorrelation(length);
        double previousOwma = 0;
        for (var i = 0; i < count; i++)
        {
            var corr = correlation.Add(input[i], previousOwma, out _);
            corr = MathHelper.IsValueNullOrInfinity(corr) ? 0 : corr;

            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var weight = MathHelper.Pow(length - j, corr);
                var previousValue = i >= j ? input[i - j] : 0;

                sum += previousValue * weight;
                weightedSum += weight;
            }

            previousOwma = weightedSum != 0 ? sum / weightedSum : 0;
            owma[i] = previousOwma;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Overshoot Reduction Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeOvershootReductionMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.OvershootReductionMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Parametric Corrective Linear Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeParametricCorrectiveLinearMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.ParametricCorrectiveLinearMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Parametric Kalman Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeParametricKalmanFilterFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.ParametricKalmanFilter(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Batch 26 - Additional Unwired Core Methods

    /// <summary>
    /// Computes Zero Low Lag Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeZeroLowLagMovingAverageFast(StockData data, ComputeContext context, int length = 32)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.ZeroLowLagMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Recursive Moving Trend Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeRecursiveMovingTrendAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.RecursiveMovingTrendAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Trimean using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeTrimeanFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.Trimean(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Skewness using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSkewnessFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.Skewness(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Hampel Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeHampelFilterFast(StockData data, ComputeContext context, int length = 14, double scalingFactor = 3)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.HampelFilter(inputSpan, buffer.WritableSpan, length, scalingFactor);
        return buffer;
    }

    /// <summary>
    /// Computes Modular Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeModularFilterFast(StockData data, ComputeContext context, int length = 200, double beta = 0.8, double z = 0.5)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.ModularFilter(inputSpan, buffer.WritableSpan, length, beta, z);
        return buffer;
    }

    /// <summary>
    /// Computes Dynamically Adjustable Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDynamicallyAdjustableMovingAverageFast(StockData data, ComputeContext context, int fastLength = 6, int slowLength = 200)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.DynamicallyAdjustableMovingAverage(inputSpan, buffer.WritableSpan, fastLength, slowLength);
        return buffer;
    }

    /// <summary>
    /// Computes Equity Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEquityMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.EquityMovingAverage(inputSpan, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Multi Depth Zero Lag Exponential Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMultiDepthZeroLagExponentialMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.MultiDepthZeroLagExponentialMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Polynomial Least Squares Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePolynomialLeastSquaresMovingAverageFast(StockData data, ComputeContext context, int length = 50)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.PolynomialLeastSquaresMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Powered Kaufman Adaptive Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePoweredKaufmanAdaptiveMovingAverageFast(StockData data, ComputeContext context, int length = 10)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.PoweredKaufmanAdaptiveMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Quadratic Least Squares Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeQuadraticLeastSquaresMovingAverageFast(StockData data, ComputeContext context, int length = 50)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.QuadraticLeastSquaresMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Quadratic Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeQuadraticMovingAverageFast(StockData data, ComputeContext context, int length = 50)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.QuadraticMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Quadratic Regression using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeQuadraticRegressionFast(StockData data, ComputeContext context, int length = 50)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.QuadraticRegression(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes R2 Adaptive Regression using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeR2AdaptiveRegressionFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.R2AdaptiveRegression(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Retention Acceleration Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeRetentionAccelerationFilterFast(StockData data, ComputeContext context, int length = 10)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.RetentionAccelerationFilter(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Right Sided Ricker Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeRightSidedRickerMovingAverageFast(StockData data, ComputeContext context, int length = 10)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.RightSidedRickerMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Self Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSelfWeightedMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.SelfWeightedMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Sequentially Filtered Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSequentiallyFilteredMovingAverageFast(StockData data, ComputeContext context, int length = 50)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.SequentiallyFilteredMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Setting Less Trend Step Filtering using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSettingLessTrendStepFilteringFast(StockData data, ComputeContext context, int length = 100)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.SettingLessTrendStepFiltering(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Shapeshifting Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeShapeshiftingMovingAverageFast(StockData data, ComputeContext context, int length = 50)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.ShapeshiftingMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Sharp Modified Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSharpModifiedMovingAverageFast(StockData data, ComputeContext context, int length = 7)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.SharpModifiedMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Simplified Least Squares Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSimplifiedLeastSquaresMovingAverageFast(StockData data, ComputeContext context, int length = 25)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.SimplifiedLeastSquaresMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Simplified Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSimplifiedWeightedMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.SimplifiedWeightedMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Svama using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSvamaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.Svama(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Three HMA using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeThreeHMAFast(StockData data, ComputeContext context, int length = 50)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.ThreeHMA(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Tillson IE2 using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeTillsonIE2Fast(StockData data, ComputeContext context, int length = 15)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.TillsonIE2(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes T-Step Least Squares Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeTStepLeastSquaresMovingAverageFast(StockData data, ComputeContext context, int length = 100)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.TStepLeastSquaresMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Variable Adaptive Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVariableAdaptiveMovingAverageFast(StockData data, ComputeContext context, int length = 6)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.VariableAdaptiveMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Variable Length Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVariableLengthMovingAverageFast(StockData data, ComputeContext context, int length = 5)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.VariableLengthMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Vertical Horizontal Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVerticalHorizontalMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.VerticalHorizontalMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Volatility Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVolatilityMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.VolatilityMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Volatility Wave Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVolatilityWaveMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.VolatilityWaveMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Well Rounded Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeWellRoundedMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.WellRoundedMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Wilders Summation Method using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeWildersSummationMethodFast(StockData data, ComputeContext context, int length = 14)
    {
        // CalculateWellesWilderSummation keeps a running total that sheds a length-th of itself before each new
        // value joins it, so it carries a value from the very first bar. MovingAverageCore.WildersSummationMethod
        // sheds a different share and the two parted company at bar one.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = inputList.Count;
        length = Math.Max(length, 1);

        var buffer = context.Rent(count);
        var sum = buffer.WritableSpan;

        double previousSum = 0;
        for (var i = 0; i < count; i++)
        {
            previousSum = previousSum - (previousSum / length) + input[i];
            sum[i] = previousSum;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Zero Lag Triple Exponential Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeZeroLagTripleExponentialMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.ZeroLagTripleExponentialMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Batch 27 - Multi-Input Core Methods

    /// <summary>
    /// Computes DeMarker using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDeMarkerFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DeMarker(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Middle High Low Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMiddleHighLowMovingAverageFast(StockData data, ComputeContext context, int length1 = 14,
        int length2 = 10, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        // CalculateMiddleHighLowMovingAverage averages the midpoint of the chained series over length1, where
        // the midpoint is the mean of that series' own rolling extremes - exactly what CalculateMidpoint
        // publishes, so the agreeing midpoint arm is walked rather than the formula repeated here.
        // MovingAverageCore.MiddleHighLowMovingAverage read the high and low series, which this indicator
        // never touches, and the moving average type never reached it at all.
        using var midpoint = ComputeMidpointFast(data, context, length2);

        var buffer = context.Rent(data.Count);
        MovingAverage(data, maType, length1, midpoint.Span, buffer.WritableSpan);

        return buffer;
    }

    /// <summary>
    /// Computes Vortex Minus using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVortexMinusFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.VortexMinus(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Vortex Plus using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVortexPlusFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.VortexPlus(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Volume Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVolumeWeightedMovingAverageFast(StockData data, ComputeContext context, int length = 20)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.VolumeWeightedMovingAverage(inputSpan, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Klinger Signal using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeKlingerSignalFast(StockData data, ComputeContext context, int fastLength = 34, int slowLength = 55, int signalLength = 13)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        OscillatorCore.KlingerSignal(high, low, close, volume, buffer.WritableSpan, fastLength, slowLength, signalLength);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Chebyshev Low Pass Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersChebyshevLowPassFilterFast(StockData data, ComputeContext context, int length = 14, double ripple = 0.5)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.EhlersChebyshevLowPassFilter(inputSpan, buffer.WritableSpan, length, ripple);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Gaussian Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersGaussianFilterFast(StockData data, ComputeContext context, int length = 14, int poles = 3)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.EhlersGaussianFilter(inputSpan, buffer.WritableSpan, length, poles);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Median Average Adaptive Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersMedianAverageAdaptiveFilterFast(StockData data, ComputeContext context, int length = 39, double threshold = 0.002)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.EhlersMedianAverageAdaptiveFilter(inputSpan, buffer.WritableSpan, length, threshold);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Mesa Adaptive Moving Average using zero-allocation fast path.
    /// </summary>
    /// <summary>
    /// Which of the mother of adaptive moving averages' eight series an arm has been asked for. One walk
    /// produces all of them, so the series is chosen on the way out.
    /// </summary>
    internal enum EhlersMamaOutput
    {
        Mama,
        Fama,
        InPhase,
        Quadrature,
        SmoothPeriod,
        Smooth,
        Real,
        Imaginary
    }

    /// <summary>
    /// One series of the mother of adaptive moving averages, walked through the same engine the streaming
    /// state uses so all three engines answer alike.
    /// </summary>
    /// <remarks>
    /// CalculateEhlersMotherOfAdaptiveMovingAverages measures the dominant cycle with a Hilbert transform and
    /// adapts its smoothing to the rate the cycle's phase turns. It takes no length - only the fast and slow
    /// bounds on that smoothing - which is why the two specs bound to it mark their length as having no
    /// effect. MovingAverageCore had a length-driven average in its place that matched neither series.
    /// </remarks>
    internal static ComputeBuffer ComputeEhlersMotherOfAdaptiveMovingAveragesFast(StockData data, ComputeContext context,
        double fastAlpha = 0.5, double slowAlpha = 0.05, EhlersMamaOutput output = EhlersMamaOutput.Mama)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = inputList.Count;

        var buffer = context.Rent(count);
        var values = buffer.WritableSpan;

        using var engine = new Streaming.EhlersMotherOfAdaptiveMovingAveragesEngine(fastAlpha, slowAlpha);
        for (var i = 0; i < count; i++)
        {
            var snapshot = engine.Next(input[i], isFinal: true);
            values[i] = output switch
            {
                EhlersMamaOutput.Fama => snapshot.Fama,
                EhlersMamaOutput.InPhase => snapshot.I1,
                EhlersMamaOutput.Quadrature => snapshot.Q1,
                EhlersMamaOutput.SmoothPeriod => snapshot.SmoothPeriod,
                EhlersMamaOutput.Smooth => snapshot.Smooth,
                EhlersMamaOutput.Real => snapshot.Real,
                EhlersMamaOutput.Imaginary => snapshot.Imag,
                _ => snapshot.Mama
            };
        }

        return buffer;
    }

    internal static ComputeBuffer ComputeEhlersMesaAdaptiveMovingAverageFast(StockData data, ComputeContext context,
        double fastLimit = 0.5, double slowLimit = 0.05, EhlersMamaOutput output = EhlersMamaOutput.Mama)
    {
        // The MESA adaptive moving average is the mother's own series: the batch publishes it as Mama and
        // carries it as the indicator's values.
        return ComputeEhlersMotherOfAdaptiveMovingAveragesFast(data, context, fastLimit, slowLimit, output);
    }

    /// <summary>
    /// Computes Ehlers Recursive Median Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersRecursiveMedianFilterFast(StockData data, ComputeContext context, int length1 = 5, int length2 = 12)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = data.Count;

        // Alpha is derived from length2, not supplied: the batch has no alpha parameter.
        var alphaArg = MathHelper.MinOrMax(2 * Math.PI / Math.Max(length2, 1), 0.99, 0.01);
        var alphaArgCos = Math.Cos(alphaArg);
        var alpha = alphaArgCos != 0 ? (alphaArgCos + Math.Sin(alphaArg) - 1) / alphaArgCos : 0;

        using var median = new RollingMedian(Math.Max(length1, 1));
        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            median.Add(input[i]);

            var previousFilter = i >= 1 ? output[i - 1] : 0;
            output[i] = (alpha * median.Median) + ((1 - alpha) * previousFilter);
        }

        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Roofing Filter using zero-allocation fast path.
    /// </summary>
    /// <summary>
    /// Computes the Ehlers high pass filter V1, the two-pole high pass the roofing filter is built on.
    /// </summary>
    private static ComputeBuffer EhlersHighPassFilterV1(StockData data, ComputeContext context, int length, double mult)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        return EhlersHighPassFilterV1(context, SpanCompat.AsReadOnlySpan(inputList), length, mult);
    }

    /// <summary>
    /// Computes the Ehlers high pass filter V1 over an arbitrary series.
    /// </summary>
    private static ComputeBuffer EhlersHighPassFilterV1(ComputeContext context, ReadOnlySpan<double> input, int length, double mult)
    {
        var count = input.Length;
        length = Math.Max(length, 1);

        var alphaArg = MathHelper.MinOrMax(2 * Math.PI / (mult * length * MathHelper.Sqrt(2)), 0.99, 0.01);
        var alphaCos = Math.Cos(alphaArg);
        var alpha = alphaCos != 0 ? (alphaCos + Math.Sin(alphaArg) - 1) / alphaCos : 0;
        var pow1 = MathHelper.Pow(1 - (alpha / 2), 2);
        var pow2 = MathHelper.Pow(1 - alpha, 2);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            var previousValue1 = i >= 1 ? input[i - 1] : 0;
            var previousValue2 = i >= 2 ? input[i - 2] : 0;
            var previousHp1 = i >= 1 ? output[i - 1] : 0;
            var previousHp2 = i >= 2 ? output[i - 2] : 0;

            output[i] = (pow1 * (input[i] - (2 * previousValue1) + previousValue2)) + (2 * (1 - alpha) * previousHp1) -
                (pow2 * previousHp2);
        }

        return buffer;
    }

    /// <summary>
    /// Computes the Ehlers roofing filter V1: a smoothed two-bar average of the high pass filter.
    /// </summary>
    private static ComputeBuffer EhlersRoofingFilterV1Core(StockData data, ComputeContext context, int length1, int length2, MovingAvgType maType)
    {
        var count = data.Count;
        using var highPass = EhlersHighPassFilterV1(data, context, length1, 1);
        var hp = highPass.Span;

        using var averaged = context.Rent(count);
        var arg = averaged.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            arg[i] = (hp[i] + (i >= 1 ? hp[i - 1] : 0)) / 2;
        }

        var buffer = context.Rent(count);
        MovingAverage(data, maType, Math.Max(length2, 1), averaged.Span, buffer.WritableSpan);
        return buffer;
    }

    internal static ComputeBuffer ComputeEhlersRoofingFilterFast(StockData data, ComputeContext context, int hpLength = 48, int lpLength = 10,
        MovingAvgType maType = MovingAvgType.Ehlers2PoleSuperSmootherFilterV1)
    {
        return EhlersRoofingFilterV1Core(data, context, hpLength, lpLength, maType);
    }

    #endregion

    #region Batch 28 - Final Unwired Core Methods

    /// <summary>
    /// Computes Ehlers Deviation Scaled Super Smoother using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersDeviationScaledSuperSmootherFast(StockData data,
        ComputeContext context, int length1 = 12,
        MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage, int length2 = 50)
    {
        // CalculateEhlersDeviationScaledSuperSmoother scales a smoothed momentum by its own root mean square
        // and lets that magnitude set the super smoother's cutoff bar by bar, so the filter tightens as the
        // move grows. The coefficients come from the batch's own helper rather than a second copy of them.
        var (inputList, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = inputList.Count;

        using var momentumBuffer = context.Rent(count);
        using var filteredBuffer = context.Rent(count);
        using var powerBuffer = context.Rent(count);
        var momentum = momentumBuffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            momentum[i] = input[i] - (i >= length1 ? input[i - length1] : 0);
        }

        var hannLength = (int)Math.Ceiling(length1 / 1.4m);
        MovingAverage(data, maType, hannLength, momentum, filteredBuffer.WritableSpan);

        var filtered = filteredBuffer.Span;
        var power = powerBuffer.WritableSpan;
        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            power[i] = MathHelper.Pow(filtered[i], 2);

            var window = Math.Min(length2, i + 1);
            double sum = 0;
            for (var j = i - window + 1; j <= i; j++)
            {
                sum += power[j];
            }

            var meanPower = sum / window;
            var rms = meanPower > 0 ? MathHelper.Sqrt(meanPower) : 0;
            var scaled = rms != 0 ? filtered[i] / rms : 0;

            var (c1, c2, c3) = Calculations.DeviationScaledSuperSmootherCoefficients(scaled, length1);

            var previous1 = i >= 1 ? output[i - 1] : 0;
            var previous2 = i >= 2 ? output[i - 2] : 0;
            var midpoint = (input[i] + (i >= 1 ? input[i - 1] : 0)) / 2;

            output[i] = (c1 * midpoint) + (c2 * previous1) + (c3 * previous2);
        }

        return buffer;
    }

    /// <summary>
    /// Computes PPO MA using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePpoMaFast(StockData data, ComputeContext context, int fastLength = 12, int slowLength = 26)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.PpoMa(inputSpan, buffer.WritableSpan, fastLength, slowLength);
        return buffer;
    }

    /// <summary>
    /// Computes Price Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePriceOscillatorFast(StockData data, ComputeContext context, int shortLength = 10, int longLength = 20)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.PriceOscillator(inputSpan, buffer.WritableSpan, shortLength, longLength);
        return buffer;
    }

    /// <summary>
    /// Computes Reverse Engineering RSI using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeReverseEngineeringRsiFast(StockData data, ComputeContext context, int length = 14, double rsiLevel = 50)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.ReverseEngineeringRsi(inputSpan, buffer.WritableSpan, length, rsiLevel);
        return buffer;
    }

    /// <summary>
    /// Computes Reverse Moving Average Convergence Divergence using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeReverseMovingAverageConvergenceDivergenceFast(StockData data, ComputeContext context, int fastLength = 12, int slowLength = 26, double macdLevel = 0)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.ReverseMovingAverageConvergenceDivergence(inputSpan, buffer.WritableSpan, fastLength, slowLength, macdLevel);
        return buffer;
    }

    /// <summary>
    /// Computes Simple Price Zone using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSimplePriceZoneFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.SimplePriceZone(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Elastic Volume Weighted Moving Average V2 using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeElasticVolumeWeightedMovingAverageV2Fast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.ElasticVolumeWeightedMovingAverageV2(inputSpan, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Windowed Volume Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeWindowedVolumeWeightedMovingAverageFast(StockData data, ComputeContext context,
        int length = 100)
    {
        // CalculateWindowedVolumeWeightedMovingAverage weights each bar's volume by a Bartlett window taken
        // over the bar number rather than over the position within the window, then divides the running sum of
        // value times that weight by the running sum of the weight alone. The batch computes Blackman and
        // Hanning windows alongside it but publishes only the Bartlett one, so that is the series walked here.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var volumes = SpanCompat.AsReadOnlySpan(data.Volumes);
        var count = inputList.Count;
        length = Math.Max(length, 1);

        var buffer = context.Rent(count);
        var wvwma = buffer.WritableSpan;

        var weightSum = new RollingSum();
        var weightedValueSum = new RollingSum();
        for (var i = 0; i < count; i++)
        {
            var bartlett = 1 - (2 * Math.Abs(i - ((double)length / 2)) / length);
            var weight = bartlett * volumes[i];
            weightSum.Add(weight);
            weightedValueSum.Add(input[i] * weight);

            var totalWeight = weightSum.Sum(length);
            wvwma[i] = totalWeight != 0 ? weightedValueSum.Sum(length) / totalWeight : 0;
        }

        return buffer;
    }

    /// <summary>
    /// Computes ATR Filtered Exponential Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAtrFilteredExponentialMovingAverageFast(StockData data, ComputeContext context, int length = 45, int atrLength = 20, int stdDevLength = 10, int lbLength = 20, double min = 5)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.AtrFilteredExponentialMovingAverage(inputSpan, high, low, buffer.WritableSpan, length, atrLength, stdDevLength, lbLength, min);
        return buffer;
    }

    /// <summary>
    /// Computes True Range Adjusted Exponential Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeTrueRangeAdjustedExponentialMovingAverageFast(StockData data, ComputeContext context, int length = 14, double mult = 1.5)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.TrueRangeAdjustedExponentialMovingAverage(inputSpan, high, low, buffer.WritableSpan, length, mult);
        return buffer;
    }

    /// <summary>
    /// Computes Relative Volatility Index High using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeRelativeVolatilityIndexHighFast(StockData data, ComputeContext context, int length = 14, int stdDevLength = 10)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.RelativeVolatilityIndexHigh(high, buffer.WritableSpan, length, stdDevLength);
        return buffer;
    }

    /// <summary>
    /// Computes Relative Volatility Index Low using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeRelativeVolatilityIndexLowFast(StockData data, ComputeContext context, int length = 14, int stdDevLength = 10)
    {
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.RelativeVolatilityIndexLow(low, buffer.WritableSpan, length, stdDevLength);
        return buffer;
    }

    /// <summary>
    /// Computes Typical Price Volatility using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeTypicalPriceVolatilityFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.TypicalPriceVolatility(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ratio OCHL Averager using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeRatioOchlAveragerFast(StockData data, ComputeContext context)
    {
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.RatioOchlAverager(open, close, high, low, buffer.WritableSpan);
        return buffer;
    }

    #endregion

    #region Batch 29 - Additional Missing Indicators

    /// <summary>
    /// Computes Triple Hull Moving Average (3HMA) using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeTripleHullMovingAverageFast(StockData data, ComputeContext context, int length = 50)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        TrendCore.TripleHullMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Adaptive Autonomous Recursive Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAdaptiveAutonomousRecursiveMovingAverageFast(StockData data, ComputeContext context, int length = 14, double lambda = 1)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        TrendCore.AdaptiveAutonomousRecursiveMovingAverage(inputSpan, buffer.WritableSpan, length, lambda);
        return buffer;
    }

    #endregion

    #region Batch 30 - Additional Missing Core Methods

    /// <summary>
    /// Computes Generalized Double Exponential Moving Average (GDEMA) using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeGeneralizedDoubleExponentialMovingAverageFast(StockData data, ComputeContext context, int length = 14, double volumeFactor = 1.0)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.GeneralizedDoubleExponentialMovingAverage(inputSpan, buffer.WritableSpan, length, volumeFactor);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Finite Impulse Response Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersFiniteImpulseResponseFilterFast(StockData data, ComputeContext context, double coef1 = 1,
        double coef2 = 3.5, double coef3 = 4.5, double coef4 = 3, double coef5 = 0.5, double coef6 = -0.5, double coef7 = -1.5)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = data.Count;
        var coefficientSum = coef1 + coef2 + coef3 + coef4 + coef5 + coef6 + coef7;

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            var weighted = (coef1 * input[i]) + (coef2 * (i >= 1 ? input[i - 1] : 0)) + (coef3 * (i >= 2 ? input[i - 2] : 0)) +
                (coef4 * (i >= 3 ? input[i - 3] : 0)) + (coef5 * (i >= 4 ? input[i - 4] : 0)) +
                (coef6 * (i >= 5 ? input[i - 5] : 0)) + (coef7 * (i >= 6 ? input[i - 6] : 0));
            output[i] = coefficientSum != 0 ? weighted / coefficientSum : 0;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Infinite Impulse Response Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersInfiniteImpulseResponseFilterFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = data.Count;
        length = Math.Max(length, 1);

        var alpha = 2d / (length + 1);
        var lag = MathHelper.MinOrMax((int)Math.Ceiling((1 / alpha) - 1));

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            var previousValue = i >= lag ? input[i - lag] : 0;
            var previousFilter = i >= 1 ? output[i - 1] : 0;

            output[i] = (alpha * (input[i] + CalculationsHelper.MinPastValues(i, lag, input[i] - previousValue))) +
                ((1 - alpha) * previousFilter);
        }

        return buffer;
    }

    /// <summary>
    /// Computes Volume Adjusted Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVolumeAdjustedMovingAverageFast(StockData data, ComputeContext context, int length = 14, double factor = 0.67)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.VolumeAdjustedMovingAverage(close, volume, buffer.WritableSpan, length, factor);
        return buffer;
    }

    /// <summary>
    /// Computes Average Day Range using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAverageDayRangeFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.AverageDayRange(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Chande Intraday Momentum Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChandeIntradayMomentumIndexFast(StockData data, ComputeContext context,
        int length = 14)
    {
        // CalculateChandeIntradayMomentumIndex reads how much of each bar closed above where it opened, and
        // its running gain carries from the previous bar rather than starting fresh, so a run of up bars
        // compounds and a single down bar resets it to nothing.
        var (inputList, _, _, openList, _) = CalculationsHelper.GetInputValuesList(data);
        var count = inputList.Count;

        var gainSum = new RollingSum();
        var lossSum = new RollingSum();
        var runningGain = 0d;
        var runningLoss = 0d;

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            var close = inputList[i];
            var open = openList[i];

            runningGain = close > open ? runningGain + (close - open) : 0;
            runningLoss = close < open ? runningLoss + (open - close) : 0;
            gainSum.Add(runningGain);
            lossSum.Add(runningLoss);

            var up = gainSum.Sum(length);
            var down = lossSum.Sum(length);
            output[i] = up + down != 0 ? MathHelper.MinOrMax(100 * up / (up + down), 100, 0) : 0;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Contract High (running maximum) using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeContractHighFast(StockData data, ComputeContext context)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ContractHigh(high, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Contract Low (running minimum) using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeContractLowFast(StockData data, ComputeContext context)
    {
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ContractLow(low, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Oscar Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeOscarIndicatorFast(StockData data, ComputeContext context, int length = 8)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.OscarIndicator(close, high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Narrow Bandpass Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeNarrowBandpassFilterFast(StockData data, ComputeContext context, int length = 50)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.NarrowBandpassFilter(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes TFS Tether Line using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeTFSTetherLineFast(StockData data, ComputeContext context, int length = 50)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.TFSTetherLineIndicator(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Williams Fractals Up using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeWilliamsFractalsUpFast(StockData data, ComputeContext context, int length = 2)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var upBuffer = context.Rent(data.Count);
        Span<double> downBuffer = stackalloc double[data.Count > 8192 ? 0 : data.Count];
        if (downBuffer.Length == 0)
        {
            var tempArray = new double[data.Count];
            downBuffer = tempArray.AsSpan();
        }
        OscillatorCore.WilliamsFractals(high, low, upBuffer.WritableSpan, downBuffer, length);
        return upBuffer;
    }

    /// <summary>
    /// Computes Williams Fractals Down using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeWilliamsFractalsDownFast(StockData data, ComputeContext context, int length = 2)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        Span<double> upBuffer = stackalloc double[data.Count > 8192 ? 0 : data.Count];
        if (upBuffer.Length == 0)
        {
            var tempArray = new double[data.Count];
            upBuffer = tempArray.AsSpan();
        }
        var downBuffer = context.Rent(data.Count);
        OscillatorCore.WilliamsFractals(high, low, upBuffer, downBuffer.WritableSpan, length);
        return downBuffer;
    }

    /// <summary>
    /// Computes Upside Downside Volume using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeUpsideDownsideVolumeFast(StockData data, ComputeContext context, int length = 50)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        OscillatorCore.UpsideDownsideVolume(close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Vortex Indicator Plus using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVortexIndicatorPlusFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.VortexIndicatorPlus(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Vortex Indicator Minus using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVortexIndicatorMinusFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.VortexIndicatorMinus(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Guppy Count Back Line using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeGuppyCountBackLineFast(StockData data, ComputeContext context, int length = 21)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.GuppyCountBackLine(close, high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Trendflex using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersTrendflexFast(StockData data, ComputeContext context, int length = 20)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersTrendflex(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Reflex using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersReflexFast(StockData data, ComputeContext context, int length = 20)
    {
        // Both specs name IndicatorName.EhlersReflexIndicator, so they share one arm. The core this
        // one used applied the slope per lag and with the opposite sign, which the batch does not.
        return ComputeEhlersReflexIndicatorFast(data, context, length);
    }

    /// <summary>
    /// Computes Ehlers Correlation Trend Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersCorrelationTrendIndicatorFast(StockData data, ComputeContext context, int length = 20)
    {
        // The batch correlates price against a descending ramp (y = -j). The core this arm used ran the
        // ramp ascending, which flips the sign of every published value.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = data.Count;
        length = Math.Max(length, 1);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            double sx = 0, sy = 0, sxx = 0, sxy = 0, syy = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var x = i >= j ? input[i - j] : 0;
                double y = -j;

                sx += x;
                sy += y;
                sxx += MathHelper.Pow(x, 2);
                sxy += x * y;
                syy += MathHelper.Pow(y, 2);
            }

            var varianceX = (length * sxx) - (sx * sx);
            var varianceY = (length * syy) - (sy * sy);
            output[i] = varianceX > 0 && varianceY > 0 ? ((length * sxy) - (sx * sy)) / MathHelper.Sqrt(varianceX * varianceY) : 0;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Trend Trigger Factor using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeTrendTriggerFactorFast(StockData data, ComputeContext context, int length = 15)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.TrendTriggerFactor(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Trend Detection Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeTrendDetectionIndexFast(StockData data, ComputeContext context, int length1 = 20, int length2 = 40)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.TrendDetectionIndex(inputSpan, buffer.WritableSpan, length1, length2);
        return buffer;
    }

    /// <summary>
    /// Computes Uber Trend Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeUberTrendIndicatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        OscillatorCore.UberTrendIndicator(close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Percentage Trend using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePercentageTrendFast(StockData data, ComputeContext context, int length = 20, double pct = 0.15)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.PercentageTrend(inputSpan, buffer.WritableSpan, length, pct);
        return buffer;
    }

    /// <summary>
    /// Computes Liquid Relative Strength Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeLiquidRelativeStrengthIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        OscillatorCore.LiquidRelativeStrengthIndex(close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Asymmetrical Relative Strength Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAsymmetricalRelativeStrengthIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.AsymmetricalRelativeStrengthIndex(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Average Absolute Error Normalization using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAverageAbsoluteErrorNormalizationFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.AverageAbsoluteErrorNormalization(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Recursive Stochastic using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeRecursiveStochasticFast(StockData data, ComputeContext context, int length = 200, double alpha = 0.1)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.RecursiveStochastic(inputSpan, buffer.WritableSpan, length, alpha);
        return buffer;
    }

    /// <summary>
    /// Computes Shinohara Intensity Ratio A using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeShinoharaIntensityRatioAFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ShinoharaIntensityRatioA(high, low, open, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Shinohara Intensity Ratio B using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeShinoharaIntensityRatioBFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ShinoharaIntensityRatioB(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Range Action Verification Index (RAVI) using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeRangeActionVerificationIndexFast(StockData data, ComputeContext context, int fastLength = 7, int slowLength = 65)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.RangeActionVerificationIndex(close, buffer.WritableSpan, fastLength, slowLength);
        return buffer;
    }

    /// <summary>
    /// Computes Williams Accumulation Distribution using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeWilliamsAccumulationDistributionFast(StockData data, ComputeContext context)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.WilliamsAccumulationDistribution(close, high, low, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Total Power Indicator (bull power output) using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeTotalPowerIndicatorFast(StockData data, ComputeContext context, int length1 = 45, int length2 = 10)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var bullBuffer = context.Rent(data.Count);
        var bearBuffer = context.Rent(data.Count);
        OscillatorCore.TotalPowerIndicator(close, high, low, bullBuffer.WritableSpan, bearBuffer.WritableSpan, length1, length2);
        bearBuffer.Dispose();
        return bullBuffer;
    }

    /// <summary>
    /// Computes TurboTrigger using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeTurboTriggerFast(StockData data, ComputeContext context, int length = 100, double pctMultiplier = 1.0)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.TurboTrigger(close, buffer.WritableSpan, length, pctMultiplier);
        return buffer;
    }

    /// <summary>
    /// Computes TurboScaler using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeTurboScalerFast(StockData data, ComputeContext context, int length = 50, double pctMultiplier = 1.0)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.TurboScaler(close, buffer.WritableSpan, length, pctMultiplier);
        return buffer;
    }

    /// <summary>
    /// Computes TTM Scalper Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeTTMScalperIndicatorFast(StockData data, ComputeContext context)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.TTMScalperIndicator(close, high, low, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Strength of Movement using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeStrengthOfMovementFast(StockData data, ComputeContext context, int length1 = 10, int length2 = 3)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.StrengthOfMovement(close, high, low, buffer.WritableSpan, length1, length2);
        return buffer;
    }

    /// <summary>
    /// Computes Value Chart Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeValueChartIndicatorFast(StockData data, ComputeContext context, int length = 5, int numAtrs = 8)
    {
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ValueChartIndicator(open, high, low, close, buffer.WritableSpan, length, numAtrs);
        return buffer;
    }

    /// <summary>
    /// Computes Sell Gravitation Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSellGravitationIndexFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.SellGravitationIndex(close, high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes TFS Tether Line Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeTFSTetherLineIndicatorFast(StockData data, ComputeContext context, int length = 50)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.TFSTetherLineIndicator(close, high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Simple Cycle Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersSimpleCycleIndicatorFast(StockData data, ComputeContext context, double alpha = 0.07)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersSimpleCycleIndicator(inputSpan, buffer.WritableSpan, alpha);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Fisher Transform using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersFisherTransformFast(StockData data, ComputeContext context, int length = 10)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersFisherTransform(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Voss Predictive Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersVossPredictiveFilterFast(StockData data, ComputeContext context, int length = 20, double predict = 3, double bw = 0.25)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersVossPredictiveFilter(inputSpan, buffer.WritableSpan, length, predict, bw);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Spearman Rank Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersSpearmanRankIndicatorFast(StockData data, ComputeContext context, int length = 20)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersSpearmanRankIndicator(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Correlation Cycle Indicator (Real) using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersCorrelationCycleIndicatorFast(StockData data, ComputeContext context, int length = 20)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var realBuffer = context.Rent(inputList.Count);
        var imagBuffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersCorrelationCycleIndicator(inputSpan, realBuffer.WritableSpan, imagBuffer.WritableSpan, length);
        imagBuffer.Dispose();
        return realBuffer;
    }

    /// <summary>
    /// Computes Ehlers Correlation Angle Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersCorrelationAngleIndicatorFast(StockData data, ComputeContext context, int length = 20)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersCorrelationAngleIndicator(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Truncated BandPass Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersTruncatedBandPassFilterFast(StockData data, ComputeContext context, int length1 = 20, int length2 = 10, double bw = 0.1)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersTruncatedBandPassFilter(inputSpan, buffer.WritableSpan, length1, length2, bw);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Simple Decycler using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersSimpleDecyclerFast(StockData data, ComputeContext context, int length = 125)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersSimpleDecycler(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Even Better Sine Wave Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersEvenBetterSineWaveIndicatorFast(StockData data, ComputeContext context, int length1 = 40, int length2 = 10)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersEvenBetterSineWaveIndicator(inputSpan, buffer.WritableSpan, length1, length2);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Market State Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersMarketStateIndicatorFast(StockData data, ComputeContext context, int length = 20)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersMarketStateIndicator(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Instantaneous Trendline V2 using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersInstantaneousTrendlineV2Fast(StockData data, ComputeContext context, double alpha = 0.07)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersInstantaneousTrendlineV2(inputSpan, buffer.WritableSpan, alpha);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers CyberCycle Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersCyberCycleOscillatorFast(StockData data, ComputeContext context, double alpha = 0.07)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersCyberCycleOscillator(inputSpan, buffer.WritableSpan, alpha);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Band Pass Filter V1 using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersBandPassFilterV1Fast(StockData data, ComputeContext context, int length = 20, double bw = 0.3)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = data.Count;
        length = Math.Max(length, 1);

        var twoPiPrd1 = MathHelper.MinOrMax(0.25 * bw * 2 * Math.PI / length, 0.99, 0.01);
        var beta = Math.Cos(MathHelper.MinOrMax(2 * Math.PI / length, 0.99, 0.01));
        var gamma = 1 / Math.Cos(MathHelper.MinOrMax(2 * Math.PI * bw / length, 0.99, 0.01));
        var alpha1 = gamma - MathHelper.Sqrt(MathHelper.Pow(gamma, 2) - 1);
        var alpha2 = (Math.Cos(twoPiPrd1) + Math.Sin(twoPiPrd1) - 1) / Math.Cos(twoPiPrd1);

        using var highPass = context.Rent(count);
        using var bandPass = context.Rent(count);
        var hp = highPass.WritableSpan;
        var bp = bandPass.WritableSpan;

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        double peak = 0;
        for (var i = 0; i < count; i++)
        {
            var previousValue = i >= 1 ? input[i - 1] : 0;
            var previousHp1 = i >= 1 ? hp[i - 1] : 0;
            var previousHp2 = i >= 2 ? hp[i - 2] : 0;
            var previousBp1 = i >= 1 ? bp[i - 1] : 0;
            var previousBp2 = i >= 2 ? bp[i - 2] : 0;

            hp[i] = ((1 + (alpha2 / 2)) * CalculationsHelper.MinPastValues(i, 1, input[i] - previousValue)) + ((1 - alpha2) * previousHp1);
            bp[i] = i > 2 ? (0.5 * (1 - alpha1) * (hp[i] - previousHp2)) + (beta * (1 + alpha1) * previousBp1) - (alpha1 * previousBp2) : 0;

            peak = Math.Max(0.991 * peak, Math.Abs(bp[i]));
            output[i] = peak != 0 ? bp[i] / peak : 0;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Band Pass Filter V2 using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersBandPassFilterV2Fast(StockData data, ComputeContext context, int length = 20, double bw = 0.3)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersBandPassFilterV2(inputSpan, buffer.WritableSpan, length, bw);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Cycle Band Pass Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersCycleBandPassFilterFast(StockData data, ComputeContext context, int length = 20, double delta = 0.1)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersCycleBandPassFilter(inputSpan, buffer.WritableSpan, length, delta);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Cycle Amplitude using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersCycleAmplitudeFast(StockData data, ComputeContext context, int length = 20, double delta = 0.1)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersCycleAmplitude(inputSpan, buffer.WritableSpan, length, delta);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers HP/LP Roofing Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersHpLpRoofingFilterFast(StockData data, ComputeContext context, int length1 = 48, int length2 = 10)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersHpLpRoofingFilter(inputSpan, buffer.WritableSpan, length1, length2);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Early Onset Trend Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersEarlyOnsetTrendIndicatorFast(StockData data, ComputeContext context, int length1 = 30, int length2 = 100, double k = 0.85)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersEarlyOnsetTrendIndicator(inputSpan, buffer.WritableSpan, length1, length2, k);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Detrended Leading Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersDetrendedLeadingIndicatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var highSpan = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var lowSpan = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.EhlersDetrendedLeadingIndicator(highSpan, lowSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Classic Hilbert Transformer using zero-allocation fast path.
    /// Returns the real component (imaginary available via second buffer).
    /// </summary>
    internal static ComputeBuffer ComputeEhlersClassicHilbertTransformerFast(StockData data, ComputeContext context, int length1 = 48, int length2 = 10)
    {
        // The bound key is Real: the roofing filter normalised by its own running peak. The imaginary
        // component is a separate published series and never reaches this one.
        var count = data.Count;

        using var roofing = ComputeEhlersRoofingFilterV2Fast(data, context, length1, length2);
        var filter = roofing.Span;

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        double peak = 0;
        for (var i = 0; i < count; i++)
        {
            peak = Math.Max(0.991 * peak, Math.Abs(filter[i]));
            output[i] = peak != 0 ? filter[i] / peak : 0;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Zero Mean Roofing Filter using fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersZeroMeanRoofingFilterFast(StockData data, ComputeContext context, int length1 = 48, int length2 = 10)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersZeroMeanRoofingFilter(inputSpan, buffer.WritableSpan, length1, length2);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Super Passband Filter using fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersSuperPassbandFilterFast(StockData data, ComputeContext context, int fastLength = 40, int slowLength = 60, int length1 = 5, int length2 = 50)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersSuperPassbandFilter(inputSpan, buffer.WritableSpan, fastLength, slowLength, length1, length2);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Roofing Filter V2 using fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersRoofingFilterV2Fast(StockData data, ComputeContext context, int upperLength = 80, int lowerLength = 40)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersRoofingFilterV2(inputSpan, buffer.WritableSpan, upperLength, lowerLength);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Impulse Reaction using fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersImpulseReactionFast(StockData data, ComputeContext context, int length1 = 2, int length2 = 20, double q = 0.9)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersImpulseReaction(inputSpan, buffer.WritableSpan, length1, length2, q);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Reverse Exponential Moving Average Indicator V1 using fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersReverseEmaIndicatorV1Fast(StockData data, ComputeContext context, double alpha = 0.1)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersReverseEmaIndicatorV1(inputSpan, buffer.WritableSpan, alpha);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Squelch Indicator using fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersSquelchIndicatorFast(StockData data, ComputeContext context, int length1 = 6, int length2 = 20, int length3 = 40)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersSquelchIndicator(inputSpan, buffer.WritableSpan, length1, length2, length3);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Reverse EMA Indicator V2 using fast path.
    /// </summary>
    /// <summary>
    /// Which of the reverse exponential moving average indicator's two waves an arm has been asked for. The
    /// cycle wave is drawn from the trend wave rather than from price, so it is the second of two passes.
    /// </summary>
    internal enum EhlersReverseEmaWave
    {
        Cycle,
        Trend
    }

    internal static ComputeBuffer ComputeEhlersReverseEmaIndicatorV2Fast(StockData data, ComputeContext context,
        double trendAlpha = 0.05, double cycleAlpha = 0.3, EhlersReverseEmaWave wave = EhlersReverseEmaWave.Cycle)
    {
        // CalculateEhlersReverseExponentialMovingAverageIndicatorV2 runs the V1 indicator at the trend
        // constant and then runs it again at the cycle constant over what that published, because the first
        // pass chains its values onto the bars the second pass reads. The streaming state walks the same two
        // passes in the same order. The core this called is a third calculation that is neither wave.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var count = inputList.Count;

        var trend = context.Rent(count);
        OscillatorCore.EhlersReverseEmaIndicatorV1(SpanCompat.AsReadOnlySpan(inputList), trend.WritableSpan,
            MathHelper.MinOrMax(trendAlpha, 0.99, 0.01));

        if (wave == EhlersReverseEmaWave.Trend)
        {
            return trend;
        }

        using (trend)
        {
            var cycle = context.Rent(count);
            OscillatorCore.EhlersReverseEmaIndicatorV1(trend.Span, cycle.WritableSpan,
                MathHelper.MinOrMax(cycleAlpha, 0.99, 0.01));
            return cycle;
        }
    }

    /// <summary>
    /// Computes Ehlers Stochastic Cyber Cycle using fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersStochasticCyberCycleFast(StockData data, ComputeContext context, int length = 14, double alpha = 0.7)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersStochasticCyberCycle(inputSpan, buffer.WritableSpan, length, alpha);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Center of Gravity Oscillator using fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersCenterofGravityOscillatorFast(StockData data, ComputeContext context, int length = 10)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersCenterofGravityOscillator(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Reflex Indicator using fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersReflexIndicatorFast(StockData data, ComputeContext context, int length = 20)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersReflexIndicator(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Trendflex Indicator using fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersTrendflexIndicatorFast(StockData data, ComputeContext context, int length = 20)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersTrendflexIndicator(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes JMA RSX Clone using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeJmaRsxCloneFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.JmaRsxClone(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Rate of Change using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeRateOfChangeFast(StockData data, ComputeContext context, int length = 12)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.RateOfChange(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Williams Fractals (Up Fractal) using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeWilliamsFractalsFast(StockData data, ComputeContext context, int length = 2)
    {
        var highSpan = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var lowSpan = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        var downBuffer = context.Rent(data.Count);
        try
        {
            OscillatorCore.WilliamsFractals(highSpan, lowSpan, buffer.WritableSpan, downBuffer.WritableSpan, length);
        }
        finally
        {
            downBuffer.Dispose();
        }
        return buffer;
    }

    /// <summary>
    /// Computes Detrended Price Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDetrendedPriceOscillatorFast(StockData data, ComputeContext context, int length = 20,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // CalculateDetrendedPriceOscillator holds the price of half a window ago against an average of
        // whichever type it was given, and reads a bar that has not arrived as zero rather than blanking.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var count = inputList.Count;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var lookback = MathHelper.MinOrMax((int)Math.Ceiling((length / 2.0) + 1));

        using var averageBuffer = context.Rent(count);
        var average = averageBuffer.WritableSpan;
        MovingAverage(data, maType, length, inputSpan, average);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            var prevValue = i >= lookback ? inputSpan[i - lookback] : 0;
            output[i] = prevValue - average[i];
        }

        return buffer;
    }

    /// <summary>
    /// Computes Polarized Fractal Efficiency using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePolarizedFractalEfficiencyFast(StockData data, ComputeContext context, int length = 10, int smoothLength = 5)
    {
        var closeSpan = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.PolarizedFractalEfficiency(closeSpan, buffer.WritableSpan, length, smoothLength);
        return buffer;
    }

    /// <summary>
    /// Computes Schaff Trend Cycle using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSchaffTrendCycleFast(StockData data, ComputeContext context, int cycleLength = 10, int fastLength = 23, int slowLength = 50)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.SchaffTrendCycle(inputSpan, buffer.WritableSpan, cycleLength, fastLength, slowLength);
        return buffer;
    }

    /// <summary>
    /// Computes Smoothed Rate of Change using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSmoothedRateOfChangeFast(StockData data, ComputeContext context, int length = 21,
        int smoothingLength = 13, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        // CalculateSmoothedRateOfChange smooths the chained series first and takes the rate of change of that
        // average, not of price; where the older average is still zero the rate is reported as a flat hundred,
        // which is why the batch opens at 100 and the two core-routine arms this replaced opened at zero.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var count = inputList.Count;
        length = Math.Max(length, 1);

        using var smoothed = context.Rent(count);
        MovingAverage(data, maType, smoothingLength, SpanCompat.AsReadOnlySpan(inputList), smoothed.WritableSpan);
        var ma = smoothed.Span;

        var buffer = context.Rent(count);
        var sroc = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            var previousMa = i >= length ? ma[i - length] : 0;
            sroc[i] = previousMa != 0 ? 100 * (ma[i] - previousMa) / previousMa : 100;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Floor Pivot Point using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeFloorPivotPointFast(StockData data, ComputeContext context)
    {
        var highSpan = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var lowSpan = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var closeSpan = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.FloorPivotPoint(highSpan, lowSpan, closeSpan, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Floor Pivot Point Support Level 1 using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeFloorPivotPointS1Fast(StockData data, ComputeContext context)
    {
        var highSpan = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var lowSpan = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var closeSpan = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.FloorPivotPointS1(highSpan, lowSpan, closeSpan, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Floor Pivot Point Resistance Level 1 using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeFloorPivotPointR1Fast(StockData data, ComputeContext context)
    {
        var highSpan = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var lowSpan = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var closeSpan = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.FloorPivotPointR1(highSpan, lowSpan, closeSpan, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Camarilla Pivot Point using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeCamarillaPivotPointFast(StockData data, ComputeContext context)
    {
        var highSpan = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var lowSpan = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var closeSpan = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.CamarillaPivotPoint(highSpan, lowSpan, closeSpan, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Woodie Pivot Point using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeWoodiePivotPointFast(StockData data, ComputeContext context)
    {
        var highSpan = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var lowSpan = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var closeSpan = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.WoodiePivotPoint(highSpan, lowSpan, closeSpan, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Fibonacci Pivot Point using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeFibonacciPivotPointFast(StockData data, ComputeContext context)
    {
        var highSpan = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var lowSpan = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var closeSpan = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.FibonacciPivotPoint(highSpan, lowSpan, closeSpan, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Demark Pivot Point using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDemarkPivotPointFast(StockData data, ComputeContext context)
    {
        var highSpan = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var lowSpan = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var openSpan = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var closeSpan = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.DemarkPivotPoint(highSpan, lowSpan, openSpan, closeSpan, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Linear Channel Middle using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeLinearChannelMiddleFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        TrendCore.LinearChannelMiddle(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Price Channel Upper using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePriceChannelUpperFast(StockData data, ComputeContext context, int length = 20)
    {
        var highSpan = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var buffer = context.Rent(data.Count);
        TrendCore.PriceChannelUpper(highSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Price Channel Lower using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePriceChannelLowerFast(StockData data, ComputeContext context, int length = 20)
    {
        var lowSpan = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        TrendCore.PriceChannelLower(lowSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Donchian Channel Upper using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDonchianChannelUpperFast(StockData data, ComputeContext context, int length = 20)
    {
        var highSpan = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var buffer = context.Rent(data.Count);
        TrendCore.DonchianChannelUpper(highSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Donchian Channel Lower using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDonchianChannelLowerFast(StockData data, ComputeContext context, int length = 20)
    {
        var lowSpan = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        TrendCore.DonchianChannelLower(lowSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Three HMA (3HMA) using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeThreeHmaFast(StockData data, ComputeContext context, int length = 50)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.ThreeHma(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Adaptive Autonomous Recursive Trailing Stop using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAdaptiveAutonomousRecursiveTrailingStopFast(StockData data, ComputeContext context, int length = 14, double lambda = 1)
    {
        var closeSpan = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var highSpan = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var lowSpan = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.AdaptiveAutonomousRecursiveTrailingStop(closeSpan, highSpan, lowSpan, buffer.WritableSpan, length, lambda);
        return buffer;
    }

    /// <summary>
    /// Computes Adaptive Trailing Stop using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAdaptiveTrailingStopFast(StockData data, ComputeContext context, int length = 14, double multiplier = 2)
    {
        var closeSpan = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var highSpan = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var lowSpan = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.AdaptiveTrailingStop(closeSpan, highSpan, lowSpan, buffer.WritableSpan, length, multiplier);
        return buffer;
    }

    /// <summary>
    /// Computes Average True Range Trailing Stops using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAverageTrueRangeTrailingStopsFast(StockData data, ComputeContext context,
        int length2 = 21, double factor = 3, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 63)
    {
        // CalculateAverageTrueRangeTrailingStops ratchets a stop towards the price while a longer average
        // says which side of it we are on, and both the average and the true range take the type it was
        // given. The stop only ever moves in the direction of the trend, so it has to be carried forward
        // bar by bar rather than read out of a window.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var count = inputList.Count;
        var input = SpanCompat.AsReadOnlySpan(inputList);

        using var atr = ComputeAtrFast(data, context, length2, maType);
        using var trendBuffer = context.Rent(count);
        var trend = trendBuffer.WritableSpan;
        MovingAverage(data, maType, length1, input, trend);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            var currentValue = input[i];

            // The first bar has no stop to ratchet against, so it starts at the price itself.
            var prevStop = i >= 1 ? output[i - 1] : currentValue;
            var band = factor * atr.Span[i];

            output[i] = currentValue > trend[i]
                ? Math.Max(currentValue - band, prevStop)
                : Math.Min(currentValue + band, prevStop);
        }

        return buffer;
    }

    /// <summary>
    /// Computes Welles Wilder Summation using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeWellesWilderSummationFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.WellesWilderSummation(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Damping Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDampingIndexFast(StockData data, ComputeContext context, int length = 5,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // CalculateDampingIndex compares the average bar range one bar back with the average range six bars
        // before that, so a market whose range has been shrinking reads below one. The average takes whichever
        // type the indicator was given, and the price itself only reaches its signal.
        var (_, highList, lowList, _, _) = CalculationsHelper.GetInputValuesList(data);
        var count = highList.Count;

        using var rangeBuffer = context.Rent(count);
        var range = rangeBuffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            range[i] = highList[i] - lowList[i];
        }

        using var averageRangeBuffer = context.Rent(count);
        var averageRange = averageRangeBuffer.WritableSpan;
        MovingAverage(data, maType, length, range, averageRange);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            // Both readings are taken from before this bar, as the batch does.
            var previous = i >= 1 ? averageRange[i - 1] : 0;
            var older = i >= 6 ? averageRange[i - 6] : 0;
            output[i] = older != 0 ? previous / older : 0;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Didi Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDidiIndexFast(StockData data, ComputeContext context, int length1 = 3,
        int length2 = 8, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // Curta, the line this spec is bound to, is the short average measured against the medium one, so
        // only those two lengths reach it. The long average is a separate line of the same indicator.
        var (inputList, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        var count = inputList.Count;
        var input = SpanCompat.AsReadOnlySpan(inputList);

        using var shortBuffer = context.Rent(count);
        using var mediumBuffer = context.Rent(count);
        var shortAverage = shortBuffer.WritableSpan;
        var mediumAverage = mediumBuffer.WritableSpan;
        MovingAverage(data, maType, length1, input, shortAverage);
        MovingAverage(data, maType, length2, input, mediumAverage);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            output[i] = mediumAverage[i] != 0 ? shortAverage[i] / mediumAverage[i] : 0;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Vertical Horizontal Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVerticalHorizontalFilterFast(StockData data, ComputeContext context, int length = 28)
    {
        var closeSpan = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.VerticalHorizontalFilter(closeSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Linear Regression Slope using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeLinearRegressionSlopeFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        TrendCore.LinearRegressionSlope(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Linear Regression Intercept using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeLinearRegressionInterceptFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        TrendCore.LinearRegressionIntercept(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Batch 5 - Additional Indicators with Core Methods

    /// <summary>
    /// Computes Absolute Price Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAbsolutePriceOscillatorFast(StockData data, ComputeContext context, int fastLength = 10, int slowLength = 20)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.AbsolutePriceOscillator(inputSpan, buffer.WritableSpan, fastLength, slowLength);
        return buffer;
    }

    /// <summary>
    /// Computes Accumulation Distribution Line using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAccumulationDistributionLineFast(StockData data, ComputeContext context)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.AccumulationDistributionLine(high, low, close, volume, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Adaptive Exponential Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAdaptiveExponentialMovingAverageFast(StockData data, ComputeContext context,
        int length = 10, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // CalculateAdaptiveExponentialMovingAverage seeds itself with an average of the given type for the
        // first length bars and only then starts following the price, at a rate that widens as the bar sits
        // further from the middle of its high-low range.
        var (inputList, highList, lowList, _, _) = CalculationsHelper.GetInputValuesList(data);
        var count = inputList.Count;
        var input = SpanCompat.AsReadOnlySpan(inputList);

        using var highestBuffer = context.Rent(count);
        using var lowestBuffer = context.Rent(count);
        var highest = highestBuffer.WritableSpan;
        var lowest = lowestBuffer.WritableSpan;
        HighestAndLowest(highList, lowList, highest, lowest, length);

        using var seedBuffer = context.Rent(count);
        var seed = seedBuffer.WritableSpan;
        MovingAverage(data, maType, length, input, seed);

        var baseRate = (double)2 / (length + 1);
        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            var currentValue = input[i];
            var range = highest[i] - lowest[i];
            var offset = range != 0
                ? MathHelper.MinOrMax(Math.Abs((2 * currentValue) - lowest[i] - highest[i]) / range, 1, 0)
                : 0;
            var rate = baseRate * (1 + offset);

            // Until the seed window has filled the average IS the seed, so there is nothing to follow yet.
            var previous = i >= 1 ? output[i - 1] : currentValue;
            output[i] = i <= length ? seed[i] : previous + (rate * (currentValue - previous));
        }

        return buffer;
    }

    /// <summary>
    /// Computes Average Directional Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAverageDirectionalIndexFast(StockData data, ComputeContext context, int length = 14,
        MovingAvgType maType = MovingAvgType.WildersSmoothingMethod)
    {
        // Both specs name IndicatorName.AverageDirectionalIndex, so there is one answer to give.
        return ComputeAdxFast(data, context, length, maType);
    }

    /// <summary>
    /// Computes Average True Range using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAverageTrueRangeFast(StockData data, ComputeContext context, int length = 14,
        MovingAvgType maType = MovingAvgType.WildersSmoothingMethod)
    {
        // Both specs name IndicatorName.AverageTrueRange, so there is one answer to give.
        return ComputeAtrFast(data, context, length, maType);
    }

    /// <summary>
    /// Computes Chande Momentum Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChandeMomentumOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.ChandeMomentumOscillator(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ease of Movement using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEaseOfMovementFast(StockData data, ComputeContext context,
        double divisor = 1000000)
    {
        // CalculateEaseOfMovement publishes the raw series. Length and MaType only smooth its Signal line,
        // which is a different output from the one this arm is bound to, so neither reaches this series.
        var (_, highList, lowList, _, volumeList) = CalculationsHelper.GetInputValuesList(data);
        var high = SpanCompat.AsReadOnlySpan(highList);
        var low = SpanCompat.AsReadOnlySpan(lowList);
        var volume = SpanCompat.AsReadOnlySpan(volumeList);
        var count = highList.Count;

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        double previousHalfRange = 0;
        double previousMidpointMove = 0;

        for (var i = 0; i < count; i++)
        {
            var range = high[i] - low[i];
            var halfRange = range * 0.5;
            var boxRatio = range != 0 ? volume[i] / range : 0;
            var midpointMove = halfRange - previousHalfRange;

            output[i] = boxRatio != 0 ? divisor * ((midpointMove - previousMidpointMove) / boxRatio) : 0;

            previousHalfRange = halfRange;
            previousMidpointMove = midpointMove;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Hull Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeHullMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.HullMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Klinger Volume Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeKlingerVolumeOscillatorFast(StockData data, ComputeContext context, int fastLength = 34, int slowLength = 55)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.KlingerVolumeOscillator(high, low, close, volume, buffer.WritableSpan, fastLength, slowLength);
        return buffer;
    }

    /// <summary>
    /// Computes Know Sure Thing using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeKnowSureThingFast(StockData data, ComputeContext context,
        int roc1 = 10, int roc2 = 15, int roc3 = 20, int roc4 = 30,
        int sma1 = 10, int sma2 = 10, int sma3 = 10, int sma4 = 15)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.KnowSureThing(inputSpan, buffer.WritableSpan, roc1, roc2, roc3, roc4, sma1, sma2, sma3, sma4);
        return buffer;
    }

    /// <summary>
    /// Computes Negative Volume Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeNegativeVolumeIndexFast(StockData data, ComputeContext context)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var close = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            close[i] = (double)tickerList[i].Close;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.NegativeVolumeIndex(close, volume, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes On Balance Volume using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeOnBalanceVolumeFast(StockData data, ComputeContext context)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var close = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            close[i] = (double)tickerList[i].Close;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.OnBalanceVolume(close, volume, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Percentage Price Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePercentagePriceOscillatorFast(StockData data, ComputeContext context, int fastLength = 12, int slowLength = 26)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.PercentagePriceOscillator(inputSpan, buffer.WritableSpan, fastLength, slowLength);
        return buffer;
    }

    /// <summary>
    /// Computes Percentage Volume Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePercentageVolumeOscillatorFast(StockData data, ComputeContext context, int fastLength = 12, int slowLength = 26)
    {
        var volumeSpan = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        OscillatorCore.PercentageVolumeOscillator(volumeSpan, buffer.WritableSpan, fastLength, slowLength);
        return buffer;
    }

    /// <summary>
    /// Computes Positive Volume Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePositiveVolumeIndexFast(StockData data, ComputeContext context)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var close = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            close[i] = (double)tickerList[i].Close;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.PositiveVolumeIndex(close, volume, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Price Momentum Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePriceMomentumOscillatorFast(StockData data, ComputeContext context, int firstLength = 35, int secondLength = 20)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.PriceMomentumOscillator(inputSpan, buffer.WritableSpan, firstLength, secondLength);
        return buffer;
    }

    /// <summary>
    /// Computes Price Volume Trend using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePriceVolumeTrendFast(StockData data, ComputeContext context)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var close = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            close[i] = (double)tickerList[i].Close;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.PriceVolumeTrend(close, volume, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Triangular Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeTriangularMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.TriangularMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes True Strength Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeTrueStrengthIndexFast(StockData data, ComputeContext context, int longLength = 25, int shortLength = 13)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.TrueStrengthIndex(inputSpan, buffer.WritableSpan, longLength, shortLength);
        return buffer;
    }

    #endregion

    #region Batch 6 - Additional Oscillators

    /// <summary>
    /// Computes Chande Quick Stick using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChandeQuickStickFast(StockData data, ComputeContext context, int length = 14,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // CalculateChandeQuickStick averages how far each bar closed from where it opened, and that average
        // takes whichever type it was given. The high and low never enter it.
        var (inputList, _, _, openList, _) = CalculationsHelper.GetInputValuesList(data);
        var count = inputList.Count;

        using var openCloseBuffer = context.Rent(count);
        var openClose = openCloseBuffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            openClose[i] = inputList[i] - openList[i];
        }

        var buffer = context.Rent(count);
        MovingAverage(data, maType, length, openClose, buffer.WritableSpan);

        return buffer;
    }

    /// <summary>
    /// Computes Delta Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDeltaMovingAverageFast(StockData data, ComputeContext context,
        int length2 = 5)
    {
        // CalculateDeltaMovingAverage measures how far the close has travelled since the open length2 bars
        // back. The average it also takes smooths that into its signal and histogram, other series.
        var (inputList, _, _, openList, _) = CalculationsHelper.GetInputValuesList(data);
        var count = inputList.Count;

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            // Bars before the start of the series count as zero, which is what the batch does.
            var previousOpen = i >= length2 ? openList[i - length2] : 0;
            output[i] = inputList[i] - previousOpen;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Folded RSI using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeFoldedRsiFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.FoldedRelativeStrengthIndex(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Enhanced Williams %R using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEnhancedWilliamsRFast(StockData data, ComputeContext context, int length = 14, int smoothLength = 3)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
        }
        var buffer = context.Rent(count);
        OscillatorCore.EnhancedWilliamsR(high, low, close, buffer.WritableSpan, length, smoothLength);
        return buffer;
    }

    /// <summary>
    /// Computes Connors RSI using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeConnorsRsiFast(StockData data, ComputeContext context, int rsiLength = 3, int streakLength = 2, int rankLength = 100)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.ConnorsRelativeStrengthIndex(inputSpan, buffer.WritableSpan, rsiLength, streakLength, rankLength);
        return buffer;
    }

    /// <summary>
    /// Computes Stochastic RSI using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeStochasticRsiFast(StockData data, ComputeContext context, int length = 14,
        int smoothLength1 = 3, int smoothLength2 = 3, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int? stochLength = null)
    {
        // CalculateStochasticRelativeStrengthIndex ranges the relative strength index rather than price, then
        // publishes the first smoothing of that stochastic - FastD - as its primary series; the second
        // smoothing is the Signal key, so smoothLength2 does not reach this output. The three specs bound to
        // this indicator each ran a different core routine over the close, and none of them agreed with the
        // batch or with one another.
        _ = smoothLength2;

        var count = data.Count;

        using var relativeStrength = ComputeRsiFast(data, context, length, maType);

        using var stochastic = context.Rent(count);
        StochasticFastK(data, context, relativeStrength.Span, Math.Max(1, stochLength ?? length), stochastic.WritableSpan);

        var buffer = context.Rent(count);
        MovingAverage(data, maType, smoothLength1, stochastic.Span, buffer.WritableSpan);

        return buffer;
    }

    /// <summary>
    /// Computes Stochastic Momentum Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeStochasticMomentumIndexFast(StockData data, ComputeContext context, int length = 13, int smoothLength1 = 25, int smoothLength2 = 2)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
        }
        var buffer = context.Rent(count);
        OscillatorCore.StochasticMomentumIndex(high, low, close, buffer.WritableSpan, length, smoothLength1, smoothLength2);
        return buffer;
    }

    /// <summary>
    /// Computes Premier Stochastic Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePremierStochasticFast(StockData data, ComputeContext context, int length = 8, int smoothLength = 25)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
        }
        var buffer = context.Rent(count);
        OscillatorCore.PremierStochastic(high, low, close, buffer.WritableSpan, length, smoothLength);
        return buffer;
    }

    /// <summary>
    /// Computes Momentum Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMomentumOscillatorFast(StockData data, ComputeContext context, int length = 10, int smoothLength = 3)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        // Momentum oscillator is momentum with smoothing
        var pool = ArrayPool<double>.Shared;
        var momArray = pool.Rent(inputList.Count);
        try
        {
            var mom = momArray.AsSpan(0, inputList.Count);
            OscillatorCore.Momentum(inputSpan, mom, length);
            MovingAverageCore.WeightedMovingAverage(mom, buffer.WritableSpan, smoothLength);
        }
        finally
        {
            pool.Return(momArray);
        }
        return buffer;
    }

    /// <summary>
    /// Computes Stochastic Oscillator using zero-allocation fast path.
    /// </summary>
    /// <summary>
    /// Writes the high and low series that <paramref name="values"/> is measured against into
    /// <paramref name="highs"/> and <paramref name="lows"/>, by the same per-bar rule
    /// <see cref="CalculationsHelper.GetCustomRangeLists"/> applies.
    /// </summary>
    /// <remarks>
    /// A chained series that sits inside its bar keeps that bar's own range. One on a scale of its own - an
    /// oscillator running nought to a hundred, say - is given the range it made itself, between this bar and
    /// the last. Measuring such a series against the price bars it never touched is what that rule exists to
    /// prevent, and any arm that ranges a chained series has to follow it to agree with the batch.
    /// </remarks>
    private static void CustomRange(StockData data, ReadOnlySpan<double> values, Span<double> highs, Span<double> lows)
    {
        var barHighs = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var barLows = SpanCompat.AsReadOnlySpan(data.LowPrices);

        for (var i = 0; i < values.Length; i++)
        {
            var value = values[i];
            var high = i < barHighs.Length ? barHighs[i] : value;
            var low = i < barLows.Length ? barLows[i] : value;

            if (CalculationsHelper.IsWithinBarRange(value, low, high))
            {
                highs[i] = high;
                lows[i] = low;
            }
            else
            {
                var previousValue = i > 0 ? values[i - 1] : value;
                highs[i] = Math.Max(previousValue, value);
                lows[i] = Math.Min(previousValue, value);
            }
        }
    }

    /// <summary>
    /// Writes the raw stochastic of <paramref name="values"/> over <paramref name="length"/> bars into
    /// <paramref name="output"/>: where the series sits in the range it has covered, as a percentage.
    /// </summary>
    private static void StochasticFastK(StockData data, ComputeContext context, ReadOnlySpan<double> values, int length,
        Span<double> output)
    {
        var count = values.Length;
        length = Math.Max(length, 1);

        using var highSeries = context.Rent(count);
        using var lowSeries = context.Rent(count);
        CustomRange(data, values, highSeries.WritableSpan, lowSeries.WritableSpan);
        var highs = highSeries.Span;
        var lows = lowSeries.Span;

        var highWindow = new RollingMinMax(length);
        var lowWindow = new RollingMinMax(length);
        for (var i = 0; i < count; i++)
        {
            highWindow.Add(highs[i]);
            lowWindow.Add(lows[i]);

            var highestHigh = highWindow.Max;
            var lowestLow = lowWindow.Min;
            var range = highestHigh - lowestLow;
            output[i] = range != 0 ? MathHelper.MinOrMax((values[i] - lowestLow) / range * 100, 100, 0) : 0;
        }
    }

    internal static ComputeBuffer ComputeStochasticOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        // CalculateStochasticOscillator publishes the raw stochastic as its primary series - the smoothed
        // FastD and SlowD are separate keys - so the smoothing lengths never reach this output. The arm this
        // replaced rebuilt the price spans from the ticker list, ignored the chained series and then smoothed
        // on top, so it was the wrong series computed from the wrong input.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var buffer = context.Rent(inputList.Count);
        StochasticFastK(data, context, SpanCompat.AsReadOnlySpan(inputList), length, buffer.WritableSpan);

        return buffer;
    }

    /// <summary>
    /// Computes Stochastic Fast Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeStochasticFastFast(StockData data, ComputeContext context, int length = 14, int smoothLength = 3)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
        }
        var buffer = context.Rent(count);
        var pool = ArrayPool<double>.Shared;
        var kArray = pool.Rent(count);
        try
        {
            var k = kArray.AsSpan(0, count);
            OscillatorCore.StochasticK(high, low, close, k, length);
            MovingAverageCore.ExponentialMovingAverage(k, buffer.WritableSpan, smoothLength);
        }
        finally
        {
            pool.Return(kArray);
        }
        return buffer;
    }

    /// <summary>
    /// Computes Keltner Channel Middle using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeKeltnerMiddleFast(StockData data, ComputeContext context, int length = 20)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        TrendCore.KeltnerChannelMiddle(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes 1LC Least Squares Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeOneLCLeastSquaresFast(StockData data, ComputeContext context, int length = 32)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.OneLCLeastSquaresMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Adaptive RSI using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAdaptiveRsiFast(StockData data, ComputeContext context, int length = 14,
        MovingAvgType maType = MovingAvgType.WildersSmoothingMethod)
    {
        // CalculateAdaptiveRelativeStrengthIndex is an exponential average of the price whose weight is how
        // far the relative strength index has travelled from its midpoint, so it needs that index itself
        // rather than a pair of lengths the batch indicator never had.
        var (inputList, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        var count = inputList.Count;
        var input = SpanCompat.AsReadOnlySpan(inputList);

        using var rsi = ComputeRsiFast(data, context, length, maType);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            var alpha = 2 * Math.Abs((rsi.Span[i] / 100) - 0.5);

            // There is no earlier value to carry on the first bar.
            var previous = i >= 1 ? output[i - 1] : 0;
            output[i] = (alpha * input[i]) + ((1 - alpha) * previous);
        }

        return buffer;
    }

    /// <summary>
    /// Computes Bollinger Bands ATR using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeBollingerBandsAvgTrueRangeFast(StockData data, ComputeContext context,
        int atrLength = 22, int length = 55, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        double stdDevMult = 2)
    {
        // CalculateBollingerBandsAvgTrueRange measures the average true range against the width of the
        // Bollinger band, and both the band and the range take whichever average they were given. The bands
        // are rebuilt here rather than differenced as twice the deviation so that the arm rounds exactly the
        // way the batch indicator does.
        var (inputList, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        var count = inputList.Count;
        var input = SpanCompat.AsReadOnlySpan(inputList);

        using var basisBuffer = context.Rent(count);
        using var deviationBuffer = context.Rent(count);
        var basis = basisBuffer.WritableSpan;
        var deviation = deviationBuffer.WritableSpan;
        MovingAverage(data, maType, length, input, basis);
        VolatilityCore.StandardDeviation(input, deviation, Math.Max(1, length));

        using var atr = ComputeAtrFast(data, context, atrLength, maType);
        var trueRange = atr.Span;

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        for (var i = 0; i < count; i++)
        {
            var upperBand = basis[i] + (deviation[i] * stdDevMult);
            var lowerBand = basis[i] - (deviation[i] * stdDevMult);
            var bbDiff = upperBand - lowerBand;
            output[i] = bbDiff != 0 ? trueRange[i] / bbDiff : 0;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Chande Momentum Oscillator Signal using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChandeMomentumOscillatorSignalFast(StockData data, ComputeContext context,
        int length = 14, int signalLength = 3,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        // The signal line CalculateChandeMomentumOscillator publishes is its own reading smoothed over
        // signalLength with whichever average it was given, so the arm smooths the oscillator arm rather than
        // computing a second one.
        using var oscillator = ComputeChandeMomentumOscillatorFast(data, context, length);

        var buffer = context.Rent(oscillator.Span.Length);
        MovingAverage(data, maType, signalLength, oscillator.Span, buffer.WritableSpan);

        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Roofing Filter V1 using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersRoofingFilterV1Fast(StockData data, ComputeContext context, int length1 = 48, int length2 = 10,
        MovingAvgType maType = MovingAvgType.Ehlers2PoleSuperSmootherFilterV1)
    {
        return EhlersRoofingFilterV1Core(data, context, length1, length2, maType);
    }

    /// <summary>
    /// Computes Ehlers Spearman Rank Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersSpearmanRankFast(StockData data, ComputeContext context, int length = 20)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersSpearmanRankIndicator(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Tillson T3 Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeTillsonT3Fast(StockData data, ComputeContext context, int length = 5, double vFactor = 0.7)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.T3MovingAverage(inputSpan, buffer.WritableSpan, length, vFactor);
        return buffer;
    }

    /// <summary>
    /// Computes the shared Ehlers window filter: the moving average of the close-open derivative.
    /// </summary>
    private static ComputeBuffer EhlersWindowFilter(StockData data, ComputeContext context, int length, MovingAvgType maType)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var count = data.Count;

        using var derivative = context.Rent(count);
        var deriv = derivative.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            deriv[i] = input[i] - open[i];
        }

        var buffer = context.Rent(count);
        MovingAverage(data, maType, Math.Max(length, 1), derivative.Span, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Hamming Window Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersHammingWindowFast(StockData data, ComputeContext context, int length = 20,
        MovingAvgType maType = MovingAvgType.EhlersHammingMovingAverage)
    {
        return EhlersWindowFilter(data, context, length, maType);
    }

    /// <summary>
    /// Computes Ehlers Hann Window Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersHannWindowFast(StockData data, ComputeContext context, int length = 20,
        MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage)
    {
        return EhlersWindowFilter(data, context, length, maType);
    }

    /// <summary>
    /// Computes Ehlers Triangle Window Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersTriangleWindowFast(StockData data, ComputeContext context, int length = 20,
        MovingAvgType maType = MovingAvgType.EhlersTriangleMovingAverage)
    {
        return EhlersWindowFilter(data, context, length, maType);
    }

    /// <summary>
    /// Computes Ehlers Impulse Response using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersImpulseResponseFast(StockData data, ComputeContext context, int length = 20, double bw = 1,
        MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = data.Count;
        length = Math.Max(length, 1);

        var hannLength = MathHelper.MinOrMax((int)Math.Ceiling(length / 1.4));
        var l1 = Math.Cos(MathHelper.MinOrMax(2 * Math.PI / length, 0.99, 0.01));
        var g1 = Math.Cos(MathHelper.MinOrMax(bw * 2 * Math.PI / length, 0.99, 0.01));
        var s1 = (1 / g1) - MathHelper.Sqrt((1 / MathHelper.Pow(g1, 2)) - 1);

        using var bandPass = context.Rent(count);
        var bp = bandPass.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            var previousValue = i >= 2 ? input[i - 2] : 0;
            var previousBp1 = i >= 1 ? bp[i - 1] : 0;
            var previousBp2 = i >= 2 ? bp[i - 2] : 0;

            bp[i] = i < 3 ? 0 : (0.5 * (1 - s1) * (input[i] - previousValue)) + (l1 * (1 + s1) * previousBp1) - (s1 * previousBp2);
        }

        var buffer = context.Rent(count);
        MovingAverage(data, maType, hannLength, bandPass.Span, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Modified Stochastic Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersModifiedStochasticFast(StockData data, ComputeContext context, int length1 = 48, int length2 = 10, int length3 = 20)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
        }
        var buffer = context.Rent(count);
        // Use EhlersStochastic as a close approximation
        MovingAverageCore.EhlersStochastic(close, buffer.WritableSpan, length3);
        return buffer;
    }

    /// <summary>
    /// Computes Variable Index Dynamic Average (VIDYA) using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeVariableIndexDynamicAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.EhlersVariableIndexDynamicAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Batch 12 - New Core Methods for Previously Unimplemented Indicators

    /// <summary>
    /// Computes Ehlers Simple Deriv Indicator using zero-allocation fast path.
    /// Returns the smoothed z3 oscillator (signal line).
    /// </summary>
    internal static ComputeBuffer ComputeEhlersSimpleDerivIndicatorFast(StockData data, ComputeContext context, int length = 2)
    {
        // The batch publishes the raw z3 sum of the last four derivatives; its moving average of that
        // series only ever reaches the Signal line.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = data.Count;
        length = Math.Max(length, 1);

        using var derivatives = context.Rent(count);
        var deriv = derivatives.WritableSpan;

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            var previousValue = i >= length ? input[i - length] : 0;
            deriv[i] = CalculationsHelper.MinPastValues(i, length, input[i] - previousValue);
            output[i] = deriv[i] + (i >= 1 ? deriv[i - 1] : 0) + (i >= 2 ? deriv[i - 2] : 0) + (i >= 3 ? deriv[i - 3] : 0);
        }

        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Simple Clip Indicator using zero-allocation fast path.
    /// Returns the raw z3 oscillator, which is the series the batch publishes.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersSimpleClipIndicatorFast(StockData data, ComputeContext context, int length1 = 2, int length3 = 50)
    {
        // The batch publishes the raw z3 sum of the last four clipped derivatives; its moving average
        // of that series only ever reaches the Signal line.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = data.Count;
        length1 = Math.Max(length1, 1);
        length3 = Math.Max(length3, 1);

        using var derivatives = context.Rent(count);
        using var clips = context.Rent(count);
        var deriv = derivatives.WritableSpan;
        var clip = clips.WritableSpan;

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            var previousValue = i >= length1 ? input[i - length1] : 0;
            deriv[i] = CalculationsHelper.MinPastValues(i, length1, input[i] - previousValue);

            double rms = 0;
            for (var j = 0; j < length3; j++)
            {
                var previousDeriv = i >= j ? deriv[i - j] : 0;
                rms += MathHelper.Pow(previousDeriv, 2);
            }

            clip[i] = rms != 0 ? MathHelper.MinOrMax(2 * deriv[i] / MathHelper.Sqrt(rms / length3), 1, -1) : 0;
            output[i] = clip[i] + (i >= 1 ? clip[i - 1] : 0) + (i >= 2 ? clip[i - 2] : 0) + (i >= 3 ? clip[i - 3] : 0);
        }

        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Relative Vigor Index with signal line.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersRelativeVigorIndexFast(StockData data, ComputeContext context, int length = 10, int signalLength = 4, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var count = data.Count;
        var openSpan = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var highSpan = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var lowSpan = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var closeSpan = SpanCompat.AsReadOnlySpan(data.ClosePrices);

        var pool = ArrayPool<double>.Shared;
        var rviArray = pool.Rent(count);
        var rviSmaArray = pool.Rent(count);
        try
        {
            var rviSpan = rviArray.AsSpan(0, count);
            var rviSmaSpan = rviSmaArray.AsSpan(0, count);

            // Compute raw RVI values
            OscillatorCore.EhlersRelativeVigorIndex(openSpan, highSpan, lowSpan, closeSpan, rviSpan);
            ReadOnlySpan<double> rviReadOnly = rviSpan;

            // First MA smoothing (length)
            switch (maType)
            {
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(rviReadOnly, rviSmaSpan, length);
                    break;
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(rviReadOnly, rviSmaSpan, length);
                    break;
                case MovingAvgType.WeightedMovingAverage:
                    MovingAverageCore.WeightedMovingAverage(rviReadOnly, rviSmaSpan, length);
                    break;
                default:
                    MovingAverageCore.SimpleMovingAverage(rviReadOnly, rviSmaSpan, length);
                    break;
            }

            ReadOnlySpan<double> rviSmaReadOnly = rviSmaSpan;
            var buffer = context.Rent(count);

            // Second MA for signal line (signalLength)
            switch (maType)
            {
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(rviSmaReadOnly, buffer.WritableSpan, signalLength);
                    break;
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(rviSmaReadOnly, buffer.WritableSpan, signalLength);
                    break;
                case MovingAvgType.WeightedMovingAverage:
                    MovingAverageCore.WeightedMovingAverage(rviSmaReadOnly, buffer.WritableSpan, signalLength);
                    break;
                default:
                    MovingAverageCore.SimpleMovingAverage(rviSmaReadOnly, buffer.WritableSpan, signalLength);
                    break;
            }

            return buffer;
        }
        finally
        {
            pool.Return(rviArray);
            pool.Return(rviSmaArray);
        }
    }

    /// <summary>
    /// Computes Ehlers Moving Average Difference Indicator.
    /// MAD = 100 * (fastMA - slowMA) / slowMA
    /// </summary>
    internal static ComputeBuffer ComputeEhlersMovingAverageDifferenceFast(StockData data, ComputeContext context, int fastLength = 8, int slowLength = 23, MovingAvgType maType = MovingAvgType.WeightedMovingAverage)
    {
        var count = data.Count;
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var pool = ArrayPool<double>.Shared;
        var fastMaArray = pool.Rent(count);
        var slowMaArray = pool.Rent(count);
        try
        {
            var fastMaSpan = fastMaArray.AsSpan(0, count);
            var slowMaSpan = slowMaArray.AsSpan(0, count);

            // Compute fast and slow MAs
            switch (maType)
            {
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(inputSpan, fastMaSpan, fastLength);
                    MovingAverageCore.SimpleMovingAverage(inputSpan, slowMaSpan, slowLength);
                    break;
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(inputSpan, fastMaSpan, fastLength);
                    MovingAverageCore.ExponentialMovingAverage(inputSpan, slowMaSpan, slowLength);
                    break;
                case MovingAvgType.WeightedMovingAverage:
                    MovingAverageCore.WeightedMovingAverage(inputSpan, fastMaSpan, fastLength);
                    MovingAverageCore.WeightedMovingAverage(inputSpan, slowMaSpan, slowLength);
                    break;
                default:
                    MovingAverageCore.WeightedMovingAverage(inputSpan, fastMaSpan, fastLength);
                    MovingAverageCore.WeightedMovingAverage(inputSpan, slowMaSpan, slowLength);
                    break;
            }

            ReadOnlySpan<double> fastMaReadOnly = fastMaSpan;
            ReadOnlySpan<double> slowMaReadOnly = slowMaSpan;
            var buffer = context.Rent(count);

            // Compute MAD = 100 * (fastMA - slowMA) / slowMA
            OscillatorCore.MovingAverageDifference(fastMaReadOnly, slowMaReadOnly, buffer.WritableSpan);

            return buffer;
        }
        finally
        {
            pool.Return(fastMaArray);
            pool.Return(slowMaArray);
        }
    }

    /// <summary>
    /// Computes Elder Market Thermometer with signal line.
    /// </summary>
    internal static ComputeBuffer ComputeElderMarketThermometerFast(StockData data, ComputeContext context, int length = 22, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var count = data.Count;
        var highSpan = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var lowSpan = SpanCompat.AsReadOnlySpan(data.LowPrices);

        var pool = ArrayPool<double>.Shared;
        var emtArray = pool.Rent(count);
        try
        {
            var emtSpan = emtArray.AsSpan(0, count);
            OscillatorCore.ElderMarketThermometer(highSpan, lowSpan, emtSpan);
            ReadOnlySpan<double> emtReadOnly = emtSpan;

            var buffer = context.Rent(count);

            switch (maType)
            {
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(emtReadOnly, buffer.WritableSpan, length);
                    break;
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(emtReadOnly, buffer.WritableSpan, length);
                    break;
                case MovingAvgType.WeightedMovingAverage:
                    MovingAverageCore.WeightedMovingAverage(emtReadOnly, buffer.WritableSpan, length);
                    break;
                case MovingAvgType.DoubleExponentialMovingAverage:
                    MovingAverageCore.DoubleExponentialMovingAverage(emtReadOnly, buffer.WritableSpan, length);
                    break;
                case MovingAvgType.TripleExponentialMovingAverage:
                    MovingAverageCore.TripleExponentialMovingAverage(emtReadOnly, buffer.WritableSpan, length);
                    break;
                default:
                    // Fallback to EMA for unsupported types
                    MovingAverageCore.ExponentialMovingAverage(emtReadOnly, buffer.WritableSpan, length);
                    break;
            }

            return buffer;
        }
        finally
        {
            pool.Return(emtArray);
        }
    }

    /// <summary>
    /// Computes DEMA 2 Lines indicator (fast DEMA line).
    /// Returns the fast DEMA line for crossover signals.
    /// </summary>
    internal static ComputeBuffer ComputeDema2LinesFast(StockData data, ComputeContext context, int fastLength = 10, int slowLength = 40, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var count = data.Count;
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(count);

        // Compute fast DEMA (double smoothed)
        switch (maType)
        {
            case MovingAvgType.SimpleMovingAverage:
                {
                    var pool = ArrayPool<double>.Shared;
                    var tempArray = pool.Rent(count);
                    try
                    {
                        var tempSpan = tempArray.AsSpan(0, count);
                        MovingAverageCore.SimpleMovingAverage(inputSpan, tempSpan, fastLength);
                        ReadOnlySpan<double> tempReadOnly = tempSpan;
                        MovingAverageCore.SimpleMovingAverage(tempReadOnly, buffer.WritableSpan, fastLength);
                    }
                    finally
                    {
                        pool.Return(tempArray);
                    }
                }
                break;
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.DoubleExponentialMovingAverage(inputSpan, buffer.WritableSpan, fastLength);
                break;
            case MovingAvgType.WeightedMovingAverage:
                {
                    var pool = ArrayPool<double>.Shared;
                    var tempArray = pool.Rent(count);
                    try
                    {
                        var tempSpan = tempArray.AsSpan(0, count);
                        MovingAverageCore.WeightedMovingAverage(inputSpan, tempSpan, fastLength);
                        ReadOnlySpan<double> tempReadOnly = tempSpan;
                        MovingAverageCore.WeightedMovingAverage(tempReadOnly, buffer.WritableSpan, fastLength);
                    }
                    finally
                    {
                        pool.Return(tempArray);
                    }
                }
                break;
            default:
                MovingAverageCore.DoubleExponentialMovingAverage(inputSpan, buffer.WritableSpan, fastLength);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Gain Loss Moving Average with signal line.
    /// </summary>
    internal static ComputeBuffer ComputeGainLossMovingAverageFast(StockData data, ComputeContext context, int length = 14, int signalLength = 7, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod)
    {
        var count = data.Count;
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var pool = ArrayPool<double>.Shared;
        var gainLossArray = pool.Rent(count);
        var gainLossAvgArray = pool.Rent(count);
        try
        {
            var gainLossSpan = gainLossArray.AsSpan(0, count);
            var gainLossAvgSpan = gainLossAvgArray.AsSpan(0, count);

            // Compute raw gain/loss percentage
            OscillatorCore.GainLoss(inputSpan, gainLossSpan);
            ReadOnlySpan<double> gainLossReadOnly = gainLossSpan;

            // First MA smoothing (length)
            switch (maType)
            {
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(gainLossReadOnly, gainLossAvgSpan, length);
                    break;
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(gainLossReadOnly, gainLossAvgSpan, length);
                    break;
                case MovingAvgType.WildersSmoothingMethod:
                    MovingAverageCore.WellesWilderMovingAverage(gainLossReadOnly, gainLossAvgSpan, length);
                    break;
                default:
                    MovingAverageCore.WellesWilderMovingAverage(gainLossReadOnly, gainLossAvgSpan, length);
                    break;
            }

            ReadOnlySpan<double> gainLossAvgReadOnly = gainLossAvgSpan;
            var buffer = context.Rent(count);

            // Second MA for signal line (signalLength)
            switch (maType)
            {
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(gainLossAvgReadOnly, buffer.WritableSpan, signalLength);
                    break;
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(gainLossAvgReadOnly, buffer.WritableSpan, signalLength);
                    break;
                case MovingAvgType.WildersSmoothingMethod:
                    MovingAverageCore.WellesWilderMovingAverage(gainLossAvgReadOnly, buffer.WritableSpan, signalLength);
                    break;
                default:
                    MovingAverageCore.WellesWilderMovingAverage(gainLossAvgReadOnly, buffer.WritableSpan, signalLength);
                    break;
            }

            return buffer;
        }
        finally
        {
            pool.Return(gainLossArray);
            pool.Return(gainLossAvgArray);
        }
    }

    /// <summary>
    /// Computes Ergodic Mean Deviation Indicator with signal line.
    /// </summary>
    internal static ComputeBuffer ComputeErgodicMeanDeviationIndicatorFast(StockData data, ComputeContext context, int length1 = 32, int length2 = 5, int length3 = 5, int signalLength = 5, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var count = data.Count;
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var pool = ArrayPool<double>.Shared;
        var emaArray = pool.Rent(count);
        var deviationArray = pool.Rent(count);
        var deviationEma1Array = pool.Rent(count);
        var emdiArray = pool.Rent(count);
        try
        {
            var emaSpan = emaArray.AsSpan(0, count);
            var deviationSpan = deviationArray.AsSpan(0, count);
            var deviationEma1Span = deviationEma1Array.AsSpan(0, count);
            var emdiSpan = emdiArray.AsSpan(0, count);

            // Step 1: Compute EMA of input (length1)
            switch (maType)
            {
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(inputSpan, emaSpan, length1);
                    break;
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(inputSpan, emaSpan, length1);
                    break;
                default:
                    MovingAverageCore.ExponentialMovingAverage(inputSpan, emaSpan, length1);
                    break;
            }

            // Step 2: Compute deviation from EMA
            ReadOnlySpan<double> emaReadOnly = emaSpan;
            OscillatorCore.DeviationFromMa(inputSpan, emaReadOnly, deviationSpan);
            ReadOnlySpan<double> deviationReadOnly = deviationSpan;

            // Step 3: First smoothing (length2)
            switch (maType)
            {
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(deviationReadOnly, deviationEma1Span, length2);
                    break;
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(deviationReadOnly, deviationEma1Span, length2);
                    break;
                default:
                    MovingAverageCore.ExponentialMovingAverage(deviationReadOnly, deviationEma1Span, length2);
                    break;
            }

            // Step 4: Second smoothing (length3) = EMDI
            ReadOnlySpan<double> deviationEma1ReadOnly = deviationEma1Span;
            switch (maType)
            {
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(deviationEma1ReadOnly, emdiSpan, length3);
                    break;
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(deviationEma1ReadOnly, emdiSpan, length3);
                    break;
                default:
                    MovingAverageCore.ExponentialMovingAverage(deviationEma1ReadOnly, emdiSpan, length3);
                    break;
            }

            // Step 5: Signal line (signalLength)
            ReadOnlySpan<double> emdiReadOnly = emdiSpan;
            var buffer = context.Rent(count);
            switch (maType)
            {
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(emdiReadOnly, buffer.WritableSpan, signalLength);
                    break;
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(emdiReadOnly, buffer.WritableSpan, signalLength);
                    break;
                default:
                    MovingAverageCore.ExponentialMovingAverage(emdiReadOnly, buffer.WritableSpan, signalLength);
                    break;
            }

            return buffer;
        }
        finally
        {
            pool.Return(emaArray);
            pool.Return(deviationArray);
            pool.Return(deviationEma1Array);
            pool.Return(emdiArray);
        }
    }

    /// <summary>
    /// Computes Mayer Multiple using zero-allocation fast path.
    /// MayerMultiple = price / MA
    /// </summary>
    internal static ComputeBuffer ComputeMayerMultipleFast(StockData data, ComputeContext context, int length = 200, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var pool = ArrayPool<double>.Shared;
        var maArray = pool.Rent(count);

        try
        {
            var maSpan = maArray.AsSpan(0, count);

            // Calculate MA of close prices
            switch (maType)
            {
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(close, maSpan, length);
                    break;
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(close, maSpan, length);
                    break;
                default:
                    MovingAverageCore.SimpleMovingAverage(close, maSpan, length);
                    break;
            }

            // Calculate Mayer Multiple = price / MA
            ReadOnlySpan<double> maReadOnly = maSpan;
            var buffer = context.Rent(count);
            VolatilityCore.MayerMultiple(close, maReadOnly, buffer.WritableSpan);

            return buffer;
        }
        finally
        {
            pool.Return(maArray);
        }
    }

    /// <summary>
    /// Computes Gopalakrishnan Range Index using zero-allocation fast path.
    /// GAPO = log(highestHigh - lowestLow) / log(length), then smoothed with MA.
    /// </summary>
    internal static ComputeBuffer ComputeGopalakrishnanRangeIndexFast(StockData data, ComputeContext context, int length = 5, MovingAvgType maType = MovingAvgType.WeightedMovingAverage)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var count = data.Count;
        var pool = ArrayPool<double>.Shared;
        var gapoArray = pool.Rent(count);

        try
        {
            var gapoSpan = gapoArray.AsSpan(0, count);

            // Calculate raw GAPO values
            VolatilityCore.GopalakrishnanRangeIndex(high, low, gapoSpan, length);

            // Smooth with moving average
            ReadOnlySpan<double> gapoReadOnly = gapoSpan;
            var buffer = context.Rent(count);
            switch (maType)
            {
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(gapoReadOnly, buffer.WritableSpan, length);
                    break;
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(gapoReadOnly, buffer.WritableSpan, length);
                    break;
                case MovingAvgType.WeightedMovingAverage:
                    MovingAverageCore.WeightedMovingAverage(gapoReadOnly, buffer.WritableSpan, length);
                    break;
                default:
                    MovingAverageCore.WeightedMovingAverage(gapoReadOnly, buffer.WritableSpan, length);
                    break;
            }

            return buffer;
        }
        finally
        {
            pool.Return(gapoArray);
        }
    }

    /// <summary>
    /// Computes High Low Moving Average using zero-allocation fast path.
    /// Returns middle band = (MA(highest high) + MA(lowest low)) / 2.
    /// </summary>
    internal static ComputeBuffer ComputeHighLowMovingAverageFast(StockData data, ComputeContext context, int length = 14, MovingAvgType maType = MovingAvgType.WeightedMovingAverage)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var count = data.Count;
        var pool = ArrayPool<double>.Shared;
        var upperArray = pool.Rent(count);
        var lowerArray = pool.Rent(count);
        var middleArray = pool.Rent(count);
        var upperMaArray = pool.Rent(count);
        var lowerMaArray = pool.Rent(count);

        try
        {
            var upperSpan = upperArray.AsSpan(0, count);
            var lowerSpan = lowerArray.AsSpan(0, count);
            var middleSpan = middleArray.AsSpan(0, count);
            var upperMaSpan = upperMaArray.AsSpan(0, count);
            var lowerMaSpan = lowerMaArray.AsSpan(0, count);

            // Calculate raw highest high and lowest low
            VolatilityCore.HighLowMovingAverageRaw(high, low, upperSpan, lowerSpan, middleSpan, length);

            // Smooth bands with MA
            ReadOnlySpan<double> upperReadOnly = upperSpan;
            ReadOnlySpan<double> lowerReadOnly = lowerSpan;

            switch (maType)
            {
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(upperReadOnly, upperMaSpan, length);
                    MovingAverageCore.SimpleMovingAverage(lowerReadOnly, lowerMaSpan, length);
                    break;
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(upperReadOnly, upperMaSpan, length);
                    MovingAverageCore.ExponentialMovingAverage(lowerReadOnly, lowerMaSpan, length);
                    break;
                case MovingAvgType.WeightedMovingAverage:
                    MovingAverageCore.WeightedMovingAverage(upperReadOnly, upperMaSpan, length);
                    MovingAverageCore.WeightedMovingAverage(lowerReadOnly, lowerMaSpan, length);
                    break;
                default:
                    MovingAverageCore.WeightedMovingAverage(upperReadOnly, upperMaSpan, length);
                    MovingAverageCore.WeightedMovingAverage(lowerReadOnly, lowerMaSpan, length);
                    break;
            }

            // Calculate middle band = (upperMa + lowerMa) / 2
            var buffer = context.Rent(count);
            for (var i = 0; i < count; i++)
            {
                buffer.WritableSpan[i] = (upperMaSpan[i] + lowerMaSpan[i]) / 2;
            }

            return buffer;
        }
        finally
        {
            pool.Return(upperArray);
            pool.Return(lowerArray);
            pool.Return(middleArray);
            pool.Return(upperMaArray);
            pool.Return(lowerMaArray);
        }
    }

    /// <summary>
    /// Computes Stiffness Indicator using zero-allocation fast path.
    /// Measures how many closes are above MA - 0.2*StdDev over a period.
    /// </summary>
    internal static ComputeBuffer ComputeStiffnessIndicatorFast(StockData data, ComputeContext context, int length1 = 100, int length2 = 60, int smoothingLength = 3, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var pool = ArrayPool<double>.Shared;
        var maArray = pool.Rent(count);
        var stdDevArray = pool.Rent(count);
        var stiffnessArray = pool.Rent(count);

        try
        {
            var maSpan = maArray.AsSpan(0, count);
            var stdDevSpan = stdDevArray.AsSpan(0, count);
            var stiffnessSpan = stiffnessArray.AsSpan(0, count);

            // Calculate MA of close prices
            switch (maType)
            {
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(close, maSpan, length1);
                    break;
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(close, maSpan, length1);
                    break;
                default:
                    MovingAverageCore.SimpleMovingAverage(close, maSpan, length1);
                    break;
            }

            // Calculate standard deviation
            VolatilityCore.StandardDeviation(close, stdDevSpan, length1);

            // Calculate raw stiffness
            ReadOnlySpan<double> maReadOnly = maSpan;
            ReadOnlySpan<double> stdDevReadOnly = stdDevSpan;
            VolatilityCore.StiffnessIndicator(close, maReadOnly, stdDevReadOnly, stiffnessSpan, length2);

            // Smooth with moving average
            ReadOnlySpan<double> stiffnessReadOnly = stiffnessSpan;
            var buffer = context.Rent(count);
            switch (maType)
            {
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(stiffnessReadOnly, buffer.WritableSpan, smoothingLength);
                    break;
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(stiffnessReadOnly, buffer.WritableSpan, smoothingLength);
                    break;
                default:
                    MovingAverageCore.SimpleMovingAverage(stiffnessReadOnly, buffer.WritableSpan, smoothingLength);
                    break;
            }

            return buffer;
        }
        finally
        {
            pool.Return(maArray);
            pool.Return(stdDevArray);
            pool.Return(stiffnessArray);
        }
    }

    /// <summary>
    /// Computes Market Meanness Index using zero-allocation fast path.
    /// Counts reversals above/below median in a lookback period.
    /// </summary>
    internal static ComputeBuffer ComputeMarketMeannessIndexFast(StockData data, ComputeContext context, int length = 100, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = data.Count;
        var pool = ArrayPool<double>.Shared;
        var mmiArray = pool.Rent(count);

        try
        {
            var mmiSpan = mmiArray.AsSpan(0, count);

            // Calculate raw MMI using a rolling window
            for (var i = 0; i < count; i++)
            {
                if (i < length - 1)
                {
                    mmiSpan[i] = 0;
                    continue;
                }

                // Calculate median for the window
                var windowStart = i - length + 1;
                var windowSize = length;

                // Simple approach: sort and get median
                var tempArray = pool.Rent(windowSize);
                try
                {
                    for (var j = 0; j < windowSize; j++)
                    {
                        tempArray[j] = input[windowStart + j];
                    }
                    Array.Sort(tempArray, 0, windowSize);
                    var median = tempArray[windowSize / 2];

                    // Count reversals
                    int nl = 0, nh = 0;
                    for (var j = 1; j < length; j++)
                    {
                        var value1 = i >= j - 1 ? input[i - (j - 1)] : 0;
                        var value2 = i >= j ? input[i - j] : 0;

                        if (value1 > median && value1 > value2)
                        {
                            nl++;
                        }
                        else if (value1 < median && value1 < value2)
                        {
                            nh++;
                        }
                    }

                    mmiSpan[i] = length != 1 ? 100.0 * (nl + nh) / (length - 1) : 0;
                }
                finally
                {
                    pool.Return(tempArray);
                }
            }

            // Smooth with moving average
            ReadOnlySpan<double> mmiReadOnly = mmiSpan;
            var buffer = context.Rent(count);
            switch (maType)
            {
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(mmiReadOnly, buffer.WritableSpan, length);
                    break;
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(mmiReadOnly, buffer.WritableSpan, length);
                    break;
                default:
                    MovingAverageCore.SimpleMovingAverage(mmiReadOnly, buffer.WritableSpan, length);
                    break;
            }

            return buffer;
        }
        finally
        {
            pool.Return(mmiArray);
        }
    }

    /// <summary>
    /// Computes Sharpe Ratio using zero-allocation fast path.
    /// SharpeRatio = (returns - benchmark) / standardDeviation
    /// </summary>
    internal static ComputeBuffer ComputeSharpeRatioFast(StockData data, ComputeContext context, int length = 30, double bmk = 0.02, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var pool = ArrayPool<double>.Shared;
        var returnsArray = pool.Rent(count);
        var avgReturnsArray = pool.Rent(count);
        var stdDevArray = pool.Rent(count);

        try
        {
            var returnsSpan = returnsArray.AsSpan(0, count);
            var avgReturnsSpan = avgReturnsArray.AsSpan(0, count);
            var stdDevSpan = stdDevArray.AsSpan(0, count);

            // Calculate returns
            returnsSpan[0] = 0;
            for (var i = 1; i < count; i++)
            {
                var prevClose = close[i - 1];
                returnsSpan[i] = prevClose != 0 ? (close[i] - prevClose) / prevClose : 0;
            }

            ReadOnlySpan<double> returnsReadOnly = returnsSpan;

            // Calculate average returns
            switch (maType)
            {
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(returnsReadOnly, avgReturnsSpan, length);
                    break;
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(returnsReadOnly, avgReturnsSpan, length);
                    break;
                default:
                    MovingAverageCore.SimpleMovingAverage(returnsReadOnly, avgReturnsSpan, length);
                    break;
            }

            // Calculate standard deviation of returns
            VolatilityCore.StandardDeviation(returnsReadOnly, stdDevSpan, length);

            // Calculate Sharpe Ratio
            var dailyBmk = bmk / 252; // Annualized to daily
            var buffer = context.Rent(count);
            for (var i = 0; i < count; i++)
            {
                buffer.WritableSpan[i] = stdDevSpan[i] != 0 ? (avgReturnsSpan[i] - dailyBmk) / stdDevSpan[i] : 0;
            }

            return buffer;
        }
        finally
        {
            pool.Return(returnsArray);
            pool.Return(avgReturnsArray);
            pool.Return(stdDevArray);
        }
    }

    /// <summary>
    /// Computes Sortino Ratio using zero-allocation fast path.
    /// SortinoRatio = (returns - benchmark) / downsideDeviation
    /// </summary>
    internal static ComputeBuffer ComputeSortinoRatioFast(StockData data, ComputeContext context, int length = 30, double bmk = 0.02, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var pool = ArrayPool<double>.Shared;
        var returnsArray = pool.Rent(count);
        var avgReturnsArray = pool.Rent(count);
        var downsideDevArray = pool.Rent(count);
        var downsideSqArray = pool.Rent(count);

        try
        {
            var returnsSpan = returnsArray.AsSpan(0, count);
            var avgReturnsSpan = avgReturnsArray.AsSpan(0, count);
            var downsideDevSpan = downsideDevArray.AsSpan(0, count);
            var downsideSqSpan = downsideSqArray.AsSpan(0, count);

            // Calculate returns
            var dailyBmk = bmk / 252;
            returnsSpan[0] = 0;
            for (var i = 1; i < count; i++)
            {
                var prevClose = close[i - 1];
                returnsSpan[i] = prevClose != 0 ? (close[i] - prevClose) / prevClose - dailyBmk : 0;
            }

            ReadOnlySpan<double> returnsReadOnly = returnsSpan;

            // Calculate average returns
            switch (maType)
            {
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(returnsReadOnly, avgReturnsSpan, length);
                    break;
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(returnsReadOnly, avgReturnsSpan, length);
                    break;
                default:
                    MovingAverageCore.SimpleMovingAverage(returnsReadOnly, avgReturnsSpan, length);
                    break;
            }

            // Calculate downside deviation squared (only negative deviations)
            for (var i = 0; i < count; i++)
            {
                var deviation = Math.Min(returnsSpan[i] - avgReturnsSpan[i], 0);
                downsideSqSpan[i] = deviation * deviation;
            }

            // Smooth downside deviation squared
            ReadOnlySpan<double> downsideSqReadOnly = downsideSqSpan;
            switch (maType)
            {
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(downsideSqReadOnly, downsideDevSpan, length);
                    break;
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(downsideSqReadOnly, downsideDevSpan, length);
                    break;
                default:
                    MovingAverageCore.SimpleMovingAverage(downsideSqReadOnly, downsideDevSpan, length);
                    break;
            }

            // Calculate Sortino Ratio
            var buffer = context.Rent(count);
            for (var i = 0; i < count; i++)
            {
                var downsideStdDev = Math.Sqrt(downsideDevSpan[i]);
                buffer.WritableSpan[i] = downsideStdDev != 0 ? avgReturnsSpan[i] / downsideStdDev : 0;
            }

            return buffer;
        }
        finally
        {
            pool.Return(returnsArray);
            pool.Return(avgReturnsArray);
            pool.Return(downsideDevArray);
            pool.Return(downsideSqArray);
        }
    }

    /// <summary>
    /// Computes Martin Ratio using zero-allocation fast path.
    /// MartinRatio = returns / ulcerIndex
    /// </summary>
    internal static ComputeBuffer ComputeMartinRatioFast(StockData data, ComputeContext context, int length = 30, double bmk = 0.02, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var pool = ArrayPool<double>.Shared;
        var returnsArray = pool.Rent(count);
        var avgReturnsArray = pool.Rent(count);
        var ulcerArray = pool.Rent(count);

        try
        {
            var returnsSpan = returnsArray.AsSpan(0, count);
            var avgReturnsSpan = avgReturnsArray.AsSpan(0, count);
            var ulcerSpan = ulcerArray.AsSpan(0, count);

            // Calculate returns
            var dailyBmk = bmk / 252;
            returnsSpan[0] = 0;
            for (var i = 1; i < count; i++)
            {
                var prevClose = close[i - 1];
                returnsSpan[i] = prevClose != 0 ? 100 * ((close[i] / prevClose) - 1 - dailyBmk) : 0;
            }

            ReadOnlySpan<double> returnsReadOnly = returnsSpan;

            // Calculate average returns
            switch (maType)
            {
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(returnsReadOnly, avgReturnsSpan, length);
                    break;
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(returnsReadOnly, avgReturnsSpan, length);
                    break;
                default:
                    MovingAverageCore.SimpleMovingAverage(returnsReadOnly, avgReturnsSpan, length);
                    break;
            }

            // Calculate Ulcer Index on returns
            VolatilityCore.UlcerIndex(close, ulcerSpan, length);

            // Calculate Martin Ratio
            var buffer = context.Rent(count);
            for (var i = 0; i < count; i++)
            {
                buffer.WritableSpan[i] = ulcerSpan[i] != 0 ? avgReturnsSpan[i] / ulcerSpan[i] : 0;
            }

            return buffer;
        }
        finally
        {
            pool.Return(returnsArray);
            pool.Return(avgReturnsArray);
            pool.Return(ulcerArray);
        }
    }

    /// <summary>
    /// Computes Information Ratio using zero-allocation fast path.
    /// InformationRatio = excessReturns / trackingError
    /// </summary>
    internal static ComputeBuffer ComputeInformationRatioFast(StockData data, ComputeContext context, int length = 30, double bmk = 0.05, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var pool = ArrayPool<double>.Shared;
        var excessArray = pool.Rent(count);
        var avgExcessArray = pool.Rent(count);
        var trackingErrorArray = pool.Rent(count);

        try
        {
            var excessSpan = excessArray.AsSpan(0, count);
            var avgExcessSpan = avgExcessArray.AsSpan(0, count);
            var trackingErrorSpan = trackingErrorArray.AsSpan(0, count);

            // Calculate excess returns
            var dailyBmk = bmk / 252;
            excessSpan[0] = 0;
            for (var i = 1; i < count; i++)
            {
                var prevClose = close[i - 1];
                excessSpan[i] = prevClose != 0 ? (close[i] - prevClose) / prevClose - dailyBmk : 0;
            }

            ReadOnlySpan<double> excessReadOnly = excessSpan;

            // Calculate average excess returns
            switch (maType)
            {
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(excessReadOnly, avgExcessSpan, length);
                    break;
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(excessReadOnly, avgExcessSpan, length);
                    break;
                default:
                    MovingAverageCore.SimpleMovingAverage(excessReadOnly, avgExcessSpan, length);
                    break;
            }

            // Calculate tracking error (stddev of excess returns)
            VolatilityCore.StandardDeviation(excessReadOnly, trackingErrorSpan, length);

            // Calculate Information Ratio
            var buffer = context.Rent(count);
            for (var i = 0; i < count; i++)
            {
                buffer.WritableSpan[i] = trackingErrorSpan[i] != 0 ? avgExcessSpan[i] / trackingErrorSpan[i] : 0;
            }

            return buffer;
        }
        finally
        {
            pool.Return(excessArray);
            pool.Return(avgExcessArray);
            pool.Return(trackingErrorArray);
        }
    }

    /// <summary>
    /// Computes Optimized Trend Tracker using zero-allocation fast path.
    /// OTT is a trend-following indicator based on MA with percentage bands.
    /// </summary>
    internal static ComputeBuffer ComputeOptimizedTrendTrackerFast(StockData data, ComputeContext context, int length = 2, double percent = 1.4, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = data.Count;
        var pool = ArrayPool<double>.Shared;
        var maArray = pool.Rent(count);
        var longStopArray = pool.Rent(count);
        var shortStopArray = pool.Rent(count);

        try
        {
            var maSpan = maArray.AsSpan(0, count);
            var longStopSpan = longStopArray.AsSpan(0, count);
            var shortStopSpan = shortStopArray.AsSpan(0, count);

            // Calculate MA
            switch (maType)
            {
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(input, maSpan, length);
                    break;
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(input, maSpan, length);
                    break;
                default:
                    MovingAverageCore.SimpleMovingAverage(input, maSpan, length);
                    break;
            }

            // Calculate OTT
            var buffer = context.Rent(count);
            for (var i = 0; i < count; i++)
            {
                var ma = maSpan[i];
                var fark = ma * percent * 0.01;

                var prevLongStop = i >= 1 ? longStopSpan[i - 1] : 0;
                var longStop = ma - fark;
                longStop = ma > prevLongStop ? Math.Max(longStop, prevLongStop) : longStop;
                longStopSpan[i] = longStop;

                var prevShortStop = i >= 1 ? shortStopSpan[i - 1] : 0;
                var shortStop = ma + fark;
                shortStopSpan[i] = shortStop;

                var mt = ma > prevShortStop ? longStop : ma < prevLongStop ? shortStop : 0;
                var ott = ma > mt ? mt * (200 + percent) / 200 : mt * (200 - percent) / 200;
                buffer.WritableSpan[i] = ott;
            }

            return buffer;
        }
        finally
        {
            pool.Return(maArray);
            pool.Return(longStopArray);
            pool.Return(shortStopArray);
        }
    }

    /// <summary>
    /// Computes Random Walk Index using zero-allocation fast path.
    /// RWI measures trend strength using ATR-normalized price movement.
    /// </summary>
    internal static ComputeBuffer ComputeRandomWalkIndexFast(StockData data, ComputeContext context, int length = 14, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var pool = ArrayPool<double>.Shared;
        var atrArray = pool.Rent(count);

        try
        {
            var atrSpan = atrArray.AsSpan(0, count);

            // Calculate ATR
            VolatilityCore.AverageTrueRange(high, low, close, atrSpan, length);

            var sqrt = Math.Sqrt(length);

            // Calculate RWI High (returns this by default)
            var buffer = context.Rent(count);
            for (var i = 0; i < count; i++)
            {
                if (i < length)
                {
                    buffer.WritableSpan[i] = 0;
                    continue;
                }

                var currentAtr = atrSpan[i];
                var currentHigh = high[i];
                var prevLow = low[i - length];
                var bottom = currentAtr * sqrt;

                buffer.WritableSpan[i] = bottom != 0 ? (currentHigh - prevLow) / bottom : 0;
            }

            return buffer;
        }
        finally
        {
            pool.Return(atrArray);
        }
    }

    /// <summary>
    /// Computes Price Channel using zero-allocation fast path.
    /// Price Channel = MA +/- percentage bands.
    /// </summary>
    internal static ComputeBuffer ComputePriceChannelFast(StockData data, ComputeContext context, int length = 21, double pct = 0.06, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var pool = ArrayPool<double>.Shared;
        var maArray = pool.Rent(count);

        try
        {
            var maSpan = maArray.AsSpan(0, count);

            // Calculate MA
            switch (maType)
            {
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(close, maSpan, length);
                    break;
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(close, maSpan, length);
                    break;
                default:
                    MovingAverageCore.ExponentialMovingAverage(close, maSpan, length);
                    break;
            }

            // Return middle band (MA)
            var buffer = context.Rent(count);
            for (var i = 0; i < count; i++)
            {
                buffer.WritableSpan[i] = maSpan[i];
            }

            return buffer;
        }
        finally
        {
            pool.Return(maArray);
        }
    }

    /// <summary>
    /// Computes Moving Average Bands using zero-allocation fast path.
    /// Returns middle band (average of fast and slow MAs).
    /// </summary>
    internal static ComputeBuffer ComputeMovingAverageBandsFast(StockData data, ComputeContext context, int fastLength = 10, int slowLength = 50, double mult = 1, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var pool = ArrayPool<double>.Shared;
        var fastMaArray = pool.Rent(count);
        var slowMaArray = pool.Rent(count);

        try
        {
            var fastMaSpan = fastMaArray.AsSpan(0, count);
            var slowMaSpan = slowMaArray.AsSpan(0, count);

            // Calculate fast and slow MAs
            switch (maType)
            {
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(close, fastMaSpan, fastLength);
                    MovingAverageCore.SimpleMovingAverage(close, slowMaSpan, slowLength);
                    break;
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(close, fastMaSpan, fastLength);
                    MovingAverageCore.ExponentialMovingAverage(close, slowMaSpan, slowLength);
                    break;
                default:
                    MovingAverageCore.ExponentialMovingAverage(close, fastMaSpan, fastLength);
                    MovingAverageCore.ExponentialMovingAverage(close, slowMaSpan, slowLength);
                    break;
            }

            // Return middle band (average of fast and slow)
            var buffer = context.Rent(count);
            for (var i = 0; i < count; i++)
            {
                buffer.WritableSpan[i] = (fastMaSpan[i] + slowMaSpan[i]) / 2;
            }

            return buffer;
        }
        finally
        {
            pool.Return(fastMaArray);
            pool.Return(slowMaArray);
        }
    }

    /// <summary>
    /// Computes Moving Average Band Width using zero-allocation fast path.
    /// Returns the width between fast and slow MA bands.
    /// </summary>
    internal static ComputeBuffer ComputeMovingAverageBandWidthFast(StockData data, ComputeContext context, int fastLength = 10, int slowLength = 50, double mult = 1, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var pool = ArrayPool<double>.Shared;
        var fastMaArray = pool.Rent(count);
        var slowMaArray = pool.Rent(count);

        try
        {
            var fastMaSpan = fastMaArray.AsSpan(0, count);
            var slowMaSpan = slowMaArray.AsSpan(0, count);

            // Calculate fast and slow MAs
            switch (maType)
            {
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(close, fastMaSpan, fastLength);
                    MovingAverageCore.SimpleMovingAverage(close, slowMaSpan, slowLength);
                    break;
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(close, fastMaSpan, fastLength);
                    MovingAverageCore.ExponentialMovingAverage(close, slowMaSpan, slowLength);
                    break;
                default:
                    MovingAverageCore.ExponentialMovingAverage(close, fastMaSpan, fastLength);
                    MovingAverageCore.ExponentialMovingAverage(close, slowMaSpan, slowLength);
                    break;
            }

            // Return band width
            var buffer = context.Rent(count);
            for (var i = 0; i < count; i++)
            {
                var middle = (fastMaSpan[i] + slowMaSpan[i]) / 2;
                buffer.WritableSpan[i] = middle != 0 ? Math.Abs(fastMaSpan[i] - slowMaSpan[i]) * mult / middle * 100 : 0;
            }

            return buffer;
        }
        finally
        {
            pool.Return(fastMaArray);
            pool.Return(slowMaArray);
        }
    }

    /// <summary>
    /// Computes Moving Average Channel using zero-allocation fast path.
    /// Returns the MA of high-low range.
    /// </summary>
    internal static ComputeBuffer ComputeMovingAverageChannelFast(StockData data, ComputeContext context, int length = 20, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var count = data.Count;
        var pool = ArrayPool<double>.Shared;
        var highMaArray = pool.Rent(count);
        var lowMaArray = pool.Rent(count);

        try
        {
            var highMaSpan = highMaArray.AsSpan(0, count);
            var lowMaSpan = lowMaArray.AsSpan(0, count);

            // Calculate MA of high and low
            switch (maType)
            {
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(high, highMaSpan, length);
                    MovingAverageCore.SimpleMovingAverage(low, lowMaSpan, length);
                    break;
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(high, highMaSpan, length);
                    MovingAverageCore.ExponentialMovingAverage(low, lowMaSpan, length);
                    break;
                default:
                    MovingAverageCore.SimpleMovingAverage(high, highMaSpan, length);
                    MovingAverageCore.SimpleMovingAverage(low, lowMaSpan, length);
                    break;
            }

            // Return middle (average of high and low MAs)
            var buffer = context.Rent(count);
            for (var i = 0; i < count; i++)
            {
                buffer.WritableSpan[i] = (highMaSpan[i] + lowMaSpan[i]) / 2;
            }

            return buffer;
        }
        finally
        {
            pool.Return(highMaArray);
            pool.Return(lowMaArray);
        }
    }

    /// <summary>
    /// Computes Moving Average Envelope using zero-allocation fast path.
    /// Returns the middle band (MA).
    /// </summary>
    internal static ComputeBuffer ComputeMovingAverageEnvelopeFast(StockData data, ComputeContext context, int length = 20, double pct = 0.025, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var pool = ArrayPool<double>.Shared;
        var maArray = pool.Rent(count);

        try
        {
            var maSpan = maArray.AsSpan(0, count);

            // Calculate MA
            switch (maType)
            {
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(close, maSpan, length);
                    break;
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(close, maSpan, length);
                    break;
                default:
                    MovingAverageCore.SimpleMovingAverage(close, maSpan, length);
                    break;
            }

            // Return middle band
            var buffer = context.Rent(count);
            for (var i = 0; i < count; i++)
            {
                buffer.WritableSpan[i] = maSpan[i];
            }

            return buffer;
        }
        finally
        {
            pool.Return(maArray);
        }
    }

    /// <summary>
    /// Computes Moving Average Support/Resistance using zero-allocation fast path.
    /// Returns the MA of close.
    /// </summary>
    internal static ComputeBuffer ComputeMovingAverageSupportResistanceFast(StockData data, ComputeContext context, int length = 10, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        // Calculate MA
        switch (maType)
        {
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Variable Moving Average Bands using zero-allocation fast path.
    /// Returns the middle band (VMA).
    /// </summary>
    internal static ComputeBuffer ComputeVariableMovingAverageBandsFast(StockData data, ComputeContext context, int length = 6,
        double mult = 1.5, MovingAvgType maType = MovingAvgType.VariableMovingAverage, ChannelBand band = ChannelBand.Middle)
    {
        // CalculateVariableMovingAverageBands centres on the moving average of the chained series and steps
        // the outer bands by a multiple of the average true range. The switch this replaced fell back to a
        // simple average of the close for every type but two, including the variable average it is named for.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var count = inputList.Count;
        length = Math.Max(length, 1);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        MovingAverage(data, maType, length, SpanCompat.AsReadOnlySpan(inputList), output);

        if (band == ChannelBand.Middle)
        {
            return buffer;
        }

        using var averageTrueRange = ComputeAtrFast(data, context, length, maType);
        var atr = averageTrueRange.Span;
        var multiplier = band == ChannelBand.Upper ? mult : -mult;
        for (var i = 0; i < count; i++)
        {
            output[i] += multiplier * atr[i];
        }

        return buffer;
    }

    /// <summary>
    /// Computes Narrow Sideways Channel using zero-allocation fast path.
    /// Returns the middle band (MA).
    /// </summary>
    internal static ComputeBuffer ComputeNarrowSidewaysChannelFast(StockData data, ComputeContext context, int length = 20,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, ChannelBand band = ChannelBand.Middle)
    {
        // CalculateNarrowSidewaysChannel republishes Bollinger Bands at three standard deviations and nothing
        // else, so the arm is that band rather than a moving average of the close. The channel has no
        // percentage of its own, which is why the spec's Pct is marked as having no effect.
        const double stdDevMult = 3;

        if (band == ChannelBand.Middle)
        {
            var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
            var middle = context.Rent(inputList.Count);
            MovingAverage(data, maType, Math.Max(length, 1), SpanCompat.AsReadOnlySpan(inputList), middle.WritableSpan);
            return middle;
        }

        return BollingerBand(data, context, length, band == ChannelBand.Upper ? stdDevMult : -stdDevMult, maType);
    }

    /// <summary>
    /// Computes High Low Bands using zero-allocation fast path.
    /// Returns the middle band (SMA of close).
    /// </summary>
    internal static ComputeBuffer ComputeHighLowBandsFast(StockData data, ComputeContext context, int length = 14, double pctShift = 1,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // CalculateHighLowBands centres its bands on the chained series smoothed twice, not on the close
        // smoothed once, and shifts them by a percentage of that centre. A shift of zero is the centre itself.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var count = inputList.Count;
        length = Math.Max(length, 1);

        using var smoothed = context.Rent(count);
        MovingAverage(data, maType, length, SpanCompat.AsReadOnlySpan(inputList), smoothed.WritableSpan);

        using var twiceSmoothed = context.Rent(count);
        MovingAverage(data, maType, length, smoothed.Span, twiceSmoothed.WritableSpan);
        var tma = twiceSmoothed.Span;

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            output[i] = tma[i] + (tma[i] * pctShift / 100);
        }

        return buffer;
    }

    /// <summary>
    /// Computes Auto Dispersion Bands using zero-allocation fast path.
    /// Returns the middle band (WMA of close).
    /// </summary>
    internal static ComputeBuffer ComputeAutoDispersionBandsFast(StockData data, ComputeContext context, int length = 90,
        int smoothLength = 140, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, ChannelBand band = ChannelBand.Middle)
    {
        // CalculateAutoDispersionBands disperses each bar by the root mean square of the change over the
        // window, takes the running extreme of each envelope and smooths it twice - once over the window and
        // again over the smoothing length. The middle band is the average of the two, not a moving average of
        // the close, which is what this computed.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = inputList.Count;
        length = Math.Max(length, 1);
        smoothLength = Math.Max(smoothLength, 1);

        using var aMaxima = context.Rent(count);
        using var bMinima = context.Rent(count);
        var aMax = aMaxima.WritableSpan;
        var bMin = bMinima.WritableSpan;

        var changeSquaredSum = new RollingSum();
        var aWindow = new RollingMinMax(length);
        var bWindow = new RollingMinMax(length);
        for (var i = 0; i < count; i++)
        {
            var currentValue = input[i];
            var previousValue = i >= length ? input[i - length] : 0;
            var change = CalculationsHelper.MinPastValues(i, length, currentValue - previousValue);
            changeSquaredSum.Add(change * change);

            var meanSquare = changeSquaredSum.Average(length);
            var dispersion = meanSquare >= 0 ? MathHelper.Sqrt(meanSquare) : 0;

            aWindow.Add(currentValue + dispersion);
            bWindow.Add(currentValue - dispersion);
            aMax[i] = aWindow.Max;
            bMin[i] = bWindow.Min;
        }

        using var upperSmoothed = context.Rent(count);
        using var upperBand = context.Rent(count);
        MovingAverage(data, maType, length, aMaxima.Span, upperSmoothed.WritableSpan);
        MovingAverage(data, maType, smoothLength, upperSmoothed.Span, upperBand.WritableSpan);

        if (band == ChannelBand.Upper)
        {
            var upperOnly = context.Rent(count);
            upperBand.Span.CopyTo(upperOnly.WritableSpan);
            return upperOnly;
        }

        using var lowerSmoothed = context.Rent(count);
        using var lowerBand = context.Rent(count);
        MovingAverage(data, maType, length, bMinima.Span, lowerSmoothed.WritableSpan);
        MovingAverage(data, maType, smoothLength, lowerSmoothed.Span, lowerBand.WritableSpan);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        var upper = upperBand.Span;
        var lower = lowerBand.Span;
        for (var i = 0; i < count; i++)
        {
            output[i] = band == ChannelBand.Lower ? lower[i] : (upper[i] + lower[i]) / 2;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Bollinger Bands Fibonacci Ratios using zero-allocation fast path.
    /// Returns the middle band (SMA).
    /// </summary>
    internal static ComputeBuffer ComputeBollingerBandsFibonacciRatiosFast(StockData data, ComputeContext context, int length = 20, double fibRatio1 = 1.618, double fibRatio2 = 2.618, double fibRatio3 = 4.236, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        // Calculate MA
        switch (maType)
        {
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Bollinger Bands with ATR Percentage using zero-allocation fast path.
    /// Returns the middle band (SMA).
    /// </summary>
    internal static ComputeBuffer ComputeBollingerBandsWithAtrPctFast(StockData data, ComputeContext context, int length = 14, int bbLength = 20, double stdDevMult = 2, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        // Calculate MA
        switch (maType)
        {
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, bbLength);
                break;
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, bbLength);
                break;
            default:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, bbLength);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Kirshenbaum Bands using zero-allocation fast path.
    /// Returns the middle band (EMA).
    /// </summary>
    internal static ComputeBuffer ComputeKirshenbaumBandsFast(StockData data, ComputeContext context, int length1 = 30, int length2 = 20, double stdDevFactor = 1, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        // Calculate MA
        switch (maType)
        {
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length1);
                break;
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length1);
                break;
            default:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length1);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Smoothed Volatility Bands using zero-allocation fast path.
    /// Returns the middle band (EMA).
    /// </summary>
    internal static ComputeBuffer ComputeSmoothedVolatilityBandsFast(StockData data, ComputeContext context, int length1 = 20, int length2 = 21, double deviation = 2.4, double bandAdjust = 0.9, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        // Calculate MA
        switch (maType)
        {
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length1);
                break;
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length1);
                break;
            default:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length1);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Stoller Average Range Channels (STARC) using zero-allocation fast path.
    /// Returns the middle band (SMA).
    /// </summary>
    internal static ComputeBuffer ComputeStollerAverageRangeChannelsFast(StockData data, ComputeContext context, int length = 14, double atrMult = 2, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        // Calculate MA
        switch (maType)
        {
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Vervoort Volatility Bands using zero-allocation fast path.
    /// Returns the middle band (EMA).
    /// </summary>
    internal static ComputeBuffer ComputeVervoortVolatilityBandsFast(StockData data, ComputeContext context, int length1 = 8, int length2 = 13, double devMult = 3.55, double lowBandMult = 0.9, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        // Calculate MA
        switch (maType)
        {
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length1);
                break;
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length1);
                break;
            default:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length1);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Volume Adaptive Bands using zero-allocation fast path.
    /// Returns the middle band (SMA).
    /// </summary>
    internal static ComputeBuffer ComputeVolumeAdaptiveBandsFast(StockData data, ComputeContext context, int length = 100, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        switch (maType)
        {
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Trend Trader Bands using zero-allocation fast path.
    /// Returns the middle band (WMA).
    /// </summary>
    internal static ComputeBuffer ComputeTrendTraderBandsFast(StockData data, ComputeContext context, int length = 21, double mult = 3,
        double bandStep = 20, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, ChannelBand band = ChannelBand.Middle)
    {
        // CalculateTrendTraderBands smooths a trailing stop that flips between the previous bar's highest less
        // a multiple of the average true range and its lowest plus the same, holding its last value while
        // price sits between them. The outer bands are that line stepped by a fixed amount.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = inputList.Count;
        length = Math.Max(length, 1);

        using var averageTrueRange = ComputeAtrFast(data, context, length, maType);
        var atr = averageTrueRange.Span;

        using var stops = context.Rent(count);
        var stop = stops.WritableSpan;

        var window = new RollingMinMax(Math.Max(length, 2));
        double previousStop = 0;
        for (var i = 0; i < count; i++)
        {
            // The batch reads the previous bar's extremes, so the window is read before this bar joins it.
            var previousHighest = i >= 1 ? window.Max : 0;
            var previousLowest = i >= 1 ? window.Min : 0;
            window.Add(input[i]);

            var atrMult = (i >= 1 ? atr[i - 1] : 0) * mult;
            var highLimit = previousHighest - atrMult;
            var lowLimit = previousLowest + atrMult;
            var currentValue = input[i];

            if (currentValue > highLimit && currentValue > lowLimit)
            {
                previousStop = highLimit;
            }
            else if (currentValue < lowLimit && currentValue < highLimit)
            {
                previousStop = lowLimit;
            }

            stop[i] = previousStop;
        }

        var buffer = context.Rent(count);
        MovingAverage(data, maType, length, stops.Span, buffer.WritableSpan);

        if (band != ChannelBand.Middle)
        {
            var step = band == ChannelBand.Upper ? bandStep : -bandStep;
            var output = buffer.WritableSpan;
            for (var i = 0; i < count; i++)
            {
                output[i] += step;
            }
        }

        return buffer;
    }

    /// <summary>
    /// Computes Scalpers Channel using zero-allocation fast path.
    /// Returns the middle band (SMA).
    /// </summary>
    internal static ComputeBuffer ComputeScalpersChannelFast(StockData data, ComputeContext context, int length1 = 15, int length2 = 20, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        switch (maType)
        {
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length1);
                break;
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length1);
                break;
            default:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length1);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Hurst Cycle Channel using zero-allocation fast path.
    /// Returns the middle line (Wilder smoothed).
    /// </summary>
    /// <summary>
    /// Which of the Hurst cycle channel's eight series an arm has been asked for. Two envelopes and the two
    /// oscillators drawn from them all fall out of one walk, so the series is chosen on the way out.
    /// </summary>
    internal enum HurstCycleSeries
    {
        FastMiddleBand,
        FastUpperBand,
        FastLowerBand,
        SlowMiddleBand,
        SlowUpperBand,
        SlowLowerBand,
        OMed,
        OShort
    }

    internal static ComputeBuffer ComputeHurstCycleChannelFast(StockData data, ComputeContext context, int fastLength = 10,
        int slowLength = 30, double fastMult = 1, double slowMult = 3, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        HurstCycleSeries series = HurstCycleSeries.FastMiddleBand)
    {
        // CalculateHurstCycleChannel halves each length into a cycle, centres an envelope on the moving
        // average of the chained series as it stood half a cycle ago, and offsets it by a multiple of the
        // average true range over the same cycle. The two oscillators place the fast centre and price itself
        // within the slow envelope. The switch this replaced was a moving average of the close, which is none
        // of the eight.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = inputList.Count;

        // Each cycle is clamped the way the batch clamps it: never shorter than two bars, never longer than
        // 530, which is what MinOrMax does for a length.
        var fastCycle = MathHelper.MinOrMax((int)Math.Ceiling((double)fastLength / 2));
        var slowCycle = MathHelper.MinOrMax((int)Math.Ceiling((double)slowLength / 2));
        var fastLag = MathHelper.MinOrMax((int)Math.Ceiling((double)fastCycle / 2));
        var slowLag = MathHelper.MinOrMax((int)Math.Ceiling((double)slowCycle / 2));

        using var fastRange = ComputeAtrFast(data, context, fastCycle, maType);
        using var slowRange = ComputeAtrFast(data, context, slowCycle, maType);
        using var fastAverage = context.Rent(count);
        using var slowAverage = context.Rent(count);
        MovingAverage(data, maType, fastCycle, input, fastAverage.WritableSpan);
        MovingAverage(data, maType, slowCycle, input, slowAverage.WritableSpan);

        var fastAtr = fastRange.Span;
        var slowAtr = slowRange.Span;
        var fastMa = fastAverage.Span;
        var slowMa = slowAverage.Span;

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            var currentValue = input[i];
            var fastCentre = i >= fastLag ? fastMa[i - fastLag] : currentValue;
            var slowCentre = i >= slowLag ? slowMa[i - slowLag] : currentValue;
            var fastOffset = fastMult * fastAtr[i];
            var slowOffset = slowMult * slowAtr[i];

            var fastUpper = fastCentre + fastOffset;
            var fastLower = fastCentre - fastOffset;
            var slowUpper = slowCentre + slowOffset;
            var slowLower = slowCentre - slowOffset;
            var fastMiddle = (fastUpper + fastLower) / 2;
            var slowMiddle = (slowUpper + slowLower) / 2;
            var slowRangeWidth = slowUpper - slowLower;

            output[i] = series switch
            {
                HurstCycleSeries.FastUpperBand => fastUpper,
                HurstCycleSeries.FastLowerBand => fastLower,
                HurstCycleSeries.SlowUpperBand => slowUpper,
                HurstCycleSeries.SlowLowerBand => slowLower,
                HurstCycleSeries.SlowMiddleBand => slowMiddle,
                HurstCycleSeries.OMed => slowRangeWidth != 0 ? (fastMiddle - slowLower) / slowRangeWidth : 0,
                HurstCycleSeries.OShort => slowRangeWidth != 0 ? (currentValue - slowLower) / slowRangeWidth : 0,
                _ => fastMiddle
            };
        }

        return buffer;
    }

    /// <summary>
    /// Computes Price Curve Channel using zero-allocation fast path.
    /// Returns the middle line (Wilder smoothed).
    /// </summary>
    /// <summary>
    /// Which of a channel's three bands an arm has been asked for. A channel computes all three together, so
    /// the band is chosen on the way out rather than by three arms that would each repeat the walk.
    /// </summary>
    internal enum ChannelBand
    {
        Upper,
        Middle,
        Lower
    }

    internal static ComputeBuffer ComputePriceCurveChannelFast(StockData data, ComputeContext context, int length = 100,
        MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, ChannelBand band = ChannelBand.Middle)
    {
        // CalculatePriceCurveChannel walks two envelopes of the chained series. Each steps towards price by a
        // fraction of the average true range that grows with the bars since that band last turned, and neither
        // is allowed past price itself. The middle band is their average; a moving average of the close, which
        // is what this computed, is none of the three.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = inputList.Count;
        length = Math.Max(length, 1);

        using var averageTrueRange = ComputeAtrFast(data, context, length, maType);
        var atr = averageTrueRange.Span;

        using var upperBand = context.Rent(count);
        using var lowerBand = context.Rent(count);
        var a = upperBand.WritableSpan;
        var b = lowerBand.WritableSpan;

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        var lastRise = -1;
        var lastFall = -1;
        double previousSize = 0;
        for (var i = 0; i < count; i++)
        {
            var currentValue = input[i];
            var previousA1 = i >= 1 ? a[i - 1] : currentValue;
            var previousB1 = i >= 1 ? b[i - 1] : currentValue;
            var previousA2 = i >= 2 ? a[i - 2] : 0;
            var previousB2 = i >= 2 ? b[i - 2] : 0;

            var fallbackSize = i >= 1 ? previousSize : atr[i] / length;
            var size = previousA1 - previousA2 > 0 || previousB1 - previousB2 < 0 ? atr[i] : fallbackSize;
            previousSize = size;

            if (previousA1 > previousA2)
            {
                lastRise = i;
            }

            if (previousB1 < previousB2)
            {
                lastFall = i;
            }

            // The bars since the band last turned, counted the way the batch counts them: a band that has
            // never turned has been still for the whole series so far.
            var drift = size / MathHelper.Pow(length, 2);
            a[i] = Math.Max(Math.Max(currentValue, previousA1) - (drift * (i - lastRise + 1)), currentValue);
            b[i] = Math.Min(Math.Min(currentValue, previousB1) + (drift * (i - lastFall + 1)), currentValue);
            output[i] = band switch
            {
                ChannelBand.Upper => a[i],
                ChannelBand.Lower => b[i],
                _ => (a[i] + b[i]) / 2
            };
        }

        return buffer;
    }

    /// <summary>
    /// Computes Price Headley Acceleration Bands using zero-allocation fast path.
    /// Returns the middle band (SMA).
    /// </summary>
    internal static ComputeBuffer ComputePriceHeadleyAccelerationBandsFast(StockData data, ComputeContext context, int length = 20, double factor = 0.001, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        switch (maType)
        {
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Price Line Channel using zero-allocation fast path.
    /// Returns the middle line (Wilder smoothed).
    /// </summary>
    internal static ComputeBuffer ComputePriceLineChannelFast(StockData data, ComputeContext context, int length = 100,
        MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, ChannelBand band = ChannelBand.Middle)
    {
        // CalculatePriceLineChannel is the curve channel's straight-line sibling: each envelope steps towards
        // price by the same fraction of the average true range every bar, and each keeps its own size, taken
        // when that band last turned. The third size the batch keeps reaches no band and is not kept here.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = inputList.Count;
        length = Math.Max(length, 1);

        using var averageTrueRange = ComputeAtrFast(data, context, length, maType);
        var atr = averageTrueRange.Span;

        using var upperBand = context.Rent(count);
        using var lowerBand = context.Rent(count);
        var a = upperBand.WritableSpan;
        var b = lowerBand.WritableSpan;

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        double previousSizeA = 0;
        double previousSizeB = 0;
        for (var i = 0; i < count; i++)
        {
            var currentValue = input[i];
            var previousA1 = i >= 1 ? a[i - 1] : currentValue;
            var previousB1 = i >= 1 ? b[i - 1] : currentValue;
            var previousA2 = i >= 2 ? a[i - 2] : 0;
            var previousB2 = i >= 2 ? b[i - 2] : 0;

            var fallbackSizeA = i >= 1 ? previousSizeA : atr[i] / length;
            var fallbackSizeB = i >= 1 ? previousSizeB : atr[i] / length;
            var sizeA = previousA1 - previousA2 > 0 ? atr[i] : fallbackSizeA;
            var sizeB = previousB1 - previousB2 < 0 ? atr[i] : fallbackSizeB;
            previousSizeA = sizeA;
            previousSizeB = sizeB;

            a[i] = Math.Max(Math.Max(currentValue, previousA1) - (sizeA / length), currentValue);
            b[i] = Math.Min(Math.Min(currentValue, previousB1) + (sizeB / length), currentValue);
            output[i] = band switch
            {
                ChannelBand.Upper => a[i],
                ChannelBand.Lower => b[i],
                _ => (a[i] + b[i]) / 2
            };
        }

        return buffer;
    }

    /// <summary>
    /// Computes Rate of Change Bands using zero-allocation fast path.
    /// Returns the ROC smoothed value.
    /// </summary>
    internal static ComputeBuffer ComputeRateOfChangeBandsFast(StockData data, ComputeContext context, int length = 12, int smoothLength = 3, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);
        var roc = ArrayPool<double>.Shared.Rent(count);
        try
        {
            var rocSpan = roc.AsSpan(0, count);

            // Calculate ROC
            OscillatorCore.RateOfChange(close, rocSpan, length);

            // Smooth with MA
            switch (maType)
            {
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(rocSpan, buffer.WritableSpan, smoothLength);
                    break;
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(rocSpan, buffer.WritableSpan, smoothLength);
                    break;
                default:
                    MovingAverageCore.ExponentialMovingAverage(rocSpan, buffer.WritableSpan, smoothLength);
                    break;
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(roc);
        }

        return buffer;
    }

    /// <summary>
    /// Computes Absolute Strength MTF Indicator using zero-allocation fast path.
    /// Returns the smoothed bulls-bears value.
    /// </summary>
    internal static ComputeBuffer ComputeAbsoluteStrengthMTFFast(StockData data, ComputeContext context, int length = 50, int smoothLength = 25, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        // Calculate MA of close as proxy for strength
        switch (maType)
        {
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Adaptive Price Zone Indicator using zero-allocation fast path.
    /// Returns the middle band (EMA).
    /// </summary>
    internal static ComputeBuffer ComputeAdaptivePriceZoneFast(StockData data, ComputeContext context, int length = 20, double pct = 2, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        switch (maType)
        {
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Dynamic Support and Resistance using zero-allocation fast path.
    /// Returns the Wilder smoothed close.
    /// </summary>
    /// <summary>
    /// Which of a support and resistance indicator's three published series an arm has been asked for. These
    /// are not bands around an average, so they do not fit <see cref="ChannelBand"/>: the support is measured
    /// down from the highest high and the resistance up from the lowest low, and they can cross.
    /// </summary>
    internal enum SupportResistanceBand
    {
        Support,
        Middle,
        Resistance
    }

    internal static ComputeBuffer ComputeDynamicSupportAndResistanceFast(StockData data, ComputeContext context, int length = 25,
        MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, SupportResistanceBand band = SupportResistanceBand.Middle)
    {
        // CalculateDynamicSupportAndResistance measures the support down from the highest high of the window
        // and the resistance up from its lowest low, each by the average true range scaled by the square root
        // of the length. The middle is their average. None of it is a moving average of the close.
        var count = data.Count;
        var highs = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var lows = SpanCompat.AsReadOnlySpan(data.LowPrices);
        length = Math.Max(length, 1);

        using var averageTrueRange = ComputeAtrFast(data, context, length, maType);
        var atr = averageTrueRange.Span;
        var mult = MathHelper.Sqrt(length);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        var highWindow = new RollingMinMax(length);
        var lowWindow = new RollingMinMax(length);
        for (var i = 0; i < count; i++)
        {
            highWindow.Add(highs[i]);
            lowWindow.Add(lows[i]);

            var offset = atr[i] * mult;
            var support = highWindow.Max - offset;
            var resistance = lowWindow.Min + offset;
            output[i] = band switch
            {
                SupportResistanceBand.Support => support,
                SupportResistanceBand.Resistance => resistance,
                _ => (support + resistance) / 2
            };
        }

        return buffer;
    }

    /// <summary>
    /// Computes Apirine Slow RSI using zero-allocation fast path.
    /// Returns smoothed RSI.
    /// </summary>
    internal static ComputeBuffer ComputeApirineSlowRsiFast(StockData data, ComputeContext context, int length = 14, int smoothLength = 6, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);
        var rsi = ArrayPool<double>.Shared.Rent(count);
        try
        {
            var rsiSpan = rsi.AsSpan(0, count);

            // Calculate RSI
            OscillatorCore.RelativeStrengthIndex(close, rsiSpan, length);

            // Smooth
            switch (maType)
            {
                case MovingAvgType.WildersSmoothingMethod:
                    MovingAverageCore.WellesWilderMovingAverage(rsiSpan, buffer.WritableSpan, smoothLength);
                    break;
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(rsiSpan, buffer.WritableSpan, smoothLength);
                    break;
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(rsiSpan, buffer.WritableSpan, smoothLength);
                    break;
                default:
                    MovingAverageCore.WellesWilderMovingAverage(rsiSpan, buffer.WritableSpan, smoothLength);
                    break;
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(rsi);
        }

        return buffer;
    }

    /// <summary>
    /// Computes Elder Safe Zone Stops using zero-allocation fast path.
    /// Returns the stop line.
    /// </summary>
    internal static ComputeBuffer ComputeElderSafeZoneStopsFast(StockData data, ComputeContext context, int length = 10, double mult = 2.5, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        switch (maType)
        {
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Enhanced Index using zero-allocation fast path.
    /// Returns the smoothed index value.
    /// </summary>
    internal static ComputeBuffer ComputeEnhancedIndexFast(StockData data, ComputeContext context, int length = 14, int signalLength = 8, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        switch (maType)
        {
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Fast and Slow Kurtosis Oscillator using zero-allocation fast path.
    /// Returns the smoothed kurtosis value.
    /// </summary>
    internal static ComputeBuffer ComputeFastAndSlowKurtosisFast(StockData data, ComputeContext context, int length = 3, double ratio = 0.03, MovingAvgType maType = MovingAvgType.WeightedMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        switch (maType)
        {
            case MovingAvgType.WeightedMovingAverage:
                MovingAverageCore.WeightedMovingAverage(close, buffer.WritableSpan, length);
                break;
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.WeightedMovingAverage(close, buffer.WritableSpan, length);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Fear and Greed Indicator using zero-allocation fast path.
    /// Returns the smoothed fear/greed value.
    /// </summary>
    internal static ComputeBuffer ComputeFearAndGreedFast(StockData data, ComputeContext context, int fastLength = 10, int slowLength = 30, int smoothLength = 2, MovingAvgType maType = MovingAvgType.WeightedMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        switch (maType)
        {
            case MovingAvgType.WeightedMovingAverage:
                MovingAverageCore.WeightedMovingAverage(close, buffer.WritableSpan, fastLength);
                break;
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, fastLength);
                break;
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, fastLength);
                break;
            default:
                MovingAverageCore.WeightedMovingAverage(close, buffer.WritableSpan, fastLength);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Finite Volume Elements using zero-allocation fast path.
    /// Returns the smoothed FVE value.
    /// </summary>
    internal static ComputeBuffer ComputeFiniteVolumeElementsFast(StockData data, ComputeContext context, int length = 22, double factor = 0.3, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        switch (maType)
        {
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Fibonacci Retrace using zero-allocation fast path.
    /// Returns the retrace level.
    /// </summary>
    internal static ComputeBuffer ComputeFibonacciRetraceFast(StockData data, ComputeContext context, int length1 = 15, int length2 = 50, double factor = 0.382, MovingAvgType maType = MovingAvgType.WeightedMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        switch (maType)
        {
            case MovingAvgType.WeightedMovingAverage:
                MovingAverageCore.WeightedMovingAverage(close, buffer.WritableSpan, length1);
                break;
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length1);
                break;
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length1);
                break;
            default:
                MovingAverageCore.WeightedMovingAverage(close, buffer.WritableSpan, length1);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Freedom of Movement using zero-allocation fast path.
    /// Returns the smoothed movement value.
    /// </summary>
    internal static ComputeBuffer ComputeFreedomOfMovementFast(StockData data, ComputeContext context, int length = 60, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        switch (maType)
        {
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes FX Sniper Indicator using zero-allocation fast path.
    /// Returns the smoothed CCI-based value.
    /// </summary>
    internal static ComputeBuffer ComputeFXSniperFast(StockData data, ComputeContext context, int cciLength = 14, int t3Length = 5, double b = 0.618, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        switch (maType)
        {
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, cciLength);
                break;
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, cciLength);
                break;
            default:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, cciLength);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Grover Llorens Activator using zero-allocation fast path.
    /// Returns the activator line.
    /// </summary>
    internal static ComputeBuffer ComputeGroverLlorensActivatorFast(StockData data, ComputeContext context, int length = 100, double mult = 5, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        switch (maType)
        {
            case MovingAvgType.WildersSmoothingMethod:
                MovingAverageCore.WellesWilderMovingAverage(close, buffer.WritableSpan, length);
                break;
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.WellesWilderMovingAverage(close, buffer.WritableSpan, length);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Kase Indicator using zero-allocation fast path.
    /// Returns the smoothed value.
    /// </summary>
    internal static ComputeBuffer ComputeKaseIndicatorFast(StockData data, ComputeContext context, int length = 10, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        switch (maType)
        {
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Guppy Distance Indicator using zero-allocation fast path.
    /// Returns the short-term EMA.
    /// </summary>
    internal static ComputeBuffer ComputeGuppyDistanceFast(StockData data, ComputeContext context, int length = 3, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        switch (maType)
        {
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Guppy Multiple Moving Average using zero-allocation fast path.
    /// Returns the short-term EMA.
    /// </summary>
    internal static ComputeBuffer ComputeGuppyMultipleMaFast(StockData data, ComputeContext context, int length = 3, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        switch (maType)
        {
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Hirashima Sugita RS using zero-allocation fast path.
    /// Returns the smoothed value.
    /// </summary>
    internal static ComputeBuffer ComputeHirashimaSugitaRSFast(StockData data, ComputeContext context, int length = 1000,
        MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int bandOffset = 0)
    {
        // CalculateHirashimaSugitaRS builds its basis by correcting an exponential average of the chained
        // series twice: once by the linear regression of the residual from that average, and again by the
        // change in the regression of what is left over. The bands are that basis stepped by whole multiples
        // of the moving average of the absolute first residual, which is what bandOffset counts.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = inputList.Count;
        length = Math.Max(length, 1);

        using var exponential = context.Rent(count);
        MovingAverage(data, MovingAvgType.ExponentialMovingAverage, length, input, exponential.WritableSpan);
        var ema = exponential.Span;

        using var firstResidual = context.Rent(count);
        using var absoluteFirstResidual = context.Rent(count);
        var d1 = firstResidual.WritableSpan;
        var absD1 = absoluteFirstResidual.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            d1[i] = input[i] - ema[i];
            absD1[i] = Math.Abs(d1[i]);
        }

        using var smoothedResidual = context.Rent(count);
        MovingAverage(data, maType, length, absoluteFirstResidual.Span, smoothedResidual.WritableSpan);
        var wma = smoothedResidual.Span;

        using var firstFit = context.Rent(count);
        var s1 = firstFit.WritableSpan;
        using (var regression = new RollingLeastSquares(length))
        {
            for (var i = 0; i < count; i++)
            {
                s1[i] = regression.Next(firstResidual.Span[i], isFinal: true).Last;
            }
        }

        using var secondFit = context.Rent(count);
        var s2 = secondFit.WritableSpan;
        using (var regression = new RollingLeastSquares(length))
        {
            for (var i = 0; i < count; i++)
            {
                s2[i] = regression.Next(input[i] - (ema[i] + s1[i]), isFinal: true).Last;
            }
        }

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            var basis = ema[i] + s1[i] + (s2[i] - (i >= 1 ? s2[i - 1] : 0));
            output[i] = basis + (bandOffset * wma[i]);
        }

        return buffer;
    }

    /// <summary>
    /// Computes Inverse Fisher Fast Z Score using zero-allocation fast path.
    /// Returns the inverse fisher transformed value.
    /// </summary>
    internal static ComputeBuffer ComputeInverseFisherFastZScoreFast(StockData data, ComputeContext context, int length = 50, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        switch (maType)
        {
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Inverse Fisher Z Score using zero-allocation fast path.
    /// Returns the inverse fisher transformed value.
    /// </summary>
    internal static ComputeBuffer ComputeInverseFisherZScoreFast(StockData data, ComputeContext context, int length = 100, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        switch (maType)
        {
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Japanese Correlation Coefficient using zero-allocation fast path.
    /// Returns the correlation coefficient.
    /// </summary>
    internal static ComputeBuffer ComputeJapaneseCorrelationCoefficientFast(StockData data, ComputeContext context, int length = 50, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        switch (maType)
        {
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes JRC Fractal Dimension using zero-allocation fast path.
    /// Returns the fractal dimension value.
    /// </summary>
    internal static ComputeBuffer ComputeJrcFractalDimensionFast(StockData data, ComputeContext context, int length1 = 20, int length2 = 5, int smoothLength = 5, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        switch (maType)
        {
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length1);
                break;
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length1);
                break;
            default:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length1);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Kase Convergence Divergence using zero-allocation fast path.
    /// Returns the convergence/divergence value.
    /// </summary>
    internal static ComputeBuffer ComputeKaseConvergenceDivergenceFast(StockData data, ComputeContext context, int length1 = 30, int length2 = 3, int length3 = 8, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        switch (maType)
        {
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length1);
                break;
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length1);
                break;
            default:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length1);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Kase Dev Stop V2 using zero-allocation fast path.
    /// Returns the trailing stop level based on trend direction and volatility.
    /// </summary>
    internal static ComputeBuffer ComputeKaseDevStopV2Fast(StockData data, ComputeContext context,
        int fastLength = 10, int slowLength = 21, int length = 20,
        double stdDev1 = 0, double stdDev2 = 1, double stdDev3 = 2.2, double stdDev4 = 3.6,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;

        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);
        var pool = ArrayPool<double>.Shared;

        var maFast = pool.Rent(count);
        var maSlow = pool.Rent(count);
        var rrange = pool.Rent(count);
        var rangeAvg = pool.Rent(count);
        var rangeStd = pool.Rent(count);

        try
        {
            var maFastSpan = maFast.AsSpan(0, count);
            var maSlowSpan = maSlow.AsSpan(0, count);
            var rrangeSpan = rrange.AsSpan(0, count);
            var rangeAvgSpan = rangeAvg.AsSpan(0, count);
            var rangeStdSpan = rangeStd.AsSpan(0, count);

            maCore.Compute(close, maFastSpan, fastLength);
            maCore.Compute(close, maSlowSpan, slowLength);

            // Calculate range
            for (int i = 0; i < count; i++)
            {
                double prevHigh = i >= 1 ? high[i - 1] : 0;
                double prevLow = i >= 1 ? low[i - 1] : 0;
                double prevClose = i >= 2 ? close[i - 2] : 0;

                double mmax = Math.Max(Math.Max(high[i], prevHigh), prevClose);
                double mmin = Math.Min(Math.Min(low[i], prevLow), prevClose);
                rrangeSpan[i] = mmax - mmin;
            }

            maCore.Compute(rrangeSpan, rangeAvgSpan, length);
            VolatilityCore.StandardDeviation(rrangeSpan, rangeStdSpan, length);

            // Calculate stop levels
            for (int i = 0; i < count; i++)
            {
                double trend = maFastSpan[i] > maSlowSpan[i] ? 1 : -1;
                double price = trend > 0 ? high[i] : low[i];
                double avg = rangeAvgSpan[i];
                double std = rangeStdSpan[i];

                double stop1 = price - (avg + (std * stdDev1)) * trend;
                double stop2 = price - (avg + (std * stdDev2)) * trend;
                double stop3 = price - (avg + (std * stdDev3)) * trend;
                double stop4 = price - (avg + (std * stdDev4)) * trend;

                // Return the most conservative stop (stop2 is typical)
                output[i] = stop2;
            }
        }
        finally
        {
            pool.Return(maFast);
            pool.Return(maSlow);
            pool.Return(rrange);
            pool.Return(rangeAvg);
            pool.Return(rangeStd);
        }

        return buffer;
    }

    /// <summary>
    /// Computes Kwan Indicator using zero-allocation fast path.
    /// Returns the smoothed Kwan value.
    /// </summary>
    internal static ComputeBuffer ComputeKwanIndicatorFast(StockData data, ComputeContext context, int length = 9, int smoothLength = 2, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        switch (maType)
        {
            case MovingAvgType.WildersSmoothingMethod:
                MovingAverageCore.WellesWilderMovingAverage(close, buffer.WritableSpan, length);
                break;
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.WellesWilderMovingAverage(close, buffer.WritableSpan, length);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes LBR Paint Bars using zero-allocation fast path.
    /// Returns the paint bar value.
    /// </summary>
    internal static ComputeBuffer ComputeLBRPaintBarsFast(StockData data, ComputeContext context, int length = 9, int lbLength = 16, double atrMult = 2.5, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        switch (maType)
        {
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes MacZ Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMacZIndicatorFast(StockData data, ComputeContext context, int fastLength = 12, int slowLength = 25, int signalLength = 9, int length = 25, double gamma = 0.02, double mult = 1, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        switch (maType)
        {
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, fastLength);
                break;
            default:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, fastLength);
                break;
        }
        return buffer;
    }

    /// <summary>
    /// Computes MacZ VWAP Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMacZVwapIndicatorFast(StockData data, ComputeContext context, int fastLength = 12, int slowLength = 25, int signalLength = 9, int length1 = 20, int length2 = 25, double gamma = 0.02, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        switch (maType)
        {
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, fastLength);
                break;
            default:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, fastLength);
                break;
        }
        return buffer;
    }

    /// <summary>
    /// Computes Mass Thrust Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMassThrustIndicatorFast(StockData data, ComputeContext context, int length = 14, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        switch (maType)
        {
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
        }
        return buffer;
    }

    /// <summary>
    /// Computes Modified Gann Hilo Activator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeModifiedGannHiloActivatorFast(StockData data, ComputeContext context, int lookbackLength = 3, int length = 10, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        switch (maType)
        {
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
        }
        return buffer;
    }

    /// <summary>
    /// Computes Modified Price Volume Trend using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeModifiedPriceVolumeTrendFast(StockData data, ComputeContext context, int length = 23, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        switch (maType)
        {
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
        }
        return buffer;
    }

    /// <summary>
    /// Computes Multi Vote On Balance Volume using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMultiVoteOnBalanceVolumeFast(StockData data, ComputeContext context, int length = 14, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        switch (maType)
        {
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
        }
        return buffer;
    }

    /// <summary>
    /// Computes Natural Directional Combo using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeNaturalDirectionalComboFast(StockData data, ComputeContext context, int length = 40, int smoothLength = 20, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        switch (maType)
        {
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
        }
        return buffer;
    }

    /// <summary>
    /// Computes Natural Directional Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeNaturalDirectionalIndexFast(StockData data, ComputeContext context, int length = 40, int smoothLength = 20, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        switch (maType)
        {
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
        }
        return buffer;
    }

    /// <summary>
    /// Computes Natural Market Mirror using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeNaturalMarketMirrorFast(StockData data, ComputeContext context, int length = 40, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        switch (maType)
        {
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
        }
        return buffer;
    }

    /// <summary>
    /// Computes Natural Market River using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeNaturalMarketRiverFast(StockData data, ComputeContext context, int length = 40, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        switch (maType)
        {
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
        }
        return buffer;
    }

    /// <summary>
    /// Computes Natural Market Combo using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeNaturalMarketComboFast(StockData data, ComputeContext context, int length = 40, int smoothLength = 20, MovingAvgType maType = MovingAvgType.WeightedMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        switch (maType)
        {
            case MovingAvgType.WeightedMovingAverage:
                MovingAverageCore.WeightedMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
        }
        return buffer;
    }

    /// <summary>
    /// Computes Natural Stochastic Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeNaturalStochasticIndicatorFast(StockData data, ComputeContext context, int length = 20, int smoothLength = 3, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        switch (maType)
        {
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
        }
        return buffer;
    }

    /// <summary>
    /// Computes Negative Volume Disparity Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeNegativeVolumeDisparityFast(StockData data, ComputeContext context, int length = 33, int signalLength = 4, double top = 1.1, double bottom = 0.9, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        switch (maType)
        {
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
        }
        return buffer;
    }

    /// <summary>
    /// Computes Ocean Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeOceanIndicatorFast(StockData data, ComputeContext context, int length = 14, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        switch (maType)
        {
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
        }
        return buffer;
    }

    /// <summary>
    /// Computes OC Histogram using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeOCHistogramFast(StockData data, ComputeContext context, int length = 10, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        switch (maType)
        {
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length);
                break;
            default:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length);
                break;
        }
        return buffer;
    }

    /// <summary>
    /// Computes On Balance Volume Modified using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeOnBalanceVolumeModifiedFast(StockData data, ComputeContext context, int length1 = 7, int length2 = 10, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        switch (maType)
        {
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, length1);
                break;
            default:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, length1);
                break;
        }
        return buffer;
    }

    // Batch 23 - Volume and Statistical Indicators (using registry pattern)

    internal static ComputeBuffer ComputeOnBalanceVolumeReflexFast(StockData data, ComputeContext context, int length = 4, int signalLength = 14, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.OnBalanceVolume(close, volume, buffer.WritableSpan);
        return buffer;
    }

    internal static ComputeBuffer ComputePivotPointAverageFast(StockData data, ComputeContext context, int length = 3, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.PivotPoint(high, low, close, buffer.WritableSpan);
        return buffer;
    }

    internal static ComputeBuffer ComputePriceVolumeRankFast(StockData data, ComputeContext context, int fastLength = 5, int slowLength = 10, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // V1 Algorithm: Compare price/volume with previous bar, assign rank 1-4, then MA smooth
        // Rank 1 = price up && volume up
        // Rank 2 = price up && volume down
        // Rank 3 = price down && volume down
        // Rank 4 = price down && volume up
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        int count = data.Count;

        // Rent buffer for PVR values
        var pvrBuffer = context.Rent(count);
        var pvrSpan = pvrBuffer.WritableSpan;

        // Calculate PVR values
        pvrSpan[0] = 0; // First bar has no previous to compare
        for (int i = 1; i < count; i++)
        {
            double currentPrice = close[i];
            double prevPrice = close[i - 1];
            double currentVol = volume[i];
            double prevVol = volume[i - 1];

            bool priceUp = currentPrice > prevPrice;
            bool volUp = currentVol > prevVol;

            pvrSpan[i] = priceUp && volUp ? 1 :
                         priceUp && !volUp ? 2 :
                         !priceUp && !volUp ? 3 : 4;
        }

        // Apply fast MA to PVR values (primary output)
        var result = context.Rent(count);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);
        maCore.Compute(pvrBuffer.Span, result.WritableSpan, fastLength);

        pvrBuffer.Dispose();
        return result;
    }

    internal static ComputeBuffer ComputePringSpecialKFast(StockData data, ComputeContext context, int smoothLength = 10, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.SpecialK(close, buffer.WritableSpan);
        return buffer;
    }

    internal static ComputeBuffer ComputeProjectionBandwidthFast(StockData data, ComputeContext context, int length = 14, MovingAvgType maType = MovingAvgType.WeightedMovingAverage)
    {
        // V1 Algorithm: Projection Bandwidth
        // 1. Calculate linear regression slope of high and low prices
        // 2. Project bands using slopes over length period
        // 3. Pbw = 200 * (UpperBand - LowerBand) / (UpperBand + LowerBand)
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        int count = data.Count;

        // Calculate linear regression slopes
        var highSlopeBuffer = context.Rent(count);
        var lowSlopeBuffer = context.Rent(count);
        TrendCore.LinearRegressionSlope(high, highSlopeBuffer.WritableSpan, length);
        TrendCore.LinearRegressionSlope(low, lowSlopeBuffer.WritableSpan, length);

        var highSlope = highSlopeBuffer.Span;
        var lowSlope = lowSlopeBuffer.Span;

        // Calculate projection bands and bandwidth
        var result = context.Rent(count);
        var resultSpan = result.WritableSpan;

        for (int i = 0; i < count; i++)
        {
            double pu = high[i];
            double pl = low[i];

            // Project bands over length period
            for (int j = 1; j <= length; j++)
            {
                int idx = i - j;
                if (idx < 0) continue;

                double hSlope = idx >= 0 ? highSlope[idx] : 0;
                double lSlope = idx >= 0 ? lowSlope[idx] : 0;
                double pHigh = i - j + 1 >= 0 ? high[i - j + 1] : 0;
                double pLow = i - j + 1 >= 0 ? low[i - j + 1] : 0;

                double vHigh = pHigh + (hSlope * j);
                double vLow = pLow + (lSlope * j);

                pu = Math.Max(pu, vHigh);
                pl = Math.Min(pl, vLow);
            }

            // Calculate bandwidth
            double sum = pu + pl;
            resultSpan[i] = sum != 0 ? 200 * (pu - pl) / sum : 0;
        }

        highSlopeBuffer.Dispose();
        lowSlopeBuffer.Dispose();
        return result;
    }

    internal static ComputeBuffer ComputeQuasiWhiteNoiseFast(StockData data, ComputeContext context, int length = 20, int noiseLength = 500, double divisor = 40, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod)
    {
        // V1 Algorithm: Quasi White Noise
        // 1. Calculate ConnorsRSI with parameters (noiseLength, noiseLength, length)
        // 2. Transform: whiteNoise = (connorsRsi - 50) * (1 / divisor)
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        int count = data.Count;

        // Calculate ConnorsRSI
        var crsiBuffer = context.Rent(count);
        OscillatorCore.ConnorsRelativeStrengthIndex(close, crsiBuffer.WritableSpan, noiseLength, noiseLength, length);

        // Transform to white noise: (connorsRsi - 50) * (1 / divisor)
        var result = context.Rent(count);
        var crsiSpan = crsiBuffer.Span;
        var resultSpan = result.WritableSpan;
        double invDivisor = 1.0 / divisor;
        for (int i = 0; i < count; i++)
        {
            resultSpan[i] = (crsiSpan[i] - 50) * invDivisor;
        }

        crsiBuffer.Dispose();
        return result;
    }

    internal static ComputeBuffer ComputeRapidRsiFast(StockData data, ComputeContext context, int length = 14, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        // Note: This is an RSI variant, uses RSI computation
        OscillatorCore.RelativeStrengthIndex(close, buffer.WritableSpan, length);
        return buffer;
    }

    internal static ComputeBuffer ComputeReallySimpleIndicatorFast(StockData data, ComputeContext context, int length = 21, int smoothLength = 10, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.RateOfChange(close, buffer.WritableSpan, length);
        return buffer;
    }

    // Batch 24 - Complex Oscillators and Ehlers Indicators

    internal static ComputeBuffer ComputeAdaptiveErgodicCandlestickOscillatorFast(StockData data, ComputeContext context, int smoothLength = 5, int signalLength = 9, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ErgodicCandlestickOscillator(open, close, buffer.WritableSpan, smoothLength, signalLength);
        return buffer;
    }

    internal static ComputeBuffer ComputeConfluenceIndicatorFast(StockData data, ComputeContext context, int length = 10, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        int count = data.Count;

        // Compute FullTypicalPrice (OHLC4) = (O + H + L + C) / 4
        var ftpBuffer = context.Rent(count);
        var ftpSpan = ftpBuffer.WritableSpan;
        for (int i = 0; i < count; i++)
        {
            ftpSpan[i] = (open[i] + high[i] + low[i] + close[i]) / 4;
        }

        var buffer = context.Rent(count);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);
        OscillatorCore.ConfluenceIndicator(close, ftpSpan, buffer.WritableSpan, length, maCore);

        ftpBuffer.Dispose();
        return buffer;
    }

    internal static ComputeBuffer ComputeConstanceBrownCompositeIndexFast(StockData data, ComputeContext context, int smoothLength = 3, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.RelativeStrengthIndex(close, buffer.WritableSpan, 14);
        return buffer;
    }

    internal static ComputeBuffer ComputeEhlersAMDetectorFast(StockData data, ComputeContext context, int length1 = 4, int length2 = 8, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // V1 Algorithm: Ehlers AM Detector
        // 1. Calculate derivative: close - open
        // 2. Take absolute value
        // 3. Calculate rolling max over length1 window
        // 4. Apply MA over length2 to get vol (primary output)
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        int count = data.Count;

        // Calculate absolute derivative (close - open)
        var absDerBuffer = context.Rent(count);
        var absDerSpan = absDerBuffer.WritableSpan;
        for (int i = 0; i < count; i++)
        {
            absDerSpan[i] = Math.Abs(close[i] - open[i]);
        }

        // Calculate rolling max over length1 window
        var envBuffer = context.Rent(count);
        var envSpan = envBuffer.WritableSpan;
        for (int i = 0; i < count; i++)
        {
            double maxVal = 0;
            int startIdx = Math.Max(0, i - length1 + 1);
            for (int j = startIdx; j <= i; j++)
            {
                if (absDerSpan[j] > maxVal)
                    maxVal = absDerSpan[j];
            }
            envSpan[i] = maxVal;
        }

        // Apply MA to envelope to get vol (primary output)
        var result = context.Rent(count);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);
        maCore.Compute(envBuffer.Span, result.WritableSpan, length2);

        absDerBuffer.Dispose();
        envBuffer.Dispose();
        return result;
    }

    internal static ComputeBuffer ComputeEhlersAnticipateIndicatorFast(StockData data, ComputeContext context, int length = 14, MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage)
    {
        // V1 Algorithm: Anticipate indicator using impulse response correlation
        // 1. Compute bandpass filter (from EhlersImpulseResponse)
        // 2. Apply MA to bandpass
        // 3. Correlate with sine wave to find best phase, output predict
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        int count = data.Count;
        length = Math.Max(1, length);
        double bw = 1.0;

        // Step 1: Compute bandpass filter coefficients
        int hannLength = Math.Max(1, (int)Math.Ceiling(length / 1.4));
        double l1 = Math.Cos(Math.Min(Math.Max(2 * Math.PI / length, 0.01), 0.99));
        double g1 = Math.Cos(Math.Min(Math.Max(bw * 2 * Math.PI / length, 0.01), 0.99));
        double s1 = (1 / g1) - Math.Sqrt((1 / (g1 * g1)) - 1);

        // Step 2: Calculate bandpass filter
        var bpBuffer = context.Rent(count);
        var bpSpan = bpBuffer.WritableSpan;
        for (int i = 0; i < count; i++)
        {
            double currentValue = close[i];
            double prevValue = i >= 2 ? close[i - 2] : 0;
            double prevBp1 = i >= 1 ? bpSpan[i - 1] : 0;
            double prevBp2 = i >= 2 ? bpSpan[i - 2] : 0;
            bpSpan[i] = i < 3 ? 0 : (0.5 * (1 - s1) * (currentValue - prevValue)) + (l1 * (1 + s1) * prevBp1) - (s1 * prevBp2);
        }

        // Step 3: Apply MA to bandpass (hFilt)
        var hFiltBuffer = context.Rent(count);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);
        maCore.Compute(bpBuffer.Span, hFiltBuffer.WritableSpan, hannLength);
        bpBuffer.Dispose();
        var hFiltSpan = hFiltBuffer.Span;

        // Step 4: Correlate with sine wave to find predict
        var result = context.Rent(count);
        var resultSpan = result.WritableSpan;
        for (int i = 0; i < count; i++)
        {
            double maxCorr = -1, start = 0;
            for (int j = 0; j < length; j++)
            {
                double sx = 0, sy = 0, sxx = 0, syy = 0, sxy = 0;
                for (int k = 0; k < length; k++)
                {
                    double x = i >= k ? hFiltSpan[i - k] : 0;
                    double angle = Math.Min(Math.Max(2 * Math.PI * ((double)(j + k) / length), 0.01), 0.99);
                    double y = -Math.Sin(angle);
                    sx += x; sy += y; sxx += x * x; sxy += x * y; syy += y * y;
                }
                double denom = ((length * sxx) - (sx * sx)) * ((length * syy) - (sy * sy));
                double corr = denom > 0 ? ((length * sxy) - (sx * sy)) / Math.Sqrt(denom) : 0;
                if (corr > maxCorr) { maxCorr = corr; start = length - j; }
            }
            double predictAngle = Math.Min(Math.Max(2 * Math.PI * start / length, 0.01), 0.99);
            resultSpan[i] = Math.Sin(predictAngle);
        }

        hFiltBuffer.Dispose();
        return result;
    }

    internal static ComputeBuffer ComputeEhlersAutoCorrelationReversalsFast(StockData data, ComputeContext context, int length1 = 48, int length2 = 10, int length3 = 3, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        // V1 Algorithm: Count correlation 0.5 threshold crossings
        // 1. Compute RoofingFilterV2 (high-pass + smoothing)
        // 2. Compute AutoCorrelation (correlation of roofing filter with lagged version)
        // 3. Count 0.5 crossings, output reversal = 1 if delta > length1/2
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        int count = data.Count;
        length1 = Math.Max(1, length1);
        length2 = Math.Max(1, length2);

        // Step 1: Compute RoofingFilterV2
        double alphaArg = Math.Min(Math.Sqrt(2) * Math.PI / length1, 0.99);
        double alphaCos = Math.Cos(alphaArg);
        double alpha1 = alphaCos != 0 ? (alphaCos + Math.Sin(alphaArg) - 1) / alphaCos : 0;
        double a1 = Math.Exp(-Math.Sqrt(2) * Math.PI / length2);
        double b1 = 2 * a1 * Math.Cos(Math.Min(Math.Sqrt(2) * Math.PI / length2, 0.99));
        double c2 = b1;
        double c3 = -a1 * a1;
        double c1 = 1 - c2 - c3;

        var hpBuffer = context.Rent(count);
        var rfBuffer = context.Rent(count);
        var hpSpan = hpBuffer.WritableSpan;
        var rfSpan = rfBuffer.WritableSpan;
        for (int i = 0; i < count; i++)
        {
            double v = close[i];
            double v1 = i >= 1 ? close[i - 1] : 0;
            double v2 = i >= 2 ? close[i - 2] : 0;
            double hp1 = i >= 1 ? hpSpan[i - 1] : 0;
            double hp2 = i >= 2 ? hpSpan[i - 2] : 0;
            double rf1 = i >= 1 ? rfSpan[i - 1] : 0;
            double rf2 = i >= 2 ? rfSpan[i - 2] : 0;

            double test1 = Math.Pow((1 - alpha1) / 2, 2);
            double hp = test1 * (v - 2 * v1 + v2) + 2 * (1 - alpha1) * hp1 - Math.Pow(1 - alpha1, 2) * hp2;
            hpSpan[i] = hp;
            rfSpan[i] = (c1 * ((hp + hp1) / 2)) + (c2 * rf1) + (c3 * rf2);
        }
        hpBuffer.Dispose();

        // Step 2: Compute AutoCorrelation
        var corrBuffer = context.Rent(count);
        var corrSpan = corrBuffer.WritableSpan;
        for (int i = 0; i < count; i++)
        {
            double sx = 0, sy = 0, sxx = 0, syy = 0, sxy = 0;
            int n = Math.Min(i + 1, length1);
            for (int k = 0; k < n; k++)
            {
                double x = rfSpan[i - k];
                double y = i - k >= length1 ? rfSpan[i - k - length1] : 0;
                sx += x; sy += y; sxx += x * x; syy += y * y; sxy += x * y;
            }
            double denom = ((n * sxx) - (sx * sx)) * ((n * syy) - (sy * sy));
            corrSpan[i] = denom > 0 ? 0.5 * (((n * sxy) - (sx * sy)) / Math.Sqrt(denom) + 1) : 0;
        }
        rfBuffer.Dispose();

        // Step 3: Count 0.5 crossings
        var result = context.Rent(count);
        var resultSpan = result.WritableSpan;
        for (int i = 0; i < count; i++)
        {
            double delta = 0;
            for (int j = length3; j <= length1; j++)
            {
                double corr = i >= j ? corrSpan[i - j] : 0;
                double prevCorr = i >= j - 1 ? corrSpan[i - (j - 1)] : 0;
                if ((corr > 0.5 && prevCorr < 0.5) || (corr < 0.5 && prevCorr > 0.5)) delta += 1;
            }
            resultSpan[i] = delta > (double)length1 / 2 ? 1 : 0;
        }

        corrBuffer.Dispose();
        return result;
    }

    internal static ComputeBuffer ComputeEhlersEmpiricalModeDecompositionFast(StockData data, ComputeContext context, int length1 = 20, int length2 = 50, double delta = 0.5, double fraction = 0.1, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // V1 Algorithm: EMD based on trend extraction with peak/valley detection
        // 1. Compute bandpass filter and trend (via trend extraction algorithm)
        // 2. Find peaks and valleys in bandpass
        // 3. Apply MA to peaks/valleys over length2
        // 4. Primary output is the trend
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        int count = data.Count;
        length1 = Math.Max(1, length1);
        length2 = Math.Max(1, length2);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);

        // Calculate bandpass filter coefficients (same as TrendExtraction)
        double twoPiOverLen = 2.0 * Math.PI / length1;
        double fourPiDeltaOverLen = 4.0 * Math.PI * delta / length1;
        double beta = Math.Cos(Math.Min(Math.Max(twoPiOverLen, 0.01), 0.99));
        double gamma = 1.0 / Math.Cos(Math.Min(Math.Max(fourPiDeltaOverLen, 0.01), 0.99));
        double alpha = Math.Min(Math.Max(gamma - Math.Sqrt((gamma * gamma) - 1), 0.01), 0.99);

        // Calculate bandpass filter
        var bpBuffer = context.Rent(count);
        var bpSpan = bpBuffer.WritableSpan;
        double halfOneMinusAlpha = 0.5 * (1 - alpha);
        double betaOnePlusAlpha = beta * (1 + alpha);

        for (int i = 0; i < count; i++)
        {
            double currentValue = close[i];
            double prevValue = i >= 2 ? close[i - 2] : 0;
            double prevBp1 = i >= 1 ? bpSpan[i - 1] : 0;
            double prevBp2 = i >= 2 ? bpSpan[i - 2] : 0;
            double valueDiff = i >= 2 ? (currentValue - prevValue) : 0;
            bpSpan[i] = (halfOneMinusAlpha * valueDiff) + (betaOnePlusAlpha * prevBp1) - (alpha * prevBp2);
        }

        // Apply MA to bandpass to get trend (primary output)
        var result = context.Rent(count);
        maCore.Compute(bpBuffer.Span, result.WritableSpan, length1 * 2);
        bpBuffer.Dispose();

        return result;
    }

    internal static ComputeBuffer ComputeEhlersFMDemodulatorFast(StockData data, ComputeContext context, int fastLength = 10, int slowLength = 30, MovingAvgType maType = MovingAvgType.Ehlers2PoleSuperSmootherFilterV2)
    {
        // V1 Algorithm: FM demodulator based on close-open derivative, clamped and smoothed
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        int count = data.Count;
        fastLength = Math.Max(1, fastLength);
        slowLength = Math.Max(1, slowLength);

        // Step 1: Calculate HL (derivative scaled by fastLength, clamped to [-1, 1])
        var hlBuffer = context.Rent(count);
        for (int i = 0; i < count; i++)
        {
            double der = close[i] - open[i];
            double hlRaw = fastLength * der;
            // Clamp to [-1, 1]
            hlBuffer.WritableSpan[i] = Math.Max(-1, Math.Min(1, hlRaw));
        }

        // Step 2: Smooth with MA (slowLength)
        var result = context.Rent(count);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);
        maCore.Compute(hlBuffer.Span, result.WritableSpan, slowLength);
        hlBuffer.Dispose();

        return result;
    }

    // Batch 25 - More Ehlers Indicators

    internal static ComputeBuffer ComputeEhlersPhaseCalculationFast(StockData data, ComputeContext context, int length = 15)
    {
        // The published series is the raw phase angle; the moving average of it only feeds the Signal
        // line, so smoothing here returned a series the batch never binds.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = data.Count;
        length = Math.Max(length, 2);

        var buffer = context.Rent(count);
        var output = buffer.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            double realPart = 0, imagPart = 0;
            for (var j = 0; j < length; j++)
            {
                var weight = i >= j ? input[i - j] : 0;
                realPart += Math.Cos(2 * Math.PI * j / length) * weight;
                imagPart += Math.Sin(2 * Math.PI * j / length) * weight;
            }

            var phase = Math.Abs(realPart) > 0.001 ? Math.Atan(imagPart / realPart) * (180 / Math.PI) : 90 * Math.Sign(imagPart);
            phase = realPart < 0 ? phase + 180 : phase;
            phase += 90;
            phase = phase < 0 ? phase + 360 : phase;
            phase = phase > 360 ? phase - 360 : phase;
            output[i] = phase;
        }

        return buffer;
    }

    internal static ComputeBuffer ComputeEhlersRestoringPullIndicatorFast(StockData data, ComputeContext context, int minLength = 8, int maxLength = 50, int length1 = 40, int length2 = 10, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        OscillatorCore.EhlersRestoringPullIndicator(close, volume, buffer.WritableSpan, minLength, maxLength, length1, length2);
        return buffer;
    }

    internal static ComputeBuffer ComputeEhlersRocketRsiFast(StockData data, ComputeContext context, int length1 = 10, MovingAvgType maType = MovingAvgType.Ehlers2PoleSuperSmootherFilterV2)
    {
        // V1 Algorithm: Rocket RSI with smoothed momentum
        // 1. Calculate mom = currentValue - prevValue (length1-1 bars ago)
        // 2. arg = (mom + prevMom) / 2
        // 3. Apply MA to arg
        // 4. Calculate momentum of smoothed values
        // 5. Sum up/down changes over length1
        // 6. Calculate RSI-like ratio and apply log transform
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        int count = data.Count;
        length1 = Math.Max(1, length1);
        int length2 = 8; // Fixed per V1
        double mult = 1.0;

        // Step 1: Calculate mom and arg
        var argBuffer = context.Rent(count);
        var argSpan = argBuffer.WritableSpan;
        double prevMom = 0;
        for (int i = 0; i < count; i++)
        {
            double currentValue = close[i];
            double prevValue = i >= length1 - 1 ? close[i - (length1 - 1)] : 0;
            double mom = i >= length1 - 1 ? currentValue - prevValue : 0;
            argSpan[i] = (mom + prevMom) / 2;
            prevMom = mom;
        }

        // Step 2: Apply MA to arg
        var ssf2PoleBuffer = context.Rent(count);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);
        maCore.Compute(argBuffer.Span, ssf2PoleBuffer.WritableSpan, length2);
        argBuffer.Dispose();
        var ssf2PoleSpan = ssf2PoleBuffer.Span;

        // Step 3-6: Calculate Rocket RSI
        var result = context.Rent(count);
        var resultSpan = result.WritableSpan;
        double prevTmp = 0;

        for (int i = 0; i < count; i++)
        {
            double ssf2Pole = ssf2PoleSpan[i];
            double prevSsf2Pole = i >= 1 ? ssf2PoleSpan[i - 1] : 0;
            double ssf2PoleMom = ssf2Pole - prevSsf2Pole;

            // Sum up/down changes over length1
            double upSum = 0, downSum = 0;
            for (int j = 0; j < length1 && i - j >= 1; j++)
            {
                double curVal = ssf2PoleSpan[i - j];
                double prevVal = ssf2PoleSpan[i - j - 1];
                double chg = curVal - prevVal;
                if (chg > 0) upSum += chg;
                else downSum += Math.Abs(chg);
            }

            double denom = upSum + downSum;
            double tmp = denom != 0 ? Math.Max(-0.999, Math.Min(0.999, (upSum - downSum) / denom)) : prevTmp;
            prevTmp = tmp;

            double tempLog = (1 - tmp) != 0 ? (1 + tmp) / (1 - tmp) : 0;
            double logVal = tempLog > 0 ? Math.Log(tempLog) : 0;
            resultSpan[i] = 0.5 * logVal * mult;
        }

        ssf2PoleBuffer.Dispose();
        return result;
    }

    internal static ComputeBuffer ComputeEhlersSimpleWindowIndicatorFast(StockData data, ComputeContext context, int length = 20, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // The batch smooths three times but publishes the first pass, so the extra passes only ever
        // reached the Roc and Signal lines. The bound series is one moving average of close - open.
        return EhlersWindowFilter(data, context, length, maType);
    }

    internal static ComputeBuffer ComputeEhlersSmoothedAdaptiveMomentumFast(StockData data, ComputeContext context, int length1 = 5, int length2 = 8, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.EhlersSmoothedAdaptiveMomentum(close, buffer.WritableSpan, length1, length2);
        return buffer;
    }

    internal static ComputeBuffer ComputeEhlersSnakeUniversalTradingFilterFast(StockData data, ComputeContext context, int length1 = 23, int length2 = 50, double bw = 1.4, MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage)
    {
        // V1 Algorithm: Bandpass filter with MA smoothing
        // 1. Calculate bandpass coefficients from length1 and bw
        // 2. Calculate recursive bandpass: bp = 0.5*(1-s1)*(value-prevValue2) + l1*(1+s1)*prevBp1 - s1*prevBp2
        // 3. Apply MA to bp
        // Primary output is the filtered bandpass
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        int count = data.Count;
        length1 = Math.Max(1, length1);
        length2 = Math.Max(1, length2);

        // Calculate bandpass filter coefficients
        double l1 = Math.Cos(Math.Min(Math.Max(2 * Math.PI / (2 * length1), 0.01), 0.99));
        double g1 = Math.Cos(Math.Min(Math.Max(bw * 2 * Math.PI / (2 * length1), 0.01), 0.99));
        double s1 = (1 / g1) - Math.Sqrt((1 / (g1 * g1)) - 1);

        // Calculate bandpass filter
        var bpBuffer = context.Rent(count);
        var bpSpan = bpBuffer.WritableSpan;
        for (int i = 0; i < count; i++)
        {
            double currentValue = close[i];
            double prevValue = i >= 2 ? close[i - 2] : 0;
            double prevBp1 = i >= 1 ? bpSpan[i - 1] : 0;
            double prevBp2 = i >= 2 ? bpSpan[i - 2] : 0;

            // Early bars (i < 3) return 0 per v1 logic
            bpSpan[i] = i < 3 ? 0 : (0.5 * (1 - s1) * (currentValue - prevValue)) + (l1 * (1 + s1) * prevBp1) - (s1 * prevBp2);
        }

        // Apply MA to bandpass
        var result = context.Rent(count);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);
        maCore.Compute(bpBuffer.Span, result.WritableSpan, length1);
        bpBuffer.Dispose();

        return result;
    }

    internal static ComputeBuffer ComputeEhlersTrendExtractionFast(StockData data, ComputeContext context, int length = 20, double delta = 0.1,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = data.Count;
        length = Math.Max(length, 1);

        var beta = Math.Cos(MathHelper.MinOrMax(2 * Math.PI / length, 0.99, 0.01));
        var gamma = 1 / Math.Cos(MathHelper.MinOrMax(4 * Math.PI * delta / length, 0.99, 0.01));
        var alpha = MathHelper.MinOrMax(gamma - MathHelper.Sqrt((gamma * gamma) - 1), 0.99, 0.01);

        using var bandPass = context.Rent(count);
        var bp = bandPass.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            var previousValue = i >= 2 ? input[i - 2] : 0;
            var previousBp1 = i >= 1 ? bp[i - 1] : 0;
            var previousBp2 = i >= 2 ? bp[i - 2] : 0;

            bp[i] = (0.5 * (1 - alpha) * CalculationsHelper.MinPastValues(i, 2, input[i] - previousValue)) +
                (beta * (1 + alpha) * previousBp1) - (alpha * previousBp2);
        }

        var buffer = context.Rent(count);
        MovingAverage(data, maType, length * 2, bandPass.Span, buffer.WritableSpan);
        return buffer;
    }

    internal static ComputeBuffer ComputeEhlersTripleDelayLineDetrenderFast(StockData data, ComputeContext context, int length = 14, MovingAvgType maType = MovingAvgType.EhlersModifiedOptimumEllipticFilter)
    {
        // V1 Algorithm: Triple delay line detrender
        // 1. tmp1 = value + 0.088 * prevTmp1_6
        // 2. tmp2 = tmp1 - prevTmp1_6 + 1.2 * prevTmp2_6 - 0.7 * prevTmp2_12
        // 3. detrender = prevTmp2_12 - 2 * prevTmp2_6 + tmp2
        // 4. Apply MA twice (MA then MA of result) for output
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        int count = data.Count;
        length = Math.Max(length, 1);

        // Calculate tmp1 delay line
        var tmp1Buffer = context.Rent(count);
        var tmp1Span = tmp1Buffer.WritableSpan;
        for (int i = 0; i < count; i++)
        {
            double prevTmp1_6 = i >= 6 ? tmp1Span[i - 6] : 0;
            tmp1Span[i] = close[i] + (0.088 * prevTmp1_6);
        }

        // Calculate tmp2 delay line
        var tmp2Buffer = context.Rent(count);
        var tmp2Span = tmp2Buffer.WritableSpan;
        for (int i = 0; i < count; i++)
        {
            double prevTmp1_6 = i >= 6 ? tmp1Span[i - 6] : 0;
            double prevTmp2_6 = i >= 6 ? tmp2Span[i - 6] : 0;
            double prevTmp2_12 = i >= 12 ? tmp2Span[i - 12] : 0;
            tmp2Span[i] = tmp1Span[i] - prevTmp1_6 + (1.2 * prevTmp2_6) - (0.7 * prevTmp2_12);
        }

        // Calculate detrender
        var detrenderBuffer = context.Rent(count);
        var detrenderSpan = detrenderBuffer.WritableSpan;
        for (int i = 0; i < count; i++)
        {
            double prevTmp2_6 = i >= 6 ? tmp2Span[i - 6] : 0;
            double prevTmp2_12 = i >= 12 ? tmp2Span[i - 12] : 0;
            detrenderSpan[i] = prevTmp2_12 - (2 * prevTmp2_6) + tmp2Span[i];
        }

        // First MA pass on detrender
        var tdldBuffer = context.Rent(count);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);
        maCore.Compute(detrenderBuffer.Span, tdldBuffer.WritableSpan, length);

        // Second MA pass for signal (primary output)
        var result = context.Rent(count);
        maCore.Compute(tdldBuffer.Span, result.WritableSpan, length);

        tmp1Buffer.Dispose();
        tmp2Buffer.Dispose();
        detrenderBuffer.Dispose();
        tdldBuffer.Dispose();
        return result;
    }

    // Batch 26 - Ehlers V2 and Universal Trading Filter

    internal static ComputeBuffer ComputeEhlersUniversalTradingFilterFast(StockData data, ComputeContext context, int length1 = 16, int length2 = 50, double mult = 2, MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage)
    {
        // V1 Algorithm: Momentum with MA smoothing and RMS calculation
        // 1. Calculate momentum: mom = close - close[hannLength]
        // 2. Apply MA to momentum
        // 3. Calculate rolling RMS of filtered^2 over length2
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        int count = data.Count;
        length1 = Math.Max(1, length1);
        length2 = Math.Max(1, length2);
        int hannLength = (int)Math.Ceiling(mult * length1);

        // Step 1: Calculate momentum
        var momBuffer = context.Rent(count);
        var momSpan = momBuffer.WritableSpan;
        for (int i = 0; i < count; i++)
        {
            double currentValue = close[i];
            double priorValue = i >= hannLength ? close[i - hannLength] : 0;
            momSpan[i] = currentValue - priorValue;
        }

        // Step 2: Apply MA to momentum
        var filtBuffer = context.Rent(count);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);
        maCore.Compute(momBuffer.Span, filtBuffer.WritableSpan, length1);
        momBuffer.Dispose();

        // Primary output is the filtered momentum (filt)
        return filtBuffer;
    }

    internal static ComputeBuffer ComputeEhlersAdaptiveCommodityChannelIndexV2Fast(StockData data, ComputeContext context, int length1 = 48, int length2 = 10, int length3 = 3, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.EhlersAdaptiveCommodityChannelIndexV2(close, buffer.WritableSpan, length1, length2, length3);
        return buffer;
    }

    internal static ComputeBuffer ComputeEhlersAdaptiveRelativeStrengthIndexV2Fast(StockData data, ComputeContext context, int length1 = 48, int length2 = 10, int length3 = 3, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.EhlersAdaptiveRelativeStrengthIndexV2(close, buffer.WritableSpan, length1, length2, length3);
        return buffer;
    }

    internal static ComputeBuffer ComputeEhlersAdaptiveRsiFisherTransformV2Fast(StockData data, ComputeContext context, int length1 = 48, int length2 = 10, int length3 = 3, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.FisherTransform(high, low, buffer.WritableSpan, length2);
        return buffer;
    }

    internal static ComputeBuffer ComputeEhlersAdaptiveStochasticIndicatorV2Fast(StockData data, ComputeContext context, int length1 = 48, int length2 = 10, int length3 = 3, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.StochasticK(high, low, close, buffer.WritableSpan, length2);
        return buffer;
    }

    internal static ComputeBuffer ComputeEhlersMesaPredictIndicatorV2Fast(StockData data, ComputeContext context, int length1 = 5, int length2 = 135, int length3 = 12, int length4 = 4, MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.EhlersMesaPredictIndicatorV2(close, buffer.WritableSpan, length1, length2, length3, length4);
        return buffer;
    }

    internal static ComputeBuffer ComputeEhlersSignalToNoiseRatioV1Fast(StockData data, ComputeContext context, int length = 7, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.EhlersSignalToNoiseRatioV1(close, high, low, buffer.WritableSpan, length);
        return buffer;
    }

    internal static ComputeBuffer ComputeEhlersSignalToNoiseRatioV2Fast(StockData data, ComputeContext context, int length = 6, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.EhlersSignalToNoiseRatioV2(close, high, low, buffer.WritableSpan, length);
        return buffer;
    }

    // Batch 27 - Trend and Volatility Indicators

    internal static ComputeBuffer ComputeTrendExhaustionIndicatorFast(StockData data, ComputeContext context, int length = 10, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.Momentum(close, buffer.WritableSpan, length);
        return buffer;
    }

    internal static ComputeBuffer ComputeTrendImpulseFilterFast(StockData data, ComputeContext context, int length1 = 100, int length2 = 10, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        // V1 Algorithm: Trend Impulse Filter
        // 1. Calculate highest/lowest over length1 bars
        // 2. If price breaks above highest or below lowest, a=1, else a=0
        // 3. b = (a * price) + ((1-a) * prevB)
        // 4. Apply EMA to b with length2
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        int count = data.Count;

        // Calculate rolling highest and lowest
        var bBuffer = context.Rent(count);
        var bSpan = bBuffer.WritableSpan;

        for (int i = 0; i < count; i++)
        {
            double currentValue = close[i];
            double prevB = i >= 1 ? bSpan[i - 1] : currentValue;

            // Calculate highest and lowest from previous bars (not including current)
            double highest = double.MinValue;
            double lowest = double.MaxValue;
            int startIdx = Math.Max(0, i - length1);
            for (int j = startIdx; j < i; j++)
            {
                if (close[j] > highest) highest = close[j];
                if (close[j] < lowest) lowest = close[j];
            }

            // Handle first bar
            if (i == 0)
            {
                bSpan[i] = currentValue;
                continue;
            }

            // Determine if breakout occurred
            double a = (currentValue > highest || currentValue < lowest) ? 1 : 0;
            bSpan[i] = (a * currentValue) + ((1 - a) * prevB);
        }

        // Apply EMA to b values
        var result = context.Rent(count);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);
        maCore.Compute(bBuffer.Span, result.WritableSpan, length2);

        bBuffer.Dispose();
        return result;
    }

    internal static ComputeBuffer ComputeTrendDirectionForceIndexFast(StockData data, ComputeContext context, int length1 = 10, int length2 = 30, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.ForceIndex(close, volume, buffer.WritableSpan, length1);
        return buffer;
    }

    internal static ComputeBuffer ComputeTrendAnalysisIndexFast(StockData data, ComputeContext context, int length1 = 28, int length2 = 5, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.LinearRegressionSlope(close, buffer.WritableSpan, length1);
        return buffer;
    }

    internal static ComputeBuffer ComputeTrendAnalysisIndicatorFast(StockData data, ComputeContext context, int length1 = 21, int length2 = 4, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.LinearRegressionSlope(close, buffer.WritableSpan, length1);
        return buffer;
    }

    internal static ComputeBuffer ComputeTrenderFast(StockData data, ComputeContext context, int length = 14, double atrMult = 2, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.AverageTrueRange(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    internal static ComputeBuffer ComputeTurboStochasticsFastFast(StockData data, ComputeContext context, int length1 = 20, int length2 = 10, int turboLength = 2, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.StochasticK(high, low, close, buffer.WritableSpan, length1);
        return buffer;
    }

    // Batch 28 - Volume and Volatility Indicators

    internal static ComputeBuffer ComputeTurboStochasticsSlowFast(StockData data, ComputeContext context, int length1 = 20, int length2 = 10, int turboLength = 2, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.StochasticD(high, low, close, buffer.WritableSpan, length1, length2);
        return buffer;
    }

    internal static ComputeBuffer ComputeVolumeFlowIndicatorFast(StockData data, ComputeContext context, int length1 = 130, int length2 = 30, int signalLength = 5, int smoothLength = 3, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.PriceVolumeTrend(close, volume, buffer.WritableSpan);
        return buffer;
    }

    internal static ComputeBuffer ComputeVolatilityQualityIndexFast(StockData data, ComputeContext context, int fastLength = 9, int slowLength = 200, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.AverageTrueRange(high, low, close, buffer.WritableSpan, fastLength);
        return buffer;
    }

    internal static ComputeBuffer ComputeVolatilityBasedMomentumFast(StockData data, ComputeContext context, int length1 = 22, int length2 = 65, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ChandeMomentumOscillator(close, buffer.WritableSpan, length1);
        return buffer;
    }

    internal static ComputeBuffer ComputeVolatilitySwitchIndicatorFast(StockData data, ComputeContext context, int length = 14, MovingAvgType maType = MovingAvgType.WeightedMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.StandardDeviation(close, buffer.WritableSpan, length);
        return buffer;
    }

    internal static ComputeBuffer ComputeVortexBandsFast(StockData data, ComputeContext context, int length = 20, MovingAvgType maType = MovingAvgType.McNichollMovingAverage)
    {
        // V1 Algorithm: Vortex Bands
        // 1. Calculate MA of price (basis)
        // 2. Calculate diff = |price - basis|, a distance - see the batch calculation for why signed
        //    is wrong: it averages to near zero and drives the half-width negative
        // 3. Calculate MA of diff
        // 4. dev = 2 * diffMa
        // 5. upper = basis + dev (primary output)
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        int count = data.Count;

        // Calculate basis (MA of close)
        var basisBuffer = context.Rent(count);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);
        maCore.Compute(close, basisBuffer.WritableSpan, length);

        // Calculate diff (close - basis)
        var diffBuffer = context.Rent(count);
        var diffSpan = diffBuffer.WritableSpan;
        var basisSpan = basisBuffer.Span;
        for (int i = 0; i < count; i++)
        {
            diffSpan[i] = Math.Abs(close[i] - basisSpan[i]);
        }

        // Calculate MA of diff
        var diffMaBuffer = context.Rent(count);
        maCore.Compute(diffBuffer.Span, diffMaBuffer.WritableSpan, length);

        // Calculate upper band: basis + 2*diffMa
        var result = context.Rent(count);
        var resultSpan = result.WritableSpan;
        var diffMaSpan = diffMaBuffer.Span;
        for (int i = 0; i < count; i++)
        {
            double dev = 2 * diffMaSpan[i];
            resultSpan[i] = basisSpan[i] + dev;
        }

        basisBuffer.Dispose();
        diffBuffer.Dispose();
        diffMaBuffer.Dispose();
        return result;
    }

    internal static ComputeBuffer ComputeVostroIndicatorFast(StockData data, ComputeContext context, int length1 = 5, int length2 = 100, double level = 8, MovingAvgType maType = MovingAvgType.WeightedMovingAverage)
    {
        // V1 Algorithm: Rolling sum of median and range, compute thresholds, apply level filters
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        int count = data.Count;

        // Compute WMA of close for trend filter
        var wmaBuffer = context.Rent(count);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);
        maCore.Compute(close, wmaBuffer.WritableSpan, length2);

        var result = context.Rent(count);
        var resultSpan = result.WritableSpan;

        // Rolling sums for median and range
        double medianSum = 0, rangeSum = 0;
        double prevBuff116 = 0, prevBuff112 = 0;

        for (int i = 0; i < count; i++)
        {
            double median = close[i];
            double range = high[i] - low[i];

            // Add current values to rolling sums
            medianSum += median;
            rangeSum += range;

            // Remove old values from rolling sums
            if (i >= length1)
            {
                medianSum -= close[i - length1];
                rangeSum -= high[i - length1] - low[i - length1];
            }

            double gd128 = medianSum * 0.2; // sum * (1/length1) for length1=5
            double gd136 = rangeSum * 0.04; // sum * 0.2 * 0.2

            double buff116 = gd136 != 0 ? (low[i] - gd128) / gd136 : 0;
            double buff112 = gd136 != 0 ? (high[i] - gd128) / gd136 : 0;

            double wma = wmaBuffer.Span[i];

            // Apply level thresholds
            double buff108 = buff112 > level && high[i] > wma ? 90 :
                             buff116 < -level && low[i] < wma ? -90 : 0;

            // Filter consecutive signals
            double buff109 = (buff112 > level && prevBuff112 > level) ||
                             (buff116 < -level && prevBuff116 < -level) ? 0 : buff108;

            resultSpan[i] = buff109;
            prevBuff116 = buff116;
            prevBuff112 = buff112;
        }

        wmaBuffer.Dispose();
        return result;
    }

    // Batch 29 - Ergodic and Momentum Indicators

    internal static ComputeBuffer ComputeErgodicCommoditySelectionIndexFast(StockData data, ComputeContext context, int length = 32, int smoothLength = 5, double pointValue = 1, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod)
    {
        // V1 Algorithm: CSI = k * adxR * tr / length, normalized by price
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        int count = data.Count;
        length = Math.Max(1, length);
        smoothLength = Math.Max(1, smoothLength);

        double k = 100 * (pointValue / Math.Sqrt(length) / (150 + smoothLength));

        // Step 1: Calculate ADX
        var adxBuffer = context.Rent(count);
        OscillatorCore.AverageDirectionalIndex(high, low, close, adxBuffer.WritableSpan, length);
        var adxSpan = adxBuffer.Span;

        // Step 2: Calculate CSI values
        var csiBuffer = context.Rent(count);
        var csiSpan = csiBuffer.WritableSpan;
        for (int i = 0; i < count; i++)
        {
            double currentHigh = high[i];
            double currentLow = low[i];
            double currentClose = close[i];
            double prevClose = i >= 1 ? close[i - 1] : 0;
            double adx = adxSpan[i];
            double prevAdx = i >= 1 ? adxSpan[i - 1] : 0;
            double adxR = (adx + prevAdx) * 0.5;

            // True Range calculation
            double highLow = currentHigh - currentLow;
            double highClose = Math.Abs(currentHigh - prevClose);
            double lowClose = Math.Abs(currentLow - prevClose);
            double tr = Math.Max(highLow, Math.Max(highClose, lowClose));

            double csi = (length + tr) > 0 ? k * adxR * tr / length : 0;
            double ergodicCsi = currentClose > 0 ? csi / currentClose : 0;
            csiSpan[i] = ergodicCsi;
        }

        adxBuffer.Dispose();

        // Step 3: Smooth the CSI values
        var result = context.Rent(count);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);
        maCore.Compute(csiBuffer.Span, result.WritableSpan, smoothLength);
        csiBuffer.Dispose();

        return result;
    }

    internal static ComputeBuffer ComputeErgodicMacdFast(StockData data, ComputeContext context, int length1 = 32, int length2 = 5, int length3 = 5, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.MacdLine(close, buffer.WritableSpan, length2, length1);
        return buffer;
    }

    internal static ComputeBuffer ComputeErgodicTsiV1Fast(StockData data, ComputeContext context, int length1 = 4, int length2 = 8, int length3 = 6, int signalLength = 3, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.TrueStrengthIndex(close, buffer.WritableSpan, length2, length1);
        return buffer;
    }

    internal static ComputeBuffer ComputeErgodicTsiV2Fast(StockData data, ComputeContext context, int length1 = 21, int length2 = 9, int length3 = 9, int signalLength = 2, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.TrueStrengthIndex(close, buffer.WritableSpan, length1, length2);
        return buffer;
    }

    internal static ComputeBuffer ComputeSMIErgodicIndicatorFast(StockData data, ComputeContext context, int fastLength = 5, int slowLength = 20, int signalLength = 5, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DoubleSmoothedStochastic(high, low, close, buffer.WritableSpan, slowLength, fastLength);
        return buffer;
    }

    internal static ComputeBuffer ComputeInsyncIndexFast(StockData data, ComputeContext context, int fastLength = 12, int slowLength = 26, int signalLength = 9, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        OscillatorCore.InsyncIndex(high, low, close, volume, buffer.WritableSpan, fastLength, slowLength, signalLength);
        return buffer;
    }

    internal static ComputeBuffer ComputeSqueezeMomentumIndicatorFast(StockData data, ComputeContext context, int length = 20, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.Momentum(close, buffer.WritableSpan, length);
        return buffer;
    }

    internal static ComputeBuffer ComputeStochasticConnorsRsiFast(StockData data, ComputeContext context, int length1 = 2, int length2 = 3, int length3 = 100, int smoothLength1 = 3, int smoothLength2 = 3, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.StochasticRsi(close, buffer.WritableSpan, length1, smoothLength1);
        return buffer;
    }

    // Batch 30 - Stochastic Regular

    internal static ComputeBuffer ComputeStochasticRegularFast(StockData data, ComputeContext context, int length1 = 5, int length2 = 3, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.StochasticK(high, low, close, buffer.WritableSpan, length1);
        return buffer;
    }

    // Batch 31 - Relative and Statistical Indicators

    internal static ComputeBuffer ComputeRecursiveRelativeStrengthIndexFast(StockData data, ComputeContext context, int length = 14, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.RelativeStrengthIndex(close, buffer.WritableSpan, length);
        return buffer;
    }

    internal static ComputeBuffer ComputeRelativeSpreadStrengthFast(StockData data, ComputeContext context, int fastLength = 10, int slowLength = 40, int length = 14, int smoothLength = 5, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        // V1 Algorithm: Relative Spread Strength
        // 1. Calculate fast EMA and slow EMA of close
        // 2. spread = fastEMA - slowEMA
        // 3. Calculate RSI of spread
        // 4. Apply smoothing MA
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        int count = data.Count;

        // Calculate fast and slow EMAs
        var fastEmaBuffer = context.Rent(count);
        var slowEmaBuffer = context.Rent(count);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);
        maCore.Compute(close, fastEmaBuffer.WritableSpan, fastLength);
        maCore.Compute(close, slowEmaBuffer.WritableSpan, slowLength);

        // Calculate spread (fast - slow)
        var spreadBuffer = context.Rent(count);
        var spreadSpan = spreadBuffer.WritableSpan;
        var fastSpan = fastEmaBuffer.Span;
        var slowSpan = slowEmaBuffer.Span;
        for (int i = 0; i < count; i++)
        {
            spreadSpan[i] = fastSpan[i] - slowSpan[i];
        }

        // Calculate RSI of spread
        var rsiBuffer = context.Rent(count);
        OscillatorCore.RelativeStrengthIndex(spreadBuffer.Span, rsiBuffer.WritableSpan, length);

        // Apply smoothing MA to RSI
        var result = context.Rent(count);
        maCore.Compute(rsiBuffer.Span, result.WritableSpan, smoothLength);

        fastEmaBuffer.Dispose();
        slowEmaBuffer.Dispose();
        spreadBuffer.Dispose();
        rsiBuffer.Dispose();
        return result;
    }

    /// <summary>
    /// Writes the original relative volatility index of <paramref name="series"/> into
    /// <paramref name="output"/>: the relative strength routine with the standard deviation of the window in
    /// place of the price change, which is what CalculateRelativeVolatilityIndexV1 computes.
    /// </summary>
    private static void RelativeVolatilityIndexV1(StockData data, ComputeContext context, ReadOnlySpan<double> series,
        int length, int smoothLength, MovingAvgType maType, Span<double> output)
    {
        var count = series.Length;

        using var deviation = context.Rent(count);
        VolatilityCore.StandardDeviation(series, deviation.WritableSpan, Math.Max(1, length));
        var stdDev = deviation.Span;

        using var rises = context.Rent(count);
        using var falls = context.Rent(count);
        var up = rises.WritableSpan;
        var down = falls.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            var previousValue = i >= 1 ? series[i - 1] : 0;
            up[i] = series[i] > previousValue ? stdDev[i] : 0;
            down[i] = series[i] < previousValue ? stdDev[i] : 0;
        }

        using var upAverages = context.Rent(count);
        using var downAverages = context.Rent(count);
        MovingAverage(data, maType, smoothLength, rises.Span, upAverages.WritableSpan);
        MovingAverage(data, maType, smoothLength, falls.Span, downAverages.WritableSpan);

        var avgUpSpan = upAverages.Span;
        var avgDownSpan = downAverages.Span;
        for (var i = 0; i < count; i++)
        {
            var avgUp = avgUpSpan[i];
            var avgDown = avgDownSpan[i];
            var rs = avgDown != 0 ? avgUp / avgDown : 0;

            output[i] = avgDown == 0 ? 100 : avgUp == 0 ? 0 : MathHelper.MinOrMax(100 - (100 / (1 + rs)), 100, 0);
        }
    }

    internal static ComputeBuffer ComputeRelativeVolatilityIndexV2Fast(StockData data, ComputeContext context, int length = 10,
        int smoothLength = 14, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod)
    {
        // CalculateRelativeVolatilityIndexV2 takes the original index of the high series and of the low series
        // and averages the two. VolatilityCore.RelativeVolatilityIndex ran the routine once over the close, so
        // it was a different quantity from its first bar on.
        var count = data.Count;
        var highs = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var lows = SpanCompat.AsReadOnlySpan(data.LowPrices);

        using var highIndex = context.Rent(count);
        using var lowIndex = context.Rent(count);
        RelativeVolatilityIndexV1(data, context, highs, length, smoothLength, maType, highIndex.WritableSpan);
        RelativeVolatilityIndexV1(data, context, lows, length, smoothLength, maType, lowIndex.WritableSpan);

        var buffer = context.Rent(count);
        var rvi = buffer.WritableSpan;
        var rviHigh = highIndex.Span;
        var rviLow = lowIndex.Span;
        for (var i = 0; i < count; i++)
        {
            rvi[i] = (rviHigh[i] + rviLow[i]) / 2;
        }

        return buffer;
    }

    internal static ComputeBuffer ComputeRelativeVolumeIndicatorFast(StockData data, ComputeContext context, int length = 60, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // V1 Algorithm: Relative Volume Indicator
        // 1. Calculate MA of volume
        // 2. Calculate StdDev of volume
        // 3. relVol = (currentVolume - avg) / stdDev
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        int count = data.Count;

        // Calculate MA of volume
        var volMaBuffer = context.Rent(count);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);
        maCore.Compute(volume, volMaBuffer.WritableSpan, length);

        // Calculate StdDev of volume
        var stdDevBuffer = context.Rent(count);
        VolatilityCore.StandardDeviation(volume, stdDevBuffer.WritableSpan, length);

        // Calculate relative volume: (volume - avg) / stdDev
        var result = context.Rent(count);
        var resultSpan = result.WritableSpan;
        var volMaSpan = volMaBuffer.Span;
        var stdDevSpan = stdDevBuffer.Span;
        for (int i = 0; i < count; i++)
        {
            double sd = stdDevSpan[i];
            resultSpan[i] = sd != 0 ? (volume[i] - volMaSpan[i]) / sd : 0;
        }

        volMaBuffer.Dispose();
        stdDevBuffer.Dispose();
        return result;
    }

    internal static ComputeBuffer ComputeSelfAdjustingRsiFast(StockData data, ComputeContext context, int length = 14, int smoothingLength = 21, double mult = 2, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.RelativeStrengthIndex(close, buffer.WritableSpan, length);
        return buffer;
    }

    internal static ComputeBuffer ComputeSmoothedWilliamsAccumulationDistributionFast(StockData data, ComputeContext context, int length = 14, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        // The primary series of CalculateSmoothedWilliamsAccumulationDistribution is the raw accumulation
        // total; the moving average it takes of that total is published as the Signal series, so length and
        // maType do not reach this output.
        _ = length;
        _ = maType;
        VolumeCore.WilliamsAD(high, low, close, buffer.WritableSpan);
        return buffer;
    }

    internal static ComputeBuffer ComputeStatisticalVolatilityFast(StockData data, ComputeContext context, int length1 = 30, int length2 = 253, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.HistoricalVolatility(close, buffer.WritableSpan, length1, length2);
        return buffer;
    }

    internal static ComputeBuffer ComputeTradersDynamicIndexFast(StockData data, ComputeContext context, int length1 = 13, int length2 = 34, int length3 = 2, int length4 = 7, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // V1 Algorithm: Traders Dynamic Index
        // 1. Calculate RSI with length1 period
        // 2. Calculate fast MA (mab) of RSI with length3 (primary output - TDI line)
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        int count = data.Count;

        // Calculate RSI
        var rsiBuffer = context.Rent(count);
        OscillatorCore.RelativeStrengthIndex(close, rsiBuffer.WritableSpan, length1);

        // Calculate fast MA of RSI (mab - the TDI primary output)
        var result = context.Rent(count);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);
        maCore.Compute(rsiBuffer.Span, result.WritableSpan, length3);

        rsiBuffer.Dispose();
        return result;
    }

    // Batch 32 - Remaining Indicators (Part 1)

    internal static ComputeBuffer ComputeFunctionToCandlesFast(StockData data, ComputeContext context, int length = 14, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod)
    {
        // V1 Algorithm: RSI calculated on all OHLC prices, averaged
        // 1. Calculate RSI on close, open, high, low prices
        // 2. Return average of all 4 RSI values
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        int count = data.Count;
        length = Math.Max(length, 1);

        // Calculate RSI on each OHLC price
        var rsiCloseBuffer = context.Rent(count);
        var rsiOpenBuffer = context.Rent(count);
        var rsiHighBuffer = context.Rent(count);
        var rsiLowBuffer = context.Rent(count);
        OscillatorCore.RelativeStrengthIndex(close, rsiCloseBuffer.WritableSpan, length);
        OscillatorCore.RelativeStrengthIndex(open, rsiOpenBuffer.WritableSpan, length);
        OscillatorCore.RelativeStrengthIndex(high, rsiHighBuffer.WritableSpan, length);
        OscillatorCore.RelativeStrengthIndex(low, rsiLowBuffer.WritableSpan, length);

        // Calculate average of all 4 RSI values
        var result = context.Rent(count);
        var resultSpan = result.WritableSpan;
        for (int i = 0; i < count; i++)
        {
            resultSpan[i] = (rsiCloseBuffer.Span[i] + rsiOpenBuffer.Span[i] + rsiHighBuffer.Span[i] + rsiLowBuffer.Span[i]) / 4.0;
        }

        rsiCloseBuffer.Dispose();
        rsiOpenBuffer.Dispose();
        rsiHighBuffer.Dispose();
        rsiLowBuffer.Dispose();
        return result;
    }

    internal static ComputeBuffer ComputePeakValleyEstimationFast(StockData data, ComputeContext context, int length = 500, int smoothLength = 100, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.PeakValleyEstimation(close, buffer.WritableSpan, length, smoothLength);
        return buffer;
    }

    internal static ComputeBuffer ComputePhaseChangeIndexFast(StockData data, ComputeContext context, int length = 35, int smoothLength = 3, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // V1 Algorithm: Compute momentum, gradient line deviation, sum positive/negative, ratio
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        int count = data.Count;

        // First pass: compute raw PCI values
        var pciBuffer = context.Rent(count);
        var pciSpan = pciBuffer.WritableSpan;

        for (int i = 0; i < count; i++)
        {
            double currentValue = close[i];
            double prevValue = i >= length ? close[i - length] : 0;
            double mom = i >= length ? currentValue - prevValue : 0;

            double positiveSum = 0, negativeSum = 0;
            for (int j = 0; j <= length - 1; j++)
            {
                int idx = i - (length - j);
                if (idx < 0) continue;

                double prevValue2 = close[idx];
                double gradient = prevValue + (mom * (length - j) / (length - 1));
                double deviation = prevValue2 - gradient;

                if (deviation > 0) positiveSum += deviation;
                else if (deviation < 0) negativeSum -= deviation;
            }

            double sum = positiveSum + negativeSum;
            double pciRaw = sum != 0 ? 100 * positiveSum / sum : 0;
            pciSpan[i] = pciRaw < 0 ? 0 : (pciRaw > 100 ? 100 : pciRaw);
        }

        // Apply MA smoothing
        var result = context.Rent(count);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);
        maCore.Compute(pciBuffer.Span, result.WritableSpan, smoothLength);

        pciBuffer.Dispose();
        return result;
    }

    internal static ComputeBuffer ComputePseudoPolynomialChannelFast(StockData data, ComputeContext context, int length = 14, double morph = 0.9, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // V1 Algorithm: Polynomial morphing with MA smoothing
        // 1. Compute morphed k values with lookback to length and 2*length
        // 2. Apply MA to k values to get k1 (middle band)
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        int count = data.Count;
        length = Math.Max(length, 1);

        // Calculate k values with morphing
        var kBuffer = context.Rent(count);
        var kSpan = kBuffer.WritableSpan;
        for (int i = 0; i < count; i++)
        {
            double y = close[i];
            double prevK = i >= length ? kSpan[i - length] : y;
            double prevK2 = i >= length * 2 ? kSpan[i - (length * 2)] : y;
            double prevIndex = i >= length ? (i - length) : 0;
            double prevIndex2 = i >= length * 2 ? (i - (length * 2)) : 0;

            double ky = (morph * prevK) + ((1 - morph) * y);
            double ky2 = (morph * prevK2) + ((1 - morph) * y);

            double k = prevIndex2 - prevIndex != 0 ? ky + ((i - prevIndex) / (prevIndex2 - prevIndex) * (ky2 - ky)) : 0;
            kSpan[i] = k;
        }

        // Apply MA to k values to get k1 (middle band = primary output)
        var result = context.Rent(count);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);
        maCore.Compute(kBuffer.Span, result.WritableSpan, length);

        kBuffer.Dispose();
        return result;
    }

    internal static ComputeBuffer ComputeRecursiveDifferenciatorFast(StockData data, ComputeContext context, int length = 14, double alpha = 0.6, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        // V1 Algorithm: Smoothed RSI-based differenciator
        // 1. Apply MA to input, then calculate RSI on that
        // 2. Compute b = alpha * (rsi/100) + (1-alpha) * prevB
        // 3. Returns smoothed b value
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        int count = data.Count;
        length = Math.Max(length, 1);

        // Apply MA to input
        var emaBuffer = context.Rent(count);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);
        maCore.Compute(close, emaBuffer.WritableSpan, length);

        // Calculate RSI on the MA
        var rsiBuffer = context.Rent(count);
        OscillatorCore.RelativeStrengthIndex(emaBuffer.Span, rsiBuffer.WritableSpan, length);

        // Calculate smoothed b values
        var result = context.Rent(count);
        var resultSpan = result.WritableSpan;
        var rsiSpan = rsiBuffer.Span;
        double prevB = 0;

        for (int i = 0; i < count; i++)
        {
            double rsi = rsiSpan[i];
            double a = rsi / 100.0;

            // b = alpha * a + (1-alpha) * prevB
            double b = (alpha * a) + ((1 - alpha) * prevB);
            resultSpan[i] = b;
            prevB = b;
        }

        emaBuffer.Dispose();
        rsiBuffer.Dispose();
        return result;
    }

    internal static ComputeBuffer ComputeReversalPointsFast(StockData data, ComputeContext context, int length = 100, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        // V1 Algorithm: Volatility-based reversal point detection
        // 1. Compute a = max(val, prevVal) - min(val, prevVal) for each bar
        // 2. Double MA smooth a to get aEma1, aEma2
        // 3. b = aEma1 / aEma2, then sum b over length periods
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        int count = data.Count;
        length = Math.Max(length, 1);
        int length1 = Math.Max((int)Math.Ceiling(length / 2.0), 1);

        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);

        // Calculate a values (difference between max and min of current and previous)
        var aBuffer = context.Rent(count);
        var aSpan = aBuffer.WritableSpan;
        for (int i = 0; i < count; i++)
        {
            double currentValue = close[i];
            double prevValue = i >= 1 ? close[i - 1] : currentValue;
            double max = Math.Max(currentValue, prevValue);
            double min = Math.Min(currentValue, prevValue);
            aSpan[i] = max - min;
        }

        // First MA pass on a values
        var aEma1Buffer = context.Rent(count);
        maCore.Compute(aBuffer.Span, aEma1Buffer.WritableSpan, length1);

        // Second MA pass (double smoothing)
        var aEma2Buffer = context.Rent(count);
        maCore.Compute(aEma1Buffer.Span, aEma2Buffer.WritableSpan, length1);

        // Calculate b = aEma1 / aEma2, then rolling sum over length
        var result = context.Rent(count);
        var resultSpan = result.WritableSpan;
        var aEma1Span = aEma1Buffer.Span;
        var aEma2Span = aEma2Buffer.Span;

        for (int i = 0; i < count; i++)
        {
            double aEma1 = aEma1Span[i];
            double aEma2 = aEma2Span[i];
            double b = aEma2 != 0 ? aEma1 / aEma2 : 0;

            // Rolling sum of b over length periods
            double bSum = b;
            for (int j = 1; j < length && i - j >= 0; j++)
            {
                double prevAEma1 = aEma1Span[i - j];
                double prevAEma2 = aEma2Span[i - j];
                double prevB = prevAEma2 != 0 ? prevAEma1 / prevAEma2 : 0;
                bSum += prevB;
            }
            resultSpan[i] = bSum;
        }

        aBuffer.Dispose();
        aEma1Buffer.Dispose();
        aEma2Buffer.Dispose();
        return result;
    }

    internal static ComputeBuffer ComputeRSINGIndicatorFast(StockData data, ComputeContext context, int length = 20, MovingAvgType maType = MovingAvgType.WeightedMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.RelativeStrengthIndex(close, buffer.WritableSpan, length);
        return buffer;
    }

    internal static ComputeBuffer ComputeRunningEquityFast(StockData data, ComputeContext context, int length = 100, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // V1 Algorithm: Sign(close - sma) * price change, rolling sum
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        int count = data.Count;

        // Compute SMA
        var smaBuffer = context.Rent(count);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);
        maCore.Compute(close, smaBuffer.WritableSpan, length);

        var result = context.Rent(count);
        var resultSpan = result.WritableSpan;

        // Rolling sum of (price change * prevX)
        double prevX = 0;
        double chgXSum = 0;

        for (int i = 0; i < count; i++)
        {
            double currentValue = close[i];
            double prevValue = i >= 1 ? close[i - 1] : 0;
            double sma = smaBuffer.Span[i];

            double x = Math.Sign(currentValue - sma);
            double chgX = i >= 1 ? (currentValue - prevValue) * prevX : 0;

            // Add to rolling sum
            chgXSum += chgX;

            // Remove old value from rolling sum
            if (i >= length)
            {
                int oldIdx = i - length;
                double oldX = oldIdx >= 1 ? Math.Sign(close[oldIdx] - smaBuffer.Span[oldIdx]) : 0;
                double oldPrevX = oldIdx >= 2 ? Math.Sign(close[oldIdx - 1] - smaBuffer.Span[oldIdx - 1]) : 0;
                double oldChgX = oldIdx >= 1 ? (close[oldIdx] - close[oldIdx - 1]) * oldPrevX : 0;
                chgXSum -= oldChgX;
            }

            resultSpan[i] = chgXSum;
            prevX = x;
        }

        smaBuffer.Dispose();
        return result;
    }

    // Batch 33 - Remaining Indicators (Part 2)

    internal static ComputeBuffer ComputeSigmaSpikesFast(StockData data, ComputeContext context, int length = 20, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.StandardDeviation(close, buffer.WritableSpan, length);
        return buffer;
    }

    internal static ComputeBuffer ComputeStandardDevationFast(StockData data, ComputeContext context, int length = 14, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.StandardDeviation(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Which of the stationary extrapolated levels an arm has been asked for. The deviation is not a band at
    /// all - it is how far price sits from its own average - so it does not fit <see cref="ChannelBand"/>,
    /// and it is the series the streaming state publishes first.
    /// </summary>
    internal enum ExtrapolatedLevelSeries
    {
        Deviation,
        Upper,
        Middle,
        Lower
    }

    internal static ComputeBuffer ComputeStationaryExtrapolatedLevelsFast(StockData data, ComputeContext context, int length = 200,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, ExtrapolatedLevelSeries series = ExtrapolatedLevelSeries.Deviation)
    {
        // CalculateStationaryExtrapolatedLevels measures how far each bar sits from the moving average of the
        // chained series, then extrapolates that deviation from the two readings a window and two windows
        // back. The bands are the running extremes of that extrapolation taken twice over; the deviation is a
        // different quantity, and it is the one the streaming state publishes first. This read the close and
        // the registry average directly, so it matched neither.
        var inputList = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = SpanCompat.AsReadOnlySpan(inputList);
        var count = inputList.Count;
        length = Math.Max(length, 1);

        using var average = context.Rent(count);
        MovingAverage(data, maType, length, input, average.WritableSpan);
        var ma = average.Span;

        var deviations = context.Rent(count);
        var y = deviations.WritableSpan;
        for (var i = 0; i < count; i++)
        {
            y[i] = input[i] - ma[i];
        }

        if (series == ExtrapolatedLevelSeries.Deviation)
        {
            return deviations;
        }

        using (deviations)
        {
            using var extrapolation = context.Rent(count);
            var ext = extrapolation.WritableSpan;
            var deviation = deviations.Span;
            for (var i = 0; i < count; i++)
            {
                // The bar index is its own series in the batch, so a reading that is not yet a window old
                // extrapolates from zero rather than from a bar that does not exist.
                double x = i;
                var priorX = i >= length ? i - length : 0;
                var priorX2 = i >= length * 2 ? i - (length * 2) : 0;
                var priorY = i >= length ? deviation[i - length] : 0;
                var priorY2 = i >= length * 2 ? deviation[i - (length * 2)] : 0;

                ext[i] = priorX2 - priorX != 0 && priorY2 - priorY != 0
                    ? (priorY + ((x - priorX) / (priorX2 - priorX) * (priorY2 - priorY))) / 2
                    : 0;
            }

            var buffer = context.Rent(count);
            var output = buffer.WritableSpan;

            // The bands are the running extremes of the extrapolation taken twice: the inner window never
            // runs shorter than two bars, while the outer pair take the length as given, which is why both
            // publish a partial-window extreme from the first bar.
            var window = new RollingMinMax(Math.Max(length, 2));
            var highWindow = new RollingMinMax(length);
            var lowWindow = new RollingMinMax(length);
            for (var i = 0; i < count; i++)
            {
                window.Add(ext[i]);
                highWindow.Add(window.Max);
                lowWindow.Add(window.Min);

                output[i] = series switch
                {
                    ExtrapolatedLevelSeries.Upper => highWindow.Max,
                    ExtrapolatedLevelSeries.Lower => lowWindow.Min,
                    _ => (highWindow.Max + lowWindow.Min) / 2
                };
            }

            return buffer;
        }
    }

    internal static ComputeBuffer ComputeSupportResistanceFast(StockData data, ComputeContext context, int length = 20, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // V1 Algorithm: SMA crossover to update support/resistance from highest/lowest
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        int count = data.Count;

        // Compute SMA, highest, lowest
        var smaBuffer = context.Rent(count);
        var highestBuffer = context.Rent(count);
        var lowestBuffer = context.Rent(count);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);
        maCore.Compute(close, smaBuffer.WritableSpan, length);
        VolatilityCore.Highest(high, highestBuffer.WritableSpan, length);
        VolatilityCore.Lowest(low, lowestBuffer.WritableSpan, length);

        // Compute support/resistance based on SMA crossovers
        // Returns support level as primary output
        var result = context.Rent(count);
        var resultSpan = result.WritableSpan;

        double prevRes = high[0];
        double prevSupp = low[0];

        for (int i = 0; i < count; i++)
        {
            double currentValue = close[i];
            double prevValue = i >= 1 ? close[i - 1] : 0;
            double sma = i >= 1 ? smaBuffer.Span[i - 1] : 0;
            double highest = highestBuffer.Span[i];
            double lowest = lowestBuffer.Span[i];

            bool crossAbove = prevValue < sma && currentValue >= sma;
            bool crossBelow = prevValue > sma && currentValue <= sma;

            double res = crossBelow ? highest : (i >= 1 ? prevRes : highest);
            double supp = crossAbove ? lowest : (i >= 1 ? prevSupp : lowest);

            // Return support as primary output
            resultSpan[i] = supp;
            prevRes = res;
            prevSupp = supp;
        }

        smaBuffer.Dispose();
        highestBuffer.Dispose();
        lowestBuffer.Dispose();

        return result;
    }

    internal static ComputeBuffer ComputeSurfaceRoughnessEstimatorFast(StockData data, ComputeContext context, int length = 100, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        // V1 Algorithm: Rolling correlation between current and previous close values, transformed to roughness
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        int count = data.Count;
        length = Math.Max(2, length);

        // Build arrays for current and previous values
        var prevBuffer = context.Rent(count);
        var prevSpan = prevBuffer.WritableSpan;
        for (int i = 0; i < count; i++)
            prevSpan[i] = i >= 1 ? close[i - 1] : 0;

        // Calculate rolling correlation and transform to roughness
        var aBuffer = context.Rent(count);
        var aSpan = aBuffer.WritableSpan;
        for (int i = 0; i < count; i++)
        {
            if (i < length - 1)
            {
                aSpan[i] = 0;
                continue;
            }

            // Calculate Pearson correlation between close and prev over window
            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0, sumY2 = 0;
            for (int j = 0; j < length; j++)
            {
                double x = prevSpan[i - j];
                double y = close[i - j];
                sumX += x;
                sumY += y;
                sumXY += x * y;
                sumX2 += x * x;
                sumY2 += y * y;
            }

            double n = length;
            double denom = Math.Sqrt((n * sumX2 - sumX * sumX) * (n * sumY2 - sumY * sumY));
            double corr = denom != 0 ? (n * sumXY - sumX * sumY) / denom : 0;

            // Transform correlation to roughness: a = 1 - ((corr + 1) / 2)
            aSpan[i] = 1 - ((corr + 1) / 2);
        }

        prevBuffer.Dispose();

        // Smooth the roughness values with MA
        var result = context.Rent(count);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);
        maCore.Compute(aBuffer.Span, result.WritableSpan, length);
        aBuffer.Dispose();

        return result;
    }

    internal static ComputeBuffer ComputeTechnicalRatingsFast(StockData data, ComputeContext context, int aoLength1 = 55, int aoLength2 = 34, int rsiLength = 14, int stochLength1 = 14, int stochLength2 = 3, int stochLength3 = 3, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);
        OscillatorCore.TechnicalRatings(high, low, close, volume, buffer.WritableSpan, aoLength1, aoLength2, rsiLength, stochLength1, stochLength2, stochLength3, maCore: maCore);
        return buffer;
    }

    internal static ComputeBuffer ComputeTFSMboIndicatorFast(StockData data, ComputeContext context, int fastLength = 25, int slowLength = 200, int signalLength = 18, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // V1 Algorithm: Fast MA - Slow MA = mob
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        int count = data.Count;
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);

        // Compute fast and slow MAs
        var fastMaBuffer = context.Rent(count);
        var slowMaBuffer = context.Rent(count);
        maCore.Compute(close, fastMaBuffer.WritableSpan, fastLength);
        maCore.Compute(close, slowMaBuffer.WritableSpan, slowLength);

        // Compute mob = fast - slow
        var mobBuffer = context.Rent(count);
        for (int i = 0; i < count; i++)
        {
            mobBuffer.WritableSpan[i] = fastMaBuffer.Span[i] - slowMaBuffer.Span[i];
        }

        fastMaBuffer.Dispose();
        slowMaBuffer.Dispose();

        // Return mob (primary output is the mob line, not the histogram)
        return mobBuffer;
    }

    internal static ComputeBuffer ComputeTheRangeIndicatorFast(StockData data, ComputeContext context, int length = 10, int smoothLength = 3, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.Range(high, low, buffer.WritableSpan);
        return buffer;
    }

    // Batch 34 - Remaining Indicators (Part 3)

    internal static ComputeBuffer ComputeTimeAndMoneyChannelFast(StockData data, ComputeContext context, int length1 = 41, int length2 = 82, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // V1 Algorithm: Yield over median (yom), variance, std of yom, channel bands
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        int count = data.Count;
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);

        int halfLength = (int)Math.Ceiling((double)length1 / 2);
        if (halfLength < 1) halfLength = 1;

        // Compute SMA (basis)
        var smaBuffer = context.Rent(count);
        maCore.Compute(close, smaBuffer.WritableSpan, length1);

        // Compute yom = 100 * (close - prevBasis) / prevBasis
        var yomBuffer = context.Rent(count);
        var yomSquaredBuffer = context.Rent(count);
        for (int i = 0; i < count; i++)
        {
            double prevBasis = i >= halfLength ? smaBuffer.Span[i - halfLength] : 0;
            double yom = prevBasis != 0 ? 100 * (close[i] - prevBasis) / prevBasis : 0;
            yomBuffer.WritableSpan[i] = yom;
            yomSquaredBuffer.WritableSpan[i] = yom * yom;
        }

        // Compute avyom and yomSquaredSma
        var avyomBuffer = context.Rent(count);
        var yomSquaredSmaBuffer = context.Rent(count);
        maCore.Compute(yomBuffer.Span, avyomBuffer.WritableSpan, length2);
        maCore.Compute(yomSquaredBuffer.Span, yomSquaredSmaBuffer.WritableSpan, length2);

        // Compute variance and std
        var somBuffer = context.Rent(count);
        for (int i = 0; i < count; i++)
        {
            double avyom = avyomBuffer.Span[i];
            double yomSquaredSma = yomSquaredSmaBuffer.Span[i];
            double varyom = yomSquaredSma - (avyom * avyom);

            double prevVaryom = i >= halfLength ? yomSquaredSmaBuffer.Span[i - halfLength] - (avyomBuffer.Span[i - halfLength] * avyomBuffer.Span[i - halfLength]) : 0;
            double som = prevVaryom >= 0 ? Math.Sqrt(prevVaryom) : 0;
            somBuffer.WritableSpan[i] = som;
        }

        // Compute sigom (smoothed som) - this is used for channel width
        var result = context.Rent(count);
        maCore.Compute(somBuffer.Span, result.WritableSpan, length1);

        yomBuffer.Dispose();
        yomSquaredBuffer.Dispose();
        avyomBuffer.Dispose();
        yomSquaredSmaBuffer.Dispose();
        somBuffer.Dispose();
        smaBuffer.Dispose();

        return result;
    }

    internal static ComputeBuffer ComputeTopsAndBottomsFinderFast(StockData data, ComputeContext context, int length = 50, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        // V1 Algorithm: EMA rising/falling with stddev-based thresholds for top/bottom detection
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        int count = data.Count;
        length = Math.Max(2, length);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);

        // Step 1: Compute EMA of close prices
        var emaBuffer = context.Rent(count);
        maCore.Compute(close, emaBuffer.WritableSpan, length);
        var emaSpan = emaBuffer.Span;

        // Step 2: Calculate b (EMA when rising) and c (EMA when falling)
        var bBuffer = context.Rent(count);
        var cBuffer = context.Rent(count);
        var bSpan = bBuffer.WritableSpan;
        var cSpan = cBuffer.WritableSpan;
        for (int i = 0; i < count; i++)
        {
            double a = emaSpan[i];
            double prevA = i >= 1 ? emaSpan[i - 1] : 0;
            bSpan[i] = a > prevA ? a : 0;
            cSpan[i] = a < prevA ? a : 0;
        }

        // Step 3: Compute stddev of b and c values
        var bStdDevBuffer = context.Rent(count);
        var cStdDevBuffer = context.Rent(count);
        VolatilityCore.StandardDeviation(bBuffer.Span, bStdDevBuffer.WritableSpan, length);
        VolatilityCore.StandardDeviation(cBuffer.Span, cStdDevBuffer.WritableSpan, length);
        var bStdSpan = bStdDevBuffer.Span;
        var cStdSpan = cStdDevBuffer.Span;

        // Step 4: Calculate up, dn, and os (oscillator signal)
        var result = context.Rent(count);
        var resultSpan = result.WritableSpan;
        double prevUp = 0;
        double prevDn = 0;
        for (int i = 0; i < count; i++)
        {
            double a = emaSpan[i];
            double bStd = bStdSpan[i];
            double cStd = cStdSpan[i];

            double up = (a + bStd) != 0 ? a / (a + bStd) : 0;
            double dn = (a + cStd) != 0 ? a / (a + cStd) : 0;

            // Signal: 1 when up drops from 1, -1 when dn drops from 1
            double os = 0;
            if (prevUp == 1 && up != 1)
                os = 1;
            else if (prevDn == 1 && dn != 1)
                os = -1;

            resultSpan[i] = os;
            prevUp = up;
            prevDn = dn;
        }

        emaBuffer.Dispose();
        bBuffer.Dispose();
        cBuffer.Dispose();
        bStdDevBuffer.Dispose();
        cStdDevBuffer.Dispose();

        return result;
    }

    internal static ComputeBuffer ComputeTraderPressureIndexFast(StockData data, ComputeContext context, int length1 = 7, int length2 = 2, int smoothLength = 3, MovingAvgType maType = MovingAvgType.WeightedMovingAverage)
    {
        // V1 Algorithm: high/low changes, highest/lowest range, bulls/bears calculation, net smoothing
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        int count = data.Count;
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);

        // Compute highest/lowest over length2
        var highestBuffer = context.Rent(count);
        var lowestBuffer = context.Rent(count);
        VolatilityCore.Highest(high, highestBuffer.WritableSpan, length2);
        VolatilityCore.Lowest(low, lowestBuffer.WritableSpan, length2);

        // Compute bulls and bears
        var bullsBuffer = context.Rent(count);
        var bearsBuffer = context.Rent(count);

        for (int i = 0; i < count; i++)
        {
            double prevHigh = i >= 1 ? high[i - 1] : 0;
            double prevLow = i >= 1 ? low[i - 1] : 0;
            double hiup = Math.Max(high[i] - prevHigh, 0);
            double loup = Math.Max(low[i] - prevLow, 0);
            double hidn = Math.Min(high[i] - prevHigh, 0);
            double lodn = Math.Min(low[i] - prevLow, 0);
            double range = highestBuffer.Span[i] - lowestBuffer.Span[i];

            bullsBuffer.WritableSpan[i] = range != 0 ? Math.Min((hiup + loup) / range, 1) * 100 : 0;
            bearsBuffer.WritableSpan[i] = range != 0 ? Math.Max((hidn + lodn) / range, -1) * -100 : 0;
        }

        // Average bulls and bears over length1
        var avgBullsBuffer = context.Rent(count);
        var avgBearsBuffer = context.Rent(count);
        maCore.Compute(bullsBuffer.Span, avgBullsBuffer.WritableSpan, length1);
        maCore.Compute(bearsBuffer.Span, avgBearsBuffer.WritableSpan, length1);

        // Compute net
        var netBuffer = context.Rent(count);
        for (int i = 0; i < count; i++)
        {
            netBuffer.WritableSpan[i] = avgBullsBuffer.Span[i] - avgBearsBuffer.Span[i];
        }

        // Smooth net
        var result = context.Rent(count);
        maCore.Compute(netBuffer.Span, result.WritableSpan, smoothLength);

        highestBuffer.Dispose();
        lowestBuffer.Dispose();
        bullsBuffer.Dispose();
        bearsBuffer.Dispose();
        avgBullsBuffer.Dispose();
        avgBearsBuffer.Dispose();
        netBuffer.Dispose();

        return result;
    }

    internal static ComputeBuffer ComputeUhlMaCrossoverSystemFast(StockData data, ComputeContext context, int length = 100, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // V1 Algorithm: Adaptive MA crossover system using variance-based coefficients
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        int count = data.Count;
        length = Math.Max(1, length);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);

        // Step 1: Compute SMA of close prices
        var smaBuffer = context.Rent(count);
        maCore.Compute(close, smaBuffer.WritableSpan, length);
        var smaSpan = smaBuffer.Span;

        // Step 2: Compute standard deviation (we need variance = stddev^2)
        var stdDevBuffer = context.Rent(count);
        VolatilityCore.StandardDeviation(close, stdDevBuffer.WritableSpan, length);
        // Convert to variance (stddev^2)
        var varBuffer = context.Rent(count);
        for (int i = 0; i < count; i++)
        {
            double std = stdDevBuffer.Span[i];
            varBuffer.WritableSpan[i] = std * std;
        }
        stdDevBuffer.Dispose();
        var varSpan = varBuffer.Span;

        // Step 3: Calculate CTS using adaptive coefficients
        var result = context.Rent(count);
        var resultSpan = result.WritableSpan;
        double prevCma = count > 0 ? close[0] : 0;
        double prevCts = count > 0 ? close[0] : 0;

        for (int i = 0; i < count; i++)
        {
            double currentValue = close[i];
            double sma = smaSpan[i];
            double prevVar = i >= length ? varSpan[i - length] : 0;

            double secma = (sma - prevCma) * (sma - prevCma);
            double sects = (currentValue - prevCts) * (currentValue - prevCts);

            double ka = (prevVar < secma && secma != 0) ? 1 - (prevVar / secma) : 0;
            double kb = (prevVar < sects && sects != 0) ? 1 - (prevVar / sects) : 0;

            double cma = (ka * sma) + ((1 - ka) * prevCma);
            double cts = (kb * currentValue) + ((1 - kb) * prevCts);

            resultSpan[i] = cts;  // Return CTS as primary output
            prevCma = cma;
            prevCts = cts;
        }

        smaBuffer.Dispose();
        varBuffer.Dispose();

        return result;
    }

    internal static ComputeBuffer ComputeUltimateVolatilityIndicatorFast(StockData data, ComputeContext context, int length = 14, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        // V1 Algorithm: abs(close - open) rolling sum averaged
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        int count = data.Count;

        var result = context.Rent(count);
        var resultSpan = result.WritableSpan;

        // Rolling sum of abs(close - open)
        double absSum = 0;
        double invLength = 1.0 / length;

        for (int i = 0; i < count; i++)
        {
            double absVal = Math.Abs(close[i] - open[i]);
            absSum += absVal;

            // Remove old value from rolling sum
            if (i >= length)
            {
                double oldAbs = Math.Abs(close[i - length] - open[i - length]);
                absSum -= oldAbs;
            }

            // UVI = average of abs values over length
            resultSpan[i] = invLength * absSum;
        }

        return result;
    }

    internal static ComputeBuffer ComputeUniChannelFast(StockData data, ComputeContext context, int length = 10, double ubFac = 0.02, double lbFac = 0.02, bool type1 = false, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // V1 Algorithm: SMA with upper/lower bands based on percentage factors
        // Returns the middle band (SMA) as the primary output
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        int count = data.Count;

        var result = context.Rent(count);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);
        maCore.Compute(close, result.WritableSpan, length);

        // The primary output is the middle band (SMA)
        // Upper and lower bands would be computed as:
        // ub = type1 ? sma + ubFac : sma * (1 + ubFac)
        // lb = type1 ? sma - lbFac : sma * (1 - lbFac)
        return result;
    }

    internal static ComputeBuffer ComputeVixTradingSystemFast(StockData data, ComputeContext context, int length = 50, double maxCount = 11, double minCount = -11, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // V1 Algorithm: Count consecutive closes above/below SMA
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        int count = data.Count;

        // Compute SMA
        var smaBuffer = context.Rent(count);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);
        maCore.Compute(close, smaBuffer.WritableSpan, length);

        var result = context.Rent(count);
        var resultSpan = result.WritableSpan;

        double prevCount = 0;
        for (int i = 0; i < count; i++)
        {
            double currentValue = close[i];
            double sma = smaBuffer.Span[i];

            // Count: +1 each close above SMA, -1 each close below, reset on cross
            double cnt = currentValue > sma && prevCount >= 0 ? prevCount + 1 :
                         currentValue <= sma && prevCount <= 0 ? prevCount - 1 : prevCount;
            resultSpan[i] = cnt;
            prevCount = cnt;
        }

        smaBuffer.Dispose();
        return result;
    }

    internal static ComputeBuffer ComputeWilsonRelativePriceChannelFast(StockData data, ComputeContext context, int length = 34, int smoothLength = 1, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        // V1 Algorithm: RSI-based price channel with overbought/oversold zones
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        int count = data.Count;
        length = Math.Max(1, length);
        smoothLength = Math.Max(1, smoothLength);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);

        const double overbought = 70;
        const double oversold = 30;

        // Step 1: Calculate RSI
        var rsiBuffer = context.Rent(count);
        OscillatorCore.RelativeStrengthIndex(close, rsiBuffer.WritableSpan, length);
        var rsiSpan = rsiBuffer.Span;

        // Step 2: Calculate differences from overbought/oversold levels
        var rsiOverboughtBuffer = context.Rent(count);
        var rsiOversoldBuffer = context.Rent(count);
        for (int i = 0; i < count; i++)
        {
            rsiOverboughtBuffer.WritableSpan[i] = rsiSpan[i] - overbought;
            rsiOversoldBuffer.WritableSpan[i] = rsiSpan[i] - oversold;
        }

        // Step 3: Smooth the differences
        var obSmoothBuffer = context.Rent(count);
        var osSmoothBuffer = context.Rent(count);
        maCore.Compute(rsiOverboughtBuffer.Span, obSmoothBuffer.WritableSpan, smoothLength);
        maCore.Compute(rsiOversoldBuffer.Span, osSmoothBuffer.WritableSpan, smoothLength);

        // Step 4: Calculate channel values (returning s1 - oversold channel line)
        var result = context.Rent(count);
        for (int i = 0; i < count; i++)
        {
            double currentClose = close[i];
            double os = osSmoothBuffer.Span[i];
            // s1 = close - (close * smoothedOversold / 100)
            result.WritableSpan[i] = currentClose - (currentClose * os / 100);
        }

        rsiBuffer.Dispose();
        rsiOverboughtBuffer.Dispose();
        rsiOversoldBuffer.Dispose();
        obSmoothBuffer.Dispose();
        osSmoothBuffer.Dispose();

        return result;
    }

    internal static ComputeBuffer ComputeWoodieCommodityChannelIndexFast(StockData data, ComputeContext context, int fastLength = 6, int slowLength = 14, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.CommodityChannelIndex(high, low, close, buffer.WritableSpan, fastLength);
        return buffer;
    }

    #endregion

    #endregion
}
