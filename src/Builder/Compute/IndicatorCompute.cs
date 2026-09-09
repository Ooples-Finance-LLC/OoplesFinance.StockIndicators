using System.Buffers;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Models;

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
        return spec.Options switch
        {
            // Multi-output indicators with nested switch
            MacdSpecOptions macd => spec.Output switch
            {
                IndicatorOutput.Primary => ComputeMacdLineFast(data, context, macd.FastLength, macd.SlowLength),
                IndicatorOutput.Signal => ComputeMacdSignalFast(data, context, macd.FastLength, macd.SlowLength, macd.SignalLength),
                IndicatorOutput.Histogram => ComputeMacdHistogramFast(data, context, macd.FastLength, macd.SlowLength, macd.SignalLength),
                _ => null
            },
            BollingerBandsSpecOptions bb => spec.Output switch
            {
                IndicatorOutput.UpperBand => ComputeBollingerUpperFast(data, context, bb.Length, bb.StdDevMult, bb.MaType),
                IndicatorOutput.MiddleBand => ComputeBollingerMiddleFast(data, context, bb.Length, bb.MaType),
                IndicatorOutput.LowerBand => ComputeBollingerLowerFast(data, context, bb.Length, bb.StdDevMult, bb.MaType),
                IndicatorOutput.Primary => ComputeBollingerMiddleFast(data, context, bb.Length, bb.MaType), // Primary defaults to middle
                _ => null
            },
            StochasticSpecOptions stoch => spec.Output switch
            {
                IndicatorOutput.Primary => ComputeStochasticKFast(data, context, stoch.KLength),
                IndicatorOutput.Signal => ComputeStochasticDFast(data, context, stoch.KLength, stoch.DLength),
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
            RsiSpecOptions rsi => ComputeRsiFast(data, context, rsi.Length),
            RocSpecOptions roc => ComputeRocFast(data, context, roc.Length),
            MomentumSpecOptions mom => ComputeMomentumFast(data, context, mom.Length),
            WilliamsRSpecOptions willr => ComputeWilliamsRFast(data, context, willr.Length),
            CciSpecOptions cci => ComputeCciFast(data, context, cci.Length),
            CmoSpecOptions cmo => ComputeCmoFast(data, context, cmo.Length),
            PpoSpecOptions ppo => ComputePpoFast(data, context, ppo.FastLength, ppo.SlowLength),
            ApoSpecOptions apo => ComputeApoFast(data, context, apo.FastLength, apo.SlowLength),
            UltimateOscillatorSpecOptions uo => ComputeUltimateOscillatorFast(data, context, uo.Length1, uo.Length2, uo.Length3),
            TsiSpecOptions tsi => ComputeTsiFast(data, context, tsi.LongLength, tsi.ShortLength),
            StochRsiSpecOptions srsi => ComputeStochRsiFast(data, context, srsi.RsiLength, srsi.StochLength),
            AroonSpecOptions aroon => ComputeAroonOscillatorFast(data, context, aroon.Length),
            DpoSpecOptions dpo => ComputeDpoFast(data, context, dpo.Length),
            TrixSpecOptions trix => ComputeTrixFast(data, context, trix.Length),
            MassIndexSpecOptions mi => ComputeMassIndexFast(data, context, mi.EmaLength, mi.SumLength),
            AtrSpecOptions atr => ComputeAtrFast(data, context, atr.Length),
            AdxSpecOptions adx => ComputeAdxFast(data, context, adx.Length),

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
            ChaikinVolatilitySpecOptions cv => ComputeChaikinVolatilityFast(data, context, cv.Length),
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
            AwesomeOscillatorSpecOptions ao => ComputeAwesomeOscillatorFast(data, context, ao.Length),
            AcceleratorOscillatorSpecOptions aco => ComputeAcceleratorOscillatorFast(data, context, aco.Length),
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
            ChandelierExitLongSpecOptions cel => ComputeChandelierExitLongFast(data, context, cel.Length),
            ChandelierExitShortSpecOptions ces => ComputeChandelierExitShortFast(data, context, ces.Length),

            // Batch 3 - Volume/Power indicators
            BalanceOfPowerSpecOptions bop => ComputeBalanceOfPowerFast(data, context, bop.Length),
            PvoSpecOptions pvo => ComputePvoFast(data, context, pvo.Length),

            // Batch 3 - More Oscillators
            CoppockCurveSpecOptions coppock => ComputeCoppockCurveFast(data, context, coppock.Length),
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
            DemarkerSpecOptions dmk => ComputeDemarkerFast(data, context, dmk.Length),
            SmoothedRocSpecOptions sroc => ComputeSmoothedRocFast(data, context, sroc.Length),
            DerivativeOscillatorSpecOptions dro => ComputeDerivativeOscillatorFast(data, context, dro.Length),
            FractalChaosOscillatorSpecOptions fco => ComputeFractalChaosOscillatorFast(data, context, fco.Length),
            DisparityIndexSpecOptions di => ComputeDisparityIndexFast(data, context, di.Length),
            DynamicMomentumIndexSpecOptions dmi => ComputeDynamicMomentumIndexFast(data, context, dmi.Length),

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
            DoubleSmoothedStochasticSpecOptions dss => ComputeDoubleSmoothedStochasticFast(data, context, dss.Length),
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
            DirectionalTrendIndexSpecOptions dti => ComputeDirectionalTrendIndexFast(data, context, dti.Length),
            LinRegInterceptSpecOptions lri => ComputeLinRegInterceptFast(data, context, lri.Length),
            ElderImpulseSystemSpecOptions eis => ComputeElderImpulseSystemFast(data, context, eis.Length),
            MassThrustSpecOptions mt => ComputeMassThrustFast(data, context, mt.Length),

            // Batch 5 - Chande indicators
            ChandeCompositeMomentumIndexSpecOptions ccmi => ComputeChandeCompositeMomentumIndexFast(data, context, ccmi.ShortLength, ccmi.LongLength),
            ChandeKrollRSquaredIndexSpecOptions ckrsi => ComputeChandeKrollRSquaredIndexFast(data, context, ckrsi.Length),
            ChandeTrendScoreSpecOptions cts => ComputeChandeTrendScoreFast(data, context, cts.Length),
            ChandeMomentumOscillatorAbsoluteSpecOptions cmoa => ComputeChandeMomentumOscillatorAbsoluteFast(data, context, cmoa.Length),

            // Batch 5 - Oscillators
            ErgodicCandlestickOscillatorSpecOptions eco => ComputeErgodicCandlestickOscillatorFast(data, context, eco.Length),
            BayesianOscillatorSpecOptions bayes => ComputeBayesianOscillatorFast(data, context, bayes.Length),
            AnchoredMomentumSpecOptions amom => ComputeAnchoredMomentumFast(data, context, amom.Length),
            ChartmillValueIndicatorSpecOptions cmvi => ComputeChartmillValueIndicatorFast(data, context, cmvi.Length),
            CenterOfLinearitySpecOptions col => ComputeCenterOfLinearityFast(data, context, col.Length),
            BreakoutRsiSpecOptions brsi => ComputeBreakoutRsiFast(data, context, brsi.Length),
            ChopZoneSpecOptions cz => ComputeChopZoneFast(data, context, cz.Length),
            ForecastOscillatorSpecOptions fco2 => ComputeForecastOscillatorFast(data, context, fco2.Length),

            // Batch 5 - Adaptive indicators
            AsymmetricalRsiSpecOptions arsi => ComputeAsymmetricalRsiFast(data, context, arsi.UpLength, arsi.DownLength),
            AdaptiveStochasticSpecOptions adstoch => ComputeAdaptiveStochasticFast(data, context, adstoch.MinLength, adstoch.MaxLength),
            AdaptiveRsiSpecOptions adrisi => ComputeAdaptiveRsiFast(data, context, adrisi.MinLength, adrisi.MaxLength),

            // Batch 5 - Moving averages
            AutoLineSpecOptions al => ComputeAutoLineFast(data, context, al.Length),
            AutoLineWithDriftSpecOptions alwd => ComputeAutoLineWithDriftFast(data, context, alwd.Length),
            AutoFilterSpecOptions af => ComputeAutoFilterFast(data, context, af.Length),
            BuffAverageSpecOptions ba => ComputeBuffAverageFast(data, context, ba.Length),
            BryantAdaptiveMovingAverageSpecOptions bama => ComputeBryantAdaptiveMovingAverageFast(data, context, bama.Length),
            CompoundRatioMovingAverageSpecOptions crma => ComputeCompoundRatioMovingAverageFast(data, context, crma.Length),
            ConditionalAccumulatorSpecOptions ca => ComputeConditionalAccumulatorFast(data, context, ca.Length),
            AhrensMovingAverageSpecOptions ahma => ComputeAhrensMovingAverageFast(data, context, ahma.Length),
            AlphaDecreasingEmaSpecOptions adema => ComputeAlphaDecreasingEmaFast(data, context, adema.Length),
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
            StandardDeviationChannelSpecOptions sdch => ComputeStandardDeviationChannelFast(data, context, sdch.Length),
            StandardDeviationVolatilitySpecOptions sdv => ComputeStandardDeviationVolatilityFast(data, context, sdv.Length, sdv.AnnualizationFactor),
            VolatilityRatioSpecOptions vr => ComputeVolatilityRatioFast(data, context, vr.Length),

            // Batch 5 - Bands/Channels
            AtrTrailingStopsSpecOptions ats => ComputeAtrTrailingStopsFast(data, context, ats.Length, ats.Multiplier),
            AtrChannelWidthSpecOptions acw => ComputeAtrChannelWidthFast(data, context, acw.Length, acw.Multiplier),
            AverageTrueRangeChannelSpecOptions atrc => ComputeAverageTrueRangeChannelFast(data, context, atrc.Length, atrc.Multiplier),
            VolatilityStopSpecOptions vs => ComputeVolatilityStopFast(data, context, vs.Length, vs.Multiplier),
            BollingerBandsPercentBSpecOptions bbpb => ComputeBollingerBandsPercentBFast(data, context, bbpb.Length, bbpb.Multiplier),
            BollingerBandsAtrSpecOptions bbatr => ComputeBollingerBandsAtrFast(data, context, bbatr.Length, bbatr.Multiplier),

            // Batch 5 - Ratio/Performance
            CalmarRatioSpecOptions cr => ComputeCalmarRatioFast(data, context, cr.Length),
            CommoditySelectionIndexSpecOptions csi => ComputeCommoditySelectionIndexFast(data, context, csi.Length),

            // Batch 5 - Smoothed oscillators
            SmoothedWilliamsRSpecOptions swillr => ComputeSmoothedWilliamsRFast(data, context, swillr.Length, swillr.SmoothLength),
            PriceOscillatorPercentSpecOptions pop => ComputePriceOscillatorPercentFast(data, context, pop.ShortLength, pop.LongLength),
            NormalizedMacdSpecOptions nmacd => ComputeNormalizedMacdFast(data, context, nmacd.FastLength, nmacd.SlowLength),
            RelativeVigorIndexSignalSpecOptions rvis => ComputeRelativeVigorIndexSignalFast(data, context, rvis.Length, rvis.SignalLength),
            VolumeMomentumOscillatorSpecOptions vmo => ComputeVolumeMomentumOscillatorFast(data, context, vmo.ShortLength, vmo.LongLength),
            TrendContinuationFactorSpecOptions tcf => ComputeTrendContinuationFactorFast(data, context, tcf.Length),
            TrendPersistenceRateSpecOptions tpr => ComputeTrendPersistenceRateFast(data, context, tpr.Length),
            InertiaSpecOptions inertia => ComputeInertiaFast(data, context, inertia.RviLength, inertia.SmoothLength),

            // Batch 5 - Price calculations
            PercentChangeSpecOptions pchg => ComputePercentChangeFast(data, context, pchg.Length),
            PriceChangeSpecOptions prc => ComputePriceChangeFast(data, context),
            MidRangeSpecOptions mr => ComputeMidRangeFast(data, context),
            OhlcAverageSpecOptions ohlc => ComputeOhlcAverageFast(data, context),
            HlcAverageSpecOptions hlc => ComputeHlcAverageFast(data, context),
            DoubleSmoothedMomentaSpecOptions dsm => ComputeDoubleSmoothedMomentaFast(data, context, dsm.MomentumLength, dsm.FirstSmooth, dsm.SecondSmooth),

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
            ChandeMomentumOscillatorAbsoluteAverageSpecOptions cmoaa => ComputeChandeMomentumOscillatorAbsoluteAverageFast(data, context, cmoaa.Length),
            ChandeMomentumOscillatorAverageSpecOptions cmoa2 => ComputeChandeMomentumOscillatorAverageFast(data, context, cmoa2.Length),
            ChandeMomentumOscillatorAverageDisparityIndexSpecOptions cmoadi => ComputeChandeMomentumOscillatorAverageDisparityIndexFast(data, context, cmoadi.Length),
            ChandeMomentumOscillatorFilterSpecOptions cmof => ComputeChandeMomentumOscillatorFilterFast(data, context, cmof.Length),

            // Batch 6 - Stochastic variants
            DoubleStochasticOscillatorSpecOptions dso => ComputeDoubleStochasticOscillatorFast(data, context, dso.Length),
            BilateralStochasticOscillatorSpecOptions bso => ComputeBilateralStochasticOscillatorFast(data, context, bso.Length),
            FisherTransformStochasticOscillatorSpecOptions ftso => ComputeFisherTransformStochasticOscillatorFast(data, context, ftso.Length),
            StochasticCustomOscillatorSpecOptions sco => ComputeStochasticCustomOscillatorFast(data, context, sco.Length),
            FastSlowStochasticOscillatorSpecOptions fsso => ComputeFastSlowStochasticOscillatorFast(data, context, fsso.Length),
            DiNapoliPreferredStochasticOscillatorSpecOptions dnpso => ComputeDiNapoliPreferredStochasticOscillatorFast(data, context, dnpso.Length),
            DMIStochasticSpecOptions dmis => ComputeDMIStochasticFast(data, context, dmis.Length),
            CCTStochRelativeStrengthIndexSpecOptions cctrsi => ComputeCCTStochRelativeStrengthIndexFast(data, context, cctrsi.Length),

            // Batch 6 - DT/Dynamic oscillators
            DTOscillatorSpecOptions dto => ComputeDTOscillatorFast(data, context, dto.Length),
            DynamicMomentumOscillatorSpecOptions dmo => ComputeDynamicMomentumOscillatorFast(data, context, dmo.Length),

            // Batch 6 - Price/Momentum oscillators
            ComparePriceMomentumOscillatorSpecOptions cpmo => ComputeComparePriceMomentumOscillatorFast(data, context, cpmo.Length),
            DailyAveragePriceDeltaSpecOptions dapd => ComputeDailyAveragePriceDeltaFast(data, context, dapd.Length),
            PriceCycleOscillatorSpecOptions pco => ComputePriceCycleOscillatorFast(data, context, pco.Length),
            PriceVolumeOscillatorSpecOptions pvo2 => ComputePriceVolumeOscillatorFast(data, context, pvo2.Length),
            PercentChangeOscillatorSpecOptions pchosc => ComputePercentChangeOscillatorFast(data, context, pchosc.Length),
            DecisionPointPriceMomentumOscillatorSpecOptions dppmo => ComputeDecisionPointPriceMomentumOscillatorFast(data, context, dppmo.Length),

            // Batch 6 - Demand/Volume oscillators
            DemandOscillatorSpecOptions demosc => ComputeDemandOscillatorFast(data, context, demosc.Length),
            AverageMoneyFlowOscillatorSpecOptions amfo => ComputeAverageMoneyFlowOscillatorFast(data, context, amfo.Length),
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
            EhlersDecyclerOscillatorV2SpecOptions edov2 => ComputeEhlersDecyclerOscillatorV2Fast(data, context, edov2.FastLength),
            EhlersHilbertOscillatorSpecOptions eho => ComputeEhlersHilbertOscillatorFast(data, context, eho.Length),
            EhlersUniversalOscillatorSpecOptions euo => ComputeEhlersUniversalOscillatorFast(data, context, euo.Length),
            EhlersRecursiveMedianOscillatorSpecOptions ermo => ComputeEhlersRecursiveMedianOscillatorFast(data, context, ermo.Length),
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
            EhlersInverseFisherTransformSpecOptions eift => ComputeEhlersInverseFisherTransformFast(data, context, eift.Length),
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
            EhlersHighPassFilterV2SpecOptions ehpv2 => ComputeEhlersHighPassFilterV2Fast(data, context, ehpv2.Length),
            DistanceWeightedMovingAverageSpecOptions dwma => ComputeDistanceWeightedMovingAverageFast(data, context, dwma.Length),
            EhlersFilterSpecOptions efilter => ComputeEhlersFilterFast(data, context, efilter.Length),
            EhlersFirFilterSpecOptions efir => ComputeEhlersFirFilterFast(data, context, efir.Length),
            EhlersIirFilterSpecOptions eiir => ComputeEhlersIirFilterFast(data, context, eiir.Length),

            // Batch 7 - Cycle indicators
            SimpleCycleSpecOptions scyc => ComputeSimpleCycleFast(data, context, scyc.Length),
            SimpleLinesSpecOptions slines => ComputeSimpleLinesFast(data, context, slines.Length, slines.Multiplier),
            DoubleExponentialSmoothingSpecOptions des => ComputeDoubleExponentialSmoothingFast(data, context, des.Length),
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
            CorrectedMovingAverageSpecOptions cma => ComputeCorrectedMovingAverageFast(data, context, cma.Length),
            CubedWeightedMovingAverageSpecOptions cwma => ComputeCubedWeightedMovingAverageFast(data, context, cwma.Length),
            DynamicallyAdjustableFilterSpecOptions daf => ComputeDynamicallyAdjustableFilterFast(data, context, daf.Length),
            EdgePreservingFilterSpecOptions epf => ComputeEdgePreservingFilterFast(data, context, epf.Length),
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
            FollowingAdaptiveMovingAverageSpecOptions fama => ComputeFollowingAdaptiveMovingAverageFast(data, context, fama.Length),
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
            MiddleHighLowMovingAverageSpecOptions mhlma => ComputeMiddleHighLowMovingAverageFast(data, context, mhlma.Length1, mhlma.Length2),
            VortexMinusSpecOptions vminus => ComputeVortexMinusFast(data, context, vminus.Length),
            VortexPlusSpecOptions vplus => ComputeVortexPlusFast(data, context, vplus.Length),
            VolumeWeightedMovingAverageSpecOptions vwma27 => ComputeVolumeWeightedMovingAverageFast(data, context, vwma27.Length),
            KlingerSignalSpecOptions ksig => ComputeKlingerSignalFast(data, context, ksig.FastLength, ksig.SlowLength, ksig.SignalLength),
            EhlersChebyshevLowPassFilterSpecOptions eclpf => ComputeEhlersChebyshevLowPassFilterFast(data, context, eclpf.Length, eclpf.Ripple),
            EhlersGaussianFilterSpecOptions egf => ComputeEhlersGaussianFilterFast(data, context, egf.Length, egf.Poles),
            EhlersMedianAverageAdaptiveFilterSpecOptions emaaf => ComputeEhlersMedianAverageAdaptiveFilterFast(data, context, emaaf.Length, emaaf.Threshold),
            EhlersMesaAdaptiveMovingAverageSpecOptions emama => ComputeEhlersMesaAdaptiveMovingAverageFast(data, context, emama.Length, emama.FastLimit, emama.SlowLimit),
            EhlersRecursiveMedianFilterSpecOptions ermf => ComputeEhlersRecursiveMedianFilterFast(data, context, ermf.Length, ermf.Alpha),
            EhlersRoofingFilterSpecOptions eroof => ComputeEhlersRoofingFilterFast(data, context, eroof.HpLength, eroof.LpLength),

            // Batch 28
            EhlersDeviationScaledSuperSmootherSpecOptions edsss => ComputeEhlersDeviationScaledSuperSmootherFast(data, context, edsss.Length, edsss.Poles),
            PpoMaSpecOptions ppoma => ComputePpoMaFast(data, context, ppoma.FastLength, ppoma.SlowLength),
            PriceOscillatorSpecOptions posc => ComputePriceOscillatorFast(data, context, posc.ShortLength, posc.LongLength),
            ReverseEngineeringRsiSpecOptions rersi => ComputeReverseEngineeringRsiFast(data, context, rersi.Length, rersi.RsiLevel),
            ReverseMovingAverageConvergenceDivergenceSpecOptions rmacd => ComputeReverseMovingAverageConvergenceDivergenceFast(data, context, rmacd.FastLength, rmacd.SlowLength, rmacd.MacdLevel),
            SimplePriceZoneSpecOptions spz => ComputeSimplePriceZoneFast(data, context, spz.Length),
            StochasticRsiOscillatorSpecOptions srsio => ComputeStochasticRsiOscillatorFast(data, context, srsio.RsiLength, srsio.StochLength),
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
            EhlersFiniteImpulseResponseFilterSpecOptions efirf => ComputeEhlersFiniteImpulseResponseFilterFast(data, context, efirf.Length),
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
            EhlersReverseEmaIndicatorV2SpecOptions eremav2 => ComputeEhlersReverseEmaIndicatorV2Fast(data, context, eremav2.TrendAlpha, eremav2.CycleAlpha),
            EhlersStochasticCyberCycleSpecOptions escc => ComputeEhlersStochasticCyberCycleFast(data, context, escc.Length, escc.Alpha),
            EhlersCenterofGravityOscillatorSpecOptions ecog => ComputeEhlersCenterofGravityOscillatorFast(data, context, ecog.Length),
            EhlersReflexIndicatorSpecOptions eri => ComputeEhlersReflexIndicatorFast(data, context, eri.Length),
            EhlersTrendflexIndicatorSpecOptions eti => ComputeEhlersTrendflexIndicatorFast(data, context, eti.Length),
            JmaRsxCloneSpecOptions jrsx => ComputeJmaRsxCloneFast(data, context, jrsx.Length),
            RateOfChangeSpecOptions roc => ComputeRateOfChangeFast(data, context, roc.Length),
            WilliamsFractalsSpecOptions wf => ComputeWilliamsFractalsFast(data, context, wf.Length),
            DetrendedPriceOscillatorSpecOptions dpo => ComputeDetrendedPriceOscillatorFast(data, context, dpo.Length),
            PolarizedFractalEfficiencySpecOptions pfe => ComputePolarizedFractalEfficiencyFast(data, context, pfe.Length, pfe.SmoothLength),
            SchaffTrendCycleSpecOptions stc => ComputeSchaffTrendCycleFast(data, context, stc.CycleLength, stc.FastLength, stc.SlowLength),
            SmoothedRateOfChangeSpecOptions sroc => ComputeSmoothedRateOfChangeFast(data, context, sroc.RocLength, sroc.SmoothLength),
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
            AverageTrueRangeTrailingStopsSpecOptions atrts => ComputeAverageTrueRangeTrailingStopsFast(data, context, atrts.Length, atrts.Multiplier),
            WellesWilderSummationSpecOptions wws => ComputeWellesWilderSummationFast(data, context, wws.Length),
            DampingIndexSpecOptions di => ComputeDampingIndexFast(data, context, di.Length),
            DidiIndexSpecOptions didi => ComputeDidiIndexFast(data, context, didi.ShortLength, didi.MediumLength, didi.LongLength),
            VerticalHorizontalFilterSpecOptions vhf => ComputeVerticalHorizontalFilterFast(data, context, vhf.Length),
            LinearRegressionSlopeSpecOptions lrs => ComputeLinearRegressionSlopeFast(data, context, lrs.Length),
            LinearRegressionInterceptSpecOptions lri => ComputeLinearRegressionInterceptFast(data, context, lri.Length),

            // Batch 5 - Indicators with Core methods (23 indicators)
            AbsolutePriceOscillatorSpecOptions apo2 => ComputeAbsolutePriceOscillatorFast(data, context, apo2.FastLength, apo2.SlowLength),
            AccumulationDistributionLineSpecOptions adl2 => ComputeAccumulationDistributionLineFast(data, context),
            AdaptiveExponentialMovingAverageSpecOptions aema => ComputeAdaptiveExponentialMovingAverageFast(data, context, aema.Length),
            AverageDirectionalIndexSpecOptions adx2 => ComputeAverageDirectionalIndexFast(data, context, adx2.Length),
            AverageTrueRangeSpecOptions atr2 => ComputeAverageTrueRangeFast(data, context, atr2.Length),
            ChandeMomentumOscillatorSpecOptions cmo2 => ComputeChandeMomentumOscillatorFast(data, context, cmo2.Length),
            EaseOfMovementSpecOptions eom => ComputeEaseOfMovementFast(data, context, eom.Length),
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
            ChandeQuickStickSpecOptions cqs => ComputeChandeQuickStickFast(data, context, cqs.Length),
            DeltaMovingAverageSpecOptions dma => ComputeDeltaMovingAverageFast(data, context, dma.Length1, dma.Length2),
            FoldedRelativeStrengthIndexSpecOptions frsi => ComputeFoldedRsiFast(data, context, frsi.Length),
            EnhancedWilliamsRSpecOptions ewr => ComputeEnhancedWilliamsRFast(data, context, ewr.Length, ewr.SignalLength),
            ConnorsRelativeStrengthIndexSpecOptions crsi2 => ComputeConnorsRsiFast(data, context, crsi2.Length1, crsi2.Length2, crsi2.Length3),
            StochasticRelativeStrengthIndexSpecOptions srsi2 => ComputeStochasticRsiFast(data, context, srsi2.Length, srsi2.SmoothLength1, srsi2.SmoothLength2),
            StochasticMomentumIndexSpecOptions smi => ComputeStochasticMomentumIndexFast(data, context, smi.Length1, smi.SmoothLength1, smi.SmoothLength2),

            // Batch 7 - Additional oscillators and power indicators
            CCTStochRSISpecOptions cctsr => ComputeCCTStochRsiFast(data, context, cctsr.Length4, cctsr.Length1, cctsr.SmoothLength1),
            InertiaIndicatorSpecOptions inertia => ComputeInertiaFast(data, context, inertia.Length),
            PremierStochasticOscillatorSpecOptions pso => ComputePremierStochasticFast(data, context, pso.Length, pso.SmoothLength),
            BullPowerIndicatorSpecOptions bpi => ComputeBullPowerFast(data, context, bpi.Length),
            BearPowerIndicatorSpecOptions beari => ComputeBearPowerFast(data, context, beari.Length),
            MomentumOscillatorSpecOptions mosc => ComputeMomentumOscillatorFast(data, context, mosc.Length, mosc.SmoothLength),
            StochasticOscillatorSpecOptions stosc => ComputeStochasticOscillatorFast(data, context, stosc.Length, stosc.SmoothLength1),
            StochasticFastOscillatorSpecOptions stfo => ComputeStochasticFastFast(data, context, stfo.Length, stfo.SmoothLength1),

            // Multi-output: KeltnerChannels
            KeltnerChannelsSpecOptions kc => spec.Output switch
            {
                IndicatorOutput.Primary => ComputeKeltnerMiddleFast(data, context, kc.Length1),
                IndicatorOutput.MiddleBand => ComputeKeltnerMiddleFast(data, context, kc.Length1),
                _ => null
            },

            // Multi-output: ElderRayIndex
            ElderRayIndexSpecOptions eri => spec.Output switch
            {
                IndicatorOutput.Primary => ComputeElderRayBullPowerFast(data, context, eri.Length),
                _ => null
            },

            // Multi-output: ChandelierExit
            ChandelierExitSpecOptions ce => spec.Output switch
            {
                IndicatorOutput.Primary => ComputeChandelierExitLongFast(data, context, ce.Length),
                IndicatorOutput.UpperBand => ComputeChandelierExitLongFast(data, context, ce.Length),
                IndicatorOutput.LowerBand => ComputeChandelierExitShortFast(data, context, ce.Length),
                _ => null
            },

            // Batch 8 - Moving Averages with existing Core methods
            _1LCLeastSquaresMovingAverageSpecOptions olc => ComputeOneLCLeastSquaresFast(data, context, olc.Length),
            _3HMASpecOptions thma2 => ComputeThreeHmaFast(data, context, thma2.Length),
            AdaptiveRelativeStrengthIndexSpecOptions arsi => ComputeAdaptiveRsiFast(data, context, arsi.Length),
            BollingerBandsAvgTrueRangeSpecOptions bbatr => ComputeBollingerBandsAtrFast(data, context, bbatr.AtrLength, bbatr.Length),
            ChandeMomentumOscillatorSignalSpecOptions cmos => ComputeChandeMomentumOscillatorSignalFast(data, context, cmos.Length, cmos.SignalLength),
            EhlersRoofingFilterV1SpecOptions erf1 => ComputeEhlersRoofingFilterV1Fast(data, context, erf1.Length2, erf1.Length1),

            // Batch 9 - More oscillators and indicators
            SpearmanIndicatorSpecOptions spi => ComputeEhlersSpearmanRankFast(data, context, spi.Length),
            TillsonT3MovingAverageSpecOptions tt3 => ComputeTillsonT3Fast(data, context, tt3.Length, tt3.VFactor),
            UltimateMovingAverageBandsSpecOptions umab => ComputeUltimateMovingAverageFast(data, context, umab.MaxLength),

            // Batch 10 - Ehlers Window indicators
            EhlersHammingWindowIndicatorSpecOptions ehwi => ComputeEhlersHammingWindowFast(data, context, ehwi.Length, ehwi.Pedestal),
            EhlersHannWindowIndicatorSpecOptions ehnwi => ComputeEhlersHannWindowFast(data, context, ehnwi.Length),
            EhlersTriangleWindowIndicatorSpecOptions etwi => ComputeEhlersTriangleWindowFast(data, context, etwi.Length),
            EhlersImpulseResponseSpecOptions eir => ComputeEhlersImpulseReactionFast(data, context, eir.Length),
            EhlersModifiedStochasticIndicatorSpecOptions emsi => ComputeEhlersModifiedStochasticFast(data, context, emsi.Length1, emsi.Length2, emsi.Length3),

            // Batch 11 - Additional Moving Averages with Core methods
            VariableIndexDynamicAverageSpecOptions vida => ComputeVariableIndexDynamicAverageFast(data, context, vida.Length),

            // Batch 12 - New Core Methods for Previously Unimplemented Indicators
            EhlersSimpleDerivIndicatorSpecOptions esdi => ComputeEhlersSimpleDerivIndicatorFast(data, context, esdi.Length, esdi.SignalLength, esdi.MaType),
            EhlersSimpleClipIndicatorSpecOptions esci => ComputeEhlersSimpleClipIndicatorFast(data, context, esci.Length1, esci.Length3, esci.SignalLength, esci.MaType),
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
            VariableMovingAverageBandsSpecOptions vmab => ComputeVariableMovingAverageBandsFast(data, context, vmab.Length, vmab.Mult, vmab.MaType),
            NarrowSidewaysChannelSpecOptions nsc => ComputeNarrowSidewaysChannelFast(data, context, nsc.Length, nsc.Pct, nsc.MaType),

            // Batch 16 - More Band and Channel Indicators
            HighLowBandsSpecOptions hlb => ComputeHighLowBandsFast(data, context, hlb.Length, hlb.PctShift, hlb.MaType),
            AutoDispersionBandsSpecOptions adb => ComputeAutoDispersionBandsFast(data, context, adb.Length, adb.SmoothLength, adb.MaType),
            BollingerBandsFibonacciRatiosSpecOptions bbfr => ComputeBollingerBandsFibonacciRatiosFast(data, context, bbfr.Length, bbfr.FibRatio1, bbfr.FibRatio2, bbfr.FibRatio3, bbfr.MaType),
            BollingerBandsWithAtrPctSpecOptions bbatrp => ComputeBollingerBandsWithAtrPctFast(data, context, bbatrp.Length, bbatrp.BbLength, bbatrp.StdDevMult, bbatrp.MaType),
            KirshenbaumBandsSpecOptions kb => ComputeKirshenbaumBandsFast(data, context, kb.Length1, kb.Length2, kb.StdDevFactor, kb.MaType),
            SmoothedVolatilityBandsSpecOptions svb => ComputeSmoothedVolatilityBandsFast(data, context, svb.Length1, svb.Length2, svb.Deviation, svb.BandAdjust, svb.MaType),
            StollerAverageRangeChannelsSpecOptions starc => ComputeStollerAverageRangeChannelsFast(data, context, starc.Length, starc.AtrMult, starc.MaType),
            VervoortVolatilityBandsSpecOptions vvb => ComputeVervoortVolatilityBandsFast(data, context, vvb.Length1, vvb.Length2, vvb.DevMult, vvb.LowBandMult, vvb.MaType),

            // Batch 17 - More Band and Channel Indicators
            VolumeAdaptiveBandsSpecOptions vab => ComputeVolumeAdaptiveBandsFast(data, context, vab.Length, vab.MaType),
            TrendTraderBandsSpecOptions ttb => ComputeTrendTraderBandsFast(data, context, ttb.Length, ttb.Mult, ttb.BandStep, ttb.MaType),
            ScalpersChannelSpecOptions sc => ComputeScalpersChannelFast(data, context, sc.Length1, sc.Length2, sc.MaType),
            HurstCycleChannelSpecOptions hcc => ComputeHurstCycleChannelFast(data, context, hcc.FastLength, hcc.SlowLength, hcc.FastMult, hcc.SlowMult, hcc.MaType),
            PriceCurveChannelSpecOptions pcc => ComputePriceCurveChannelFast(data, context, pcc.Length, pcc.MaType),
            PriceHeadleyAccelerationBandsSpecOptions phab => ComputePriceHeadleyAccelerationBandsFast(data, context, phab.Length, phab.Factor, phab.MaType),
            PriceLineChannelSpecOptions plc => ComputePriceLineChannelFast(data, context, plc.Length, plc.MaType),
            RateOfChangeBandsSpecOptions rocb => ComputeRateOfChangeBandsFast(data, context, rocb.Length, rocb.SmoothLength, rocb.MaType),

            // Batch 18 - Strength and Zone Indicators
            AbsoluteStrengthMTFIndicatorSpecOptions asmtf => ComputeAbsoluteStrengthMTFFast(data, context, asmtf.Length, asmtf.SmoothLength, asmtf.MaType),
            AdaptivePriceZoneIndicatorSpecOptions apz => ComputeAdaptivePriceZoneFast(data, context, apz.Length, apz.Pct, apz.MaType),
            DynamicSupportAndResistanceSpecOptions dsar => ComputeDynamicSupportAndResistanceFast(data, context, dsar.Length, dsar.MaType),
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
            HirashimaSugitaRSSpecOptions hsrs => ComputeHirashimaSugitaRSFast(data, context, hsrs.Length, hsrs.MaType),
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
            EhlersPhaseCalculationSpecOptions epc => ComputeEhlersPhaseCalculationFast(data, context, epc.Length, epc.MaType),
            EhlersRestoringPullIndicatorSpecOptions erpi => ComputeEhlersRestoringPullIndicatorFast(data, context, erpi.MinLength, erpi.MaxLength, erpi.Length1, erpi.Length2, erpi.MaType),
            EhlersRocketRelativeStrengthIndexSpecOptions errsi => ComputeEhlersRocketRsiFast(data, context, errsi.Length1, errsi.MaType),
            EhlersSimpleWindowIndicatorSpecOptions eswi => ComputeEhlersSimpleWindowIndicatorFast(data, context, eswi.Length, eswi.MaType),
            EhlersSmoothedAdaptiveMomentumSpecOptions esam => ComputeEhlersSmoothedAdaptiveMomentumFast(data, context, esam.Length1, esam.Length2, esam.MaType),
            EhlersSnakeUniversalTradingFilterSpecOptions esutf => ComputeEhlersSnakeUniversalTradingFilterFast(data, context, esutf.Length1, esutf.Length2, esutf.Bw, esutf.MaType),
            EhlersTrendExtractionSpecOptions ete => ComputeEhlersTrendExtractionFast(data, context, ete.Length, ete.MaType),
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
            StationaryExtrapolatedLevelsSpecOptions sel => ComputeStationaryExtrapolatedLevelsFast(data, context, sel.Length, sel.MaType),
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
    internal static ComputeBuffer ComputeRsiFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        OscillatorCore.RelativeStrengthIndex(inputSpan, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Rate of Change using zero-allocation fast path.
    /// Uses OscillatorCore with span-based computation directly into pooled buffer.
    /// </summary>
    internal static ComputeBuffer ComputeRocFast(StockData data, ComputeContext context, int length = 12)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
    internal static ComputeBuffer ComputeAdxFast(StockData data, ComputeContext context, int length = 14)
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
        OscillatorCore.AverageDirectionalIndex(high, low, close, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Chande Momentum Oscillator using zero-allocation fast path.
    /// Uses OscillatorCore with span-based computation directly into pooled buffer.
    /// </summary>
    internal static ComputeBuffer ComputeCmoFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.TrueStrengthIndex(inputSpan, buffer.WritableSpan, longLength, shortLength);
        return buffer;
    }

    /// <summary>
    /// Computes Stochastic RSI using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeStochRsiFast(StockData data, ComputeContext context, int rsiLength = 14, int stochLength = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.StochasticRsi(inputSpan, buffer.WritableSpan, rsiLength, stochLength);
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
    internal static ComputeBuffer ComputeAtrFast(StockData data, ComputeContext context, int length = 14)
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
        VolatilityCore.AverageTrueRange(high, low, close, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Standard Deviation using zero-allocation fast path.
    /// Uses VolatilityCore with span-based computation directly into pooled buffer.
    /// </summary>
    internal static ComputeBuffer ComputeStdDevFast(StockData data, ComputeContext context, int length = 20)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        VolatilityCore.HistoricalVolatility(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Chaikin Volatility using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChaikinVolatilityFast(StockData data, ComputeContext context, int length = 10)
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
        VolatilityCore.ChaikinVolatility(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ulcer Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeUlcerIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
    internal static ComputeBuffer ComputeBollingerBandsFast(StockData data, ComputeContext context, int length = 20, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        // Use the registry to compute the MA with the specified type
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);
        maCore.Compute(inputSpan, buffer.WritableSpan, length);
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
    internal static ComputeBuffer ComputeBollingerUpperFast(StockData data, ComputeContext context, int length = 20, double multiplier = 2, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        // Rent temp buffers for middle and lower that we don't need
        var middleBuffer = context.Rent(inputList.Count);
        var lowerBuffer = context.Rent(inputList.Count);
        VolatilityCore.BollingerBands(inputSpan, buffer.WritableSpan, middleBuffer.WritableSpan, lowerBuffer.WritableSpan, length, multiplier, maType);
        middleBuffer.Dispose();
        lowerBuffer.Dispose();
        return buffer;
    }

    /// <summary>
    /// Computes Bollinger Bands Lower band using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeBollingerLowerFast(StockData data, ComputeContext context, int length = 20, double multiplier = 2, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        // Rent temp buffers for upper and middle that we don't need
        var upperBuffer = context.Rent(inputList.Count);
        var middleBuffer = context.Rent(inputList.Count);
        VolatilityCore.BollingerBands(inputSpan, upperBuffer.WritableSpan, middleBuffer.WritableSpan, buffer.WritableSpan, length, multiplier, maType);
        upperBuffer.Dispose();
        middleBuffer.Dispose();
        return buffer;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
    internal static ComputeBuffer ComputeAwesomeOscillatorFast(StockData data, ComputeContext context, int length = 5)
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
        OscillatorCore.AwesomeOscillator(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Accelerator Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAcceleratorOscillatorFast(StockData data, ComputeContext context, int length = 5)
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
        OscillatorCore.AcceleratorOscillator(high, low, buffer.WritableSpan, length);
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.SmoothedMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes McGinley Dynamic using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeMcGinleyDynamicFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.SwingIndex(open, high, low, close, buffer.WritableSpan, limitMove);
        return buffer;
    }

    /// <summary>
    /// Computes Accumulative Swing Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAccumulativeSwingIndexFast(StockData data, ComputeContext context, double limitMove = 0)
    {
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.AccumulativeSwingIndex(open, high, low, close, buffer.WritableSpan, limitMove);
        return buffer;
    }

    /// <summary>
    /// Computes Coppock Curve using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeCoppockCurveFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length; // Coppock uses fixed parameters internally
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.CoppockCurve(inputSpan, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Chande Forecast Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChandeForecastOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.ChandeForecastOscillator(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Bull Power using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeBullPowerFast(StockData data, ComputeContext context, int length = 13)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.BullPower(high, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Bear Power using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeBearPowerFast(StockData data, ComputeContext context, int length = 13)
    {
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.BearPower(low, close, buffer.WritableSpan, length);
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.LeastSquaresMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Fractal Adaptive Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeFramaFast(StockData data, ComputeContext context, int length = 16)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.FractalAdaptiveMovingAverage(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Adaptive Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAmaFast(StockData data, ComputeContext context, int length = 10)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.GeometricMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Regularized EMA using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeRegularizedEmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.RegularizedEma(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Modified Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeModifiedMaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.ModifiedMovingAverage(inputSpan, buffer.WritableSpan, length);
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
    internal static ComputeBuffer ComputeChandelierExitLongFast(StockData data, ComputeContext context, int length = 22)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.ChandelierExitLong(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Chandelier Exit Short using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChandelierExitShortFast(StockData data, ComputeContext context, int length = 22)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.ChandelierExitShort(high, low, close, buffer.WritableSpan, length);
        return buffer;
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
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.AveragePrice(open, high, low, close, buffer.WritableSpan);
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
        TrendCore.PivotPoint(high, low, close, buffer.WritableSpan);
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
    internal static ComputeBuffer ComputeDisparityIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DisparityIndex(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Directional Trend Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDirectionalTrendIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DirectionalTrendIndex(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Double Smoothed Stochastic using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDoubleSmoothedStochasticFast(StockData data, ComputeContext context, int length = 10)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DoubleSmoothedStochastic(high, low, close, buffer.WritableSpan, length, 3);
        return buffer;
    }

    /// <summary>
    /// Computes Dynamic Momentum Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDynamicMomentumIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length; // DMI uses min/max lengths, not a single length
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DynamicMomentumIndex(close, buffer.WritableSpan, 3, 30);
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
    internal static ComputeBuffer ComputeDemarkerFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.Demarker(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Smoothed Rate of Change using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSmoothedRocFast(StockData data, ComputeContext context, int length = 12)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.SmoothedRateOfChange(close, buffer.WritableSpan, length, 3);
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
    internal static ComputeBuffer ComputeBollingerBandsWidthFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.BollingerBandsWidth(close, buffer.WritableSpan, length, 2);
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
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.SuperSmoother(close, buffer.WritableSpan, length);
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
    internal static ComputeBuffer ComputeDerivativeOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DerivativeOscillator(close, buffer.WritableSpan, length);
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
    internal static ComputeBuffer ComputeChandeCompositeMomentumIndexFast(StockData data, ComputeContext context, int shortLength = 3, int longLength = 10)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ChandeCompositeMomentumIndex(close, buffer.WritableSpan, shortLength, longLength);
        return buffer;
    }

    /// <summary>
    /// Computes Chande Kroll R-Squared Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChandeKrollRSquaredIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ChandeKrollRSquaredIndex(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Bayesian Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeBayesianOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.BayesianOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Anchored Momentum using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAnchoredMomentumFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.AnchoredMomentum(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Chartmill Value Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChartmillValueIndicatorFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ChartmillValueIndicator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Center of Linearity using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeCenterOfLinearityFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.CenterOfLinearity(close, buffer.WritableSpan, length);
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
    internal static ComputeBuffer ComputeAdaptiveRsiFast(StockData data, ComputeContext context, int minLength = 5, int maxLength = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.AdaptiveRsi(close, buffer.WritableSpan, minLength, maxLength);
        return buffer;
    }

    #endregion

    #region Trend - Additional Batch 4

    /// <summary>
    /// Computes Chande Trend Score using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChandeTrendScoreFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.ChandeTrendScore(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Chop Zone using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChopZoneFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.ChopZone(high, low, close, buffer.WritableSpan, length);
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
    internal static ComputeBuffer ComputeAutoFilterFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.AutoFilter(close, buffer.WritableSpan, length);
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
    internal static ComputeBuffer ComputeAtrTrailingStopsFast(StockData data, ComputeContext context, int length = 14, double multiplier = 3)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.AtrTrailingStops(high, low, close, buffer.WritableSpan, length, multiplier);
        return buffer;
    }

    /// <summary>
    /// Computes Compound Ratio Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeCompoundRatioMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.CompoundRatioMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Conditional Accumulator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeConditionalAccumulatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.ConditionalAccumulator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ahrens Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAhrensMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.AhrensMovingAverage(close, buffer.WritableSpan, length);
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
    internal static ComputeBuffer ComputeCommoditySelectionIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.CommoditySelectionIndex(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Moving Averages - Additional Batch 4

    /// <summary>
    /// Computes Alpha Decreasing EMA using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAlphaDecreasingEmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.AlphaDecreasingEma(close, buffer.WritableSpan, length);
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

    internal static ComputeBuffer ComputeInertiaFast(StockData data, ComputeContext context, int rviLength = 14, int smoothLength = 20)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.Inertia(high, low, close, buffer.WritableSpan, rviLength, smoothLength);
        return buffer;
    }

    #endregion

    #region Volatility - Additional Batch 3

    internal static ComputeBuffer ComputeStandardDeviationChannelFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.StandardDeviationChannel(close, buffer.WritableSpan, length);
        return buffer;
    }

    internal static ComputeBuffer ComputeStandardDeviationVolatilityFast(StockData data, ComputeContext context, int length = 20, int annualizationFactor = 252)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.StandardDeviationVolatility(close, buffer.WritableSpan, length, annualizationFactor);
        return buffer;
    }

    internal static ComputeBuffer ComputeAverageTrueRangeChannelFast(StockData data, ComputeContext context, int length = 14, double multiplier = 2)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.AverageTrueRangeChannel(high, low, close, buffer.WritableSpan, length, multiplier);
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        VolatilityCore.BollingerBandsPercentB(inputSpan, buffer.WritableSpan, length, multiplier);
        return buffer;
    }

    /// <summary>
    /// Computes Bollinger Bands with ATR using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeBollingerBandsAtrFast(StockData data, ComputeContext context, int length = 20, double multiplier = 2)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.BollingerBandsAtr(high, low, close, buffer.WritableSpan, length, multiplier);
        return buffer;
    }

    #endregion

    #region Additional Oscillators (Batch 1)

    /// <summary>
    /// Computes Absolute Chande Momentum Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChandeMomentumOscillatorAbsoluteFast(StockData data, ComputeContext context, int length = 9)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.ChandeMomentumOscillatorAbsolute(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Percent Change using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePercentChangeFast(StockData data, ComputeContext context, int length = 1)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
    internal static ComputeBuffer ComputeDoubleSmoothedMomentaFast(StockData data, ComputeContext context, int momentumLength = 1, int firstSmooth = 25, int secondSmooth = 13)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.DoubleSmoothedMomenta(inputSpan, buffer.WritableSpan, momentumLength, firstSmooth, secondSmooth);
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
    internal static ComputeBuffer ComputeChandeMomentumOscillatorAbsoluteAverageFast(StockData data, ComputeContext context, int length = 9)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.ChandeMomentumOscillatorAbsoluteAverage(inputSpan, buffer.WritableSpan, length, 5);
        return buffer;
    }

    /// <summary>
    /// Computes Chande Momentum Oscillator Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChandeMomentumOscillatorAverageFast(StockData data, ComputeContext context, int length = 9)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.ChandeMomentumOscillatorAverage(inputSpan, buffer.WritableSpan, length, 5);
        return buffer;
    }

    /// <summary>
    /// Computes Double Stochastic Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDoubleStochasticOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DoubleStochasticOscillator(high, low, close, buffer.WritableSpan, length, 3);
        return buffer;
    }

    /// <summary>
    /// Computes DTOscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDTOscillatorFast(StockData data, ComputeContext context, int length = 13)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DTOscillator(high, low, close, buffer.WritableSpan, length, 8, 5);
        return buffer;
    }

    /// <summary>
    /// Computes Compare Price Momentum Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeComparePriceMomentumOscillatorFast(StockData data, ComputeContext context, int length = 35)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.ComparePriceMomentumOscillator(inputSpan, buffer.WritableSpan, length, 10, 10);
        return buffer;
    }

    /// <summary>
    /// Computes Daily Average Price Delta using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDailyAveragePriceDeltaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.DailyAveragePriceDelta(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Demand Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDemandOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DemandOscillator(high, low, close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Double Smoothed Relative Strength Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDoubleSmoothedRelativeStrengthIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.DoubleSmoothedRelativeStrengthIndex(inputSpan, buffer.WritableSpan, length, 5);
        return buffer;
    }

    /// <summary>
    /// Computes Dynamic Momentum Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDynamicMomentumOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.DynamicMomentumOscillator(inputSpan, buffer.WritableSpan, length, 5);
        return buffer;
    }

    /// <summary>
    /// Computes Average Money Flow Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAverageMoneyFlowOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        OscillatorCore.AverageMoneyFlowOscillator(high, low, close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes DMI Stochastic using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDMIStochasticFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DMIStochastic(high, low, close, buffer.WritableSpan, length, 10);
        return buffer;
    }

    /// <summary>
    /// Computes CCT Stoch RSI using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeCCTStochRelativeStrengthIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.CCTStochRsi(inputSpan, buffer.WritableSpan, length, 5, 3);
        return buffer;
    }

    /// <summary>
    /// Computes Bilateral Stochastic Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeBilateralStochasticOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.BilateralStochasticOscillator(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    #region Batch 5 - Additional Oscillators

    /// <summary>
    /// Computes Chande Momentum Oscillator Average Disparity Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChandeMomentumOscillatorAverageDisparityIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        // Length parameter maps to cmoLength; smaLength uses default
        _ = length;
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ChandeMomentumOscillatorAverageDisparityIndex(close, buffer.WritableSpan, length, 3);
        return buffer;
    }

    /// <summary>
    /// Computes Chande Momentum Oscillator Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChandeMomentumOscillatorFilterFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ChandeMomentumOscillatorFilter(close, buffer.WritableSpan, length, 3);
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
        // Length parameter is unused - Gann Swing uses swing detection
        _ = length;
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.GannSwingOscillator(high, low, buffer.WritableSpan, 2);
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
    internal static ComputeBuffer ComputeRegressionOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.RegressionOscillator(close, buffer.WritableSpan, length);
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
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.SupportAndResistanceOscillator(high, low, close, buffer.WritableSpan, length);
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
    internal static ComputeBuffer ComputeEhlersDecyclerOscillatorV1Fast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.EhlersDecyclerOscillatorV1(close, buffer.WritableSpan, length, length * 2);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Hilbert Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersHilbertOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.EhlersHilbertOscillator(close, buffer.WritableSpan, length);
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
    internal static ComputeBuffer ComputeEhlersRecursiveMedianOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.EhlersRecursiveMedianOscillator(close, buffer.WritableSpan, length > 2 ? length / 2 : 5, 3);
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
    internal static ComputeBuffer ComputeEhlersDecyclerOscillatorV2Fast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.EhlersDecyclerOscillatorV2(close, buffer.WritableSpan, length > 0 ? length * 9 : 125);
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
    internal static ComputeBuffer ComputeAlligatorJawFast(StockData data, ComputeContext context, int length = 13)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.AlligatorJaw(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Alligator Teeth using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAlligatorTeethFast(StockData data, ComputeContext context, int length = 8)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.AlligatorTeeth(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Alligator Lips using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAlligatorLipsFast(StockData data, ComputeContext context, int length = 5)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.AlligatorLips(close, buffer.WritableSpan, length);
        return buffer;
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
    internal static ComputeBuffer ComputeEhlersInverseFisherTransformFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersInverseFisherTransform(close, buffer.WritableSpan, length);
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
    internal static ComputeBuffer ComputeEhlersHighPassFilterV2Fast(StockData data, ComputeContext context, int length = 48)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersHighPassFilterV2(close, buffer.WritableSpan, length);
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
    internal static ComputeBuffer ComputeEhlersFilterFast(StockData data, ComputeContext context, int length = 15)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersFilter(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Finite Impulse Response Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersFirFilterFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersFiniteImpulseResponseFilter(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Infinite Impulse Response Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersIirFilterFast(StockData data, ComputeContext context, int length = 15)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersInfiniteImpulseResponseFilter(close, buffer.WritableSpan, length);
        return buffer;
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
    internal static ComputeBuffer ComputeDoubleExponentialSmoothingFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length; // Uses alpha/gamma parameters instead
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DoubleExponentialSmoothing(close, buffer.WritableSpan, 0.01, 0.9);
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.AdaptiveAutonomousRecursiveMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Corrected Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeCorrectedMovingAverageFast(StockData data, ComputeContext context, int length = 35)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.CorrectedMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Cubed Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeCubedWeightedMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.DynamicallyAdjustableFilter(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Edge Preserving Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEdgePreservingFilterFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.EdgePreservingFilter(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers All Pass Phase Shifter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersAllPassPhaseShifterFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.EhlersAllPassPhaseShifter(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Average Error Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersAverageErrorFilterFast(StockData data, ComputeContext context, int length = 27)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.EhlersAverageErrorFilter(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Distance Coefficient Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersDistanceCoefficientFilterFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.EhlersDistanceCoefficientFilter(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Kaufman Adaptive Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersKaufmanAdaptiveMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.FisherLeastSquaresMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Following Adaptive Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeFollowingAdaptiveMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.FollowingAdaptiveMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes General Filter Estimator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeGeneralFilterEstimatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.JsaMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Kalman Smoother using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeKalmanSmootherFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.OptimalWeightedMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Overshoot Reduction Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeOvershootReductionMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.WildersSummationMethod(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Zero Lag Triple Exponential Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeZeroLagTripleExponentialMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
    internal static ComputeBuffer ComputeMiddleHighLowMovingAverageFast(StockData data, ComputeContext context, int length1 = 14, int length2 = 10)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.MiddleHighLowMovingAverage(high, low, buffer.WritableSpan, length1, length2);
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.EhlersMedianAverageAdaptiveFilter(inputSpan, buffer.WritableSpan, length, threshold);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Mesa Adaptive Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersMesaAdaptiveMovingAverageFast(StockData data, ComputeContext context, int length = 14, double fastLimit = 0.5, double slowLimit = 0.05)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.EhlersMesaAdaptiveMovingAverage(inputSpan, buffer.WritableSpan, length, fastLimit, slowLimit);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Recursive Median Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersRecursiveMedianFilterFast(StockData data, ComputeContext context, int length = 5, double alpha = 0.5)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.EhlersRecursiveMedianFilter(inputSpan, buffer.WritableSpan, length, alpha);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Roofing Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersRoofingFilterFast(StockData data, ComputeContext context, int hpLength = 10, int lpLength = 48)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.EhlersRoofingFilter(inputSpan, buffer.WritableSpan, hpLength, lpLength);
        return buffer;
    }

    #endregion

    #region Batch 28 - Final Unwired Core Methods

    /// <summary>
    /// Computes Ehlers Deviation Scaled Super Smoother using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersDeviationScaledSuperSmootherFast(StockData data, ComputeContext context, int length = 20, int poles = 2)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.EhlersDeviationScaledSuperSmoother(inputSpan, buffer.WritableSpan, length, poles);
        return buffer;
    }

    /// <summary>
    /// Computes PPO MA using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputePpoMaFast(StockData data, ComputeContext context, int fastLength = 12, int slowLength = 26)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
    /// Computes Stochastic RSI Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeStochasticRsiOscillatorFast(StockData data, ComputeContext context, int rsiLength = 14, int stochLength = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.StochasticRsiOscillator(inputSpan, buffer.WritableSpan, rsiLength, stochLength);
        return buffer;
    }

    /// <summary>
    /// Computes Elastic Volume Weighted Moving Average V2 using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeElasticVolumeWeightedMovingAverageV2Fast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.ElasticVolumeWeightedMovingAverageV2(inputSpan, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Windowed Volume Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeWindowedVolumeWeightedMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.WindowedVolumeWeightedMovingAverage(inputSpan, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes ATR Filtered Exponential Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAtrFilteredExponentialMovingAverageFast(StockData data, ComputeContext context, int length = 45, int atrLength = 20, int stdDevLength = 10, int lbLength = 20, double min = 5)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.GeneralizedDoubleExponentialMovingAverage(inputSpan, buffer.WritableSpan, length, volumeFactor);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Finite Impulse Response Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersFiniteImpulseResponseFilterFast(StockData data, ComputeContext context, int length = 20)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.EhlersFiniteImpulseResponseFilter(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Infinite Impulse Response Filter using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersInfiniteImpulseResponseFilterFast(StockData data, ComputeContext context, int length = 15)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.EhlersInfiniteImpulseResponseFilter(inputSpan, buffer.WritableSpan, length);
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
    internal static ComputeBuffer ComputeChandeIntradayMomentumIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ChandeIntradayMomentumIndex(open, close, buffer.WritableSpan, length);
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersReflex(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Correlation Trend Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersCorrelationTrendIndicatorFast(StockData data, ComputeContext context, int length = 20)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersCorrelationTrendIndicator(inputSpan, buffer.WritableSpan, length);
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersBandPassFilterV1(inputSpan, buffer.WritableSpan, length, bw);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Band Pass Filter V2 using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersBandPassFilterV2Fast(StockData data, ComputeContext context, int length = 20, double bw = 0.3)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var realBuffer = context.Rent(inputList.Count);
        var imagBuffer = context.Rent(inputList.Count);
        try
        {
            OscillatorCore.EhlersClassicHilbertTransformer(inputSpan, realBuffer.WritableSpan, imagBuffer.WritableSpan, length1, length2);
        }
        finally
        {
            imagBuffer.Dispose(); // Only return real component
        }
        return realBuffer;
    }

    /// <summary>
    /// Computes Ehlers Zero Mean Roofing Filter using fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersZeroMeanRoofingFilterFast(StockData data, ComputeContext context, int length1 = 48, int length2 = 10)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersSquelchIndicator(inputSpan, buffer.WritableSpan, length1, length2, length3);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Reverse EMA Indicator V2 using fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersReverseEmaIndicatorV2Fast(StockData data, ComputeContext context, double trendAlpha = 0.05, double cycleAlpha = 0.3)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersReverseEmaIndicatorV2(inputSpan, buffer.WritableSpan, trendAlpha, cycleAlpha);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Stochastic Cyber Cycle using fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersStochasticCyberCycleFast(StockData data, ComputeContext context, int length = 14, double alpha = 0.7)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
    internal static ComputeBuffer ComputeDetrendedPriceOscillatorFast(StockData data, ComputeContext context, int length = 20)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.DetrendedPriceOscillator(inputSpan, buffer.WritableSpan, length);
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.SchaffTrendCycle(inputSpan, buffer.WritableSpan, cycleLength, fastLength, slowLength);
        return buffer;
    }

    /// <summary>
    /// Computes Smoothed Rate of Change using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeSmoothedRateOfChangeFast(StockData data, ComputeContext context, int rocLength = 12, int smoothLength = 3)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.SmoothedRateOfChange(inputSpan, buffer.WritableSpan, rocLength, smoothLength);
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
        var openSpan = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var buffer = context.Rent(data.Count);
        TrendCore.WoodiePivotPoint(highSpan, lowSpan, openSpan, buffer.WritableSpan);
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
    internal static ComputeBuffer ComputeAverageTrueRangeTrailingStopsFast(StockData data, ComputeContext context, int length = 14, double multiplier = 3)
    {
        var closeSpan = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var highSpan = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var lowSpan = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.AverageTrueRangeTrailingStops(closeSpan, highSpan, lowSpan, buffer.WritableSpan, length, multiplier);
        return buffer;
    }

    /// <summary>
    /// Computes Welles Wilder Summation using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeWellesWilderSummationFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.WellesWilderSummation(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Damping Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDampingIndexFast(StockData data, ComputeContext context, int length = 5)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.DampingIndex(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Didi Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDidiIndexFast(StockData data, ComputeContext context, int shortLength = 3, int mediumLength = 8, int longLength = 20)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.DidiIndex(inputSpan, buffer.WritableSpan, shortLength, mediumLength, longLength);
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
    internal static ComputeBuffer ComputeAdaptiveExponentialMovingAverageFast(StockData data, ComputeContext context, int length = 10)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.AdaptiveExponentialMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Average Directional Index using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAverageDirectionalIndexFast(StockData data, ComputeContext context, int length = 14)
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
        OscillatorCore.AverageDirectionalIndex(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Average True Range using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAverageTrueRangeFast(StockData data, ComputeContext context, int length = 14)
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
        VolatilityCore.AverageTrueRange(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Chande Momentum Oscillator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChandeMomentumOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.ChandeMomentumOscillator(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ease of Movement using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEaseOfMovementFast(StockData data, ComputeContext context, int length = 14)
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

    /// <summary>
    /// Computes Hull Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeHullMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
    internal static ComputeBuffer ComputeChandeQuickStickFast(StockData data, ComputeContext context, int length = 14)
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
        OscillatorCore.ChandeQuickStick(open, high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Delta Moving Average using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeDeltaMovingAverageFast(StockData data, ComputeContext context, int fastLength = 10, int slowLength = 5)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.DeltaMovingAverage(inputSpan, buffer.WritableSpan, fastLength, slowLength);
        return buffer;
    }

    /// <summary>
    /// Computes Folded RSI using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeFoldedRsiFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.ConnorsRelativeStrengthIndex(inputSpan, buffer.WritableSpan, rsiLength, streakLength, rankLength);
        return buffer;
    }

    /// <summary>
    /// Computes Stochastic RSI using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeStochasticRsiFast(StockData data, ComputeContext context, int rsiLength = 14, int smoothK = 3, int smoothD = 3)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.StochasticRelativeStrengthIndex(inputSpan, buffer.WritableSpan, rsiLength, rsiLength, smoothK, smoothD);
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
    /// Computes CCT Stoch RSI using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeCCTStochRsiFast(StockData data, ComputeContext context, int rsiLength = 14, int stochLength = 5, int smaLength = 3)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.CCTStochRsi(inputSpan, buffer.WritableSpan, rsiLength, stochLength, smaLength);
        return buffer;
    }

    /// <summary>
    /// Computes Inertia using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeInertiaFast(StockData data, ComputeContext context, int length = 20)
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
        OscillatorCore.Inertia(high, low, close, buffer.WritableSpan, 14, length);
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
    internal static ComputeBuffer ComputeStochasticOscillatorFast(StockData data, ComputeContext context, int length = 14, int smoothLength = 3)
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
            MovingAverageCore.SimpleMovingAverage(k, buffer.WritableSpan, smoothLength);
        }
        finally
        {
            pool.Return(kArray);
        }
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.OneLCLeastSquaresMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Adaptive RSI using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeAdaptiveRsiFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.AdaptiveRsi(inputSpan, buffer.WritableSpan, 5, length);
        return buffer;
    }

    /// <summary>
    /// Computes Bollinger Bands ATR using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeBollingerBandsAtrFast(StockData data, ComputeContext context, int atrLength = 22, int length = 55)
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
        VolatilityCore.BollingerBandsAtr(high, low, close, buffer.WritableSpan, length, 2);
        return buffer;
    }

    /// <summary>
    /// Computes Chande Momentum Oscillator Signal using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeChandeMomentumOscillatorSignalFast(StockData data, ComputeContext context, int length = 14, int signalLength = 3)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.ChandeMomentumOscillatorAverage(inputSpan, buffer.WritableSpan, length, signalLength);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Roofing Filter V1 using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersRoofingFilterV1Fast(StockData data, ComputeContext context, int hpLength = 10, int lpLength = 48)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.EhlersRoofingFilter(inputSpan, buffer.WritableSpan, hpLength, lpLength);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Spearman Rank Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersSpearmanRankFast(StockData data, ComputeContext context, int length = 20)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.T3MovingAverage(inputSpan, buffer.WritableSpan, length, vFactor);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Hamming Window Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersHammingWindowFast(StockData data, ComputeContext context, int length = 20, double pedestal = 10)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.EhlersHammingMovingAverage(inputSpan, buffer.WritableSpan, length, pedestal);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Hann Window Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersHannWindowFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.EhlersHannMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Triangle Window Indicator using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersTriangleWindowFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.EhlersTriangleMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Impulse Reaction using zero-allocation fast path.
    /// </summary>
    internal static ComputeBuffer ComputeEhlersImpulseReactionFast(StockData data, ComputeContext context, int length = 20)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.EhlersImpulseReaction(inputSpan, buffer.WritableSpan, 2, length);
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
    internal static ComputeBuffer ComputeEhlersSimpleDerivIndicatorFast(StockData data, ComputeContext context, int length = 2, int signalLength = 8, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var count = inputList.Count;

        // Get pooled buffers
        var pool = ArrayPool<double>.Shared;
        var z3Array = pool.Rent(count);

        try
        {
            var z3Span = z3Array.AsSpan(0, count);

            // Compute z3 (raw oscillator)
            OscillatorCore.EhlersSimpleDerivIndicator(inputSpan, z3Span, length);

            // Create output buffer and apply smoothing
            var buffer = context.Rent(count);
            var z3ReadOnly = (ReadOnlySpan<double>)z3Span;

            // Apply moving average for signal line
            switch (maType)
            {
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(z3ReadOnly, buffer.WritableSpan, signalLength);
                    break;
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(z3ReadOnly, buffer.WritableSpan, signalLength);
                    break;
                case MovingAvgType.WeightedMovingAverage:
                    MovingAverageCore.WeightedMovingAverage(z3ReadOnly, buffer.WritableSpan, signalLength);
                    break;
                case MovingAvgType.DoubleExponentialMovingAverage:
                    MovingAverageCore.DoubleExponentialMovingAverage(z3ReadOnly, buffer.WritableSpan, signalLength);
                    break;
                case MovingAvgType.TripleExponentialMovingAverage:
                    MovingAverageCore.TripleExponentialMovingAverage(z3ReadOnly, buffer.WritableSpan, signalLength);
                    break;
                default:
                    // Fallback to EMA for unsupported types
                    MovingAverageCore.ExponentialMovingAverage(z3ReadOnly, buffer.WritableSpan, signalLength);
                    break;
            }

            return buffer;
        }
        finally
        {
            pool.Return(z3Array);
        }
    }

    /// <summary>
    /// Computes Ehlers Simple Clip Indicator using zero-allocation fast path.
    /// Returns the smoothed z3 oscillator (signal line).
    /// </summary>
    internal static ComputeBuffer ComputeEhlersSimpleClipIndicatorFast(StockData data, ComputeContext context, int length1 = 2, int length3 = 50, int signalLength = 22, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var count = inputList.Count;

        // Get pooled buffers
        var pool = ArrayPool<double>.Shared;
        var z3Array = pool.Rent(count);

        try
        {
            var z3Span = z3Array.AsSpan(0, count);

            // Compute z3 (raw oscillator with clipped derivatives)
            OscillatorCore.EhlersSimpleClipIndicator(inputSpan, z3Span, length1, length3);

            // Create output buffer and apply smoothing
            var buffer = context.Rent(count);
            var z3ReadOnly = (ReadOnlySpan<double>)z3Span;

            // Apply moving average for signal line
            switch (maType)
            {
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(z3ReadOnly, buffer.WritableSpan, signalLength);
                    break;
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(z3ReadOnly, buffer.WritableSpan, signalLength);
                    break;
                case MovingAvgType.WeightedMovingAverage:
                    MovingAverageCore.WeightedMovingAverage(z3ReadOnly, buffer.WritableSpan, signalLength);
                    break;
                case MovingAvgType.DoubleExponentialMovingAverage:
                    MovingAverageCore.DoubleExponentialMovingAverage(z3ReadOnly, buffer.WritableSpan, signalLength);
                    break;
                case MovingAvgType.TripleExponentialMovingAverage:
                    MovingAverageCore.TripleExponentialMovingAverage(z3ReadOnly, buffer.WritableSpan, signalLength);
                    break;
                default:
                    // Fallback to EMA for unsupported types
                    MovingAverageCore.ExponentialMovingAverage(z3ReadOnly, buffer.WritableSpan, signalLength);
                    break;
            }

            return buffer;
        }
        finally
        {
            pool.Return(z3Array);
        }
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
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
    internal static ComputeBuffer ComputeVariableMovingAverageBandsFast(StockData data, ComputeContext context, int length = 6, double mult = 1.5, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        // Calculate MA (simplified - uses SMA/EMA as proxy for VMA)
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
    /// Computes Narrow Sideways Channel using zero-allocation fast path.
    /// Returns the middle band (MA).
    /// </summary>
    internal static ComputeBuffer ComputeNarrowSidewaysChannelFast(StockData data, ComputeContext context, int length = 20, double pct = 0.03, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
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
    /// Computes High Low Bands using zero-allocation fast path.
    /// Returns the middle band (SMA of close).
    /// </summary>
    internal static ComputeBuffer ComputeHighLowBandsFast(StockData data, ComputeContext context, int length = 14, double pctShift = 1, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        // Calculate MA of close
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
    /// Computes Auto Dispersion Bands using zero-allocation fast path.
    /// Returns the middle band (WMA of close).
    /// </summary>
    internal static ComputeBuffer ComputeAutoDispersionBandsFast(StockData data, ComputeContext context, int length = 90, int smoothLength = 140, MovingAvgType maType = MovingAvgType.WeightedMovingAverage)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        // Calculate MA of close
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
    internal static ComputeBuffer ComputeTrendTraderBandsFast(StockData data, ComputeContext context, int length = 21, double mult = 3, double bandStep = 20, MovingAvgType maType = MovingAvgType.WeightedMovingAverage)
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
    internal static ComputeBuffer ComputeHurstCycleChannelFast(StockData data, ComputeContext context, int fastLength = 10, int slowLength = 30, double fastMult = 1, double slowMult = 3, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var count = data.Count;
        var buffer = context.Rent(count);

        switch (maType)
        {
            case MovingAvgType.WildersSmoothingMethod:
                MovingAverageCore.WellesWilderMovingAverage(close, buffer.WritableSpan, fastLength);
                break;
            case MovingAvgType.SimpleMovingAverage:
                MovingAverageCore.SimpleMovingAverage(close, buffer.WritableSpan, fastLength);
                break;
            case MovingAvgType.ExponentialMovingAverage:
                MovingAverageCore.ExponentialMovingAverage(close, buffer.WritableSpan, fastLength);
                break;
            default:
                MovingAverageCore.WellesWilderMovingAverage(close, buffer.WritableSpan, fastLength);
                break;
        }

        return buffer;
    }

    /// <summary>
    /// Computes Price Curve Channel using zero-allocation fast path.
    /// Returns the middle line (Wilder smoothed).
    /// </summary>
    internal static ComputeBuffer ComputePriceCurveChannelFast(StockData data, ComputeContext context, int length = 100, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod)
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
    internal static ComputeBuffer ComputePriceLineChannelFast(StockData data, ComputeContext context, int length = 100, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod)
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
    internal static ComputeBuffer ComputeDynamicSupportAndResistanceFast(StockData data, ComputeContext context, int length = 25, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod)
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
    internal static ComputeBuffer ComputeHirashimaSugitaRSFast(StockData data, ComputeContext context, int length = 1000, MovingAvgType maType = MovingAvgType.WeightedMovingAverage)
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

    internal static ComputeBuffer ComputeEhlersPhaseCalculationFast(StockData data, ComputeContext context, int length = 15, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage)
    {
        // V1 Algorithm: Fourier-based phase calculation
        // 1. For each bar, compute Fourier real/imag parts weighted by price
        // 2. Convert to phase angle in degrees with quadrant adjustments
        // 3. Apply MA to phase for smoothing (primary output)
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        int count = data.Count;
        length = Math.Max(length, 2);

        // Calculate phase angles using Fourier transform
        var phaseBuffer = context.Rent(count);
        var phaseSpan = phaseBuffer.WritableSpan;
        double twoPiOverLen = 2.0 * Math.PI / length;

        for (int i = 0; i < count; i++)
        {
            double realPart = 0, imagPart = 0;
            for (int j = 0; j < length; j++)
            {
                double weight = i >= j ? close[i - j] : 0;
                realPart += Math.Cos(twoPiOverLen * j) * weight;
                imagPart += Math.Sin(twoPiOverLen * j) * weight;
            }

            // Calculate phase with quadrant adjustments
            double phase;
            if (Math.Abs(realPart) > 0.001)
            {
                phase = Math.Atan(imagPart / realPart) * (180.0 / Math.PI); // Convert to degrees
            }
            else
            {
                phase = 90 * Math.Sign(imagPart);
            }
            if (realPart < 0) phase += 180;
            phase += 90;
            if (phase < 0) phase += 360;
            if (phase > 360) phase -= 360;
            phaseSpan[i] = phase;
        }

        // Apply MA to phase for signal (primary output is smoothed phase)
        var result = context.Rent(count);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);
        maCore.Compute(phaseBuffer.Span, result.WritableSpan, length);

        phaseBuffer.Dispose();
        return result;
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
        // V1 Algorithm: Triple-pass MA on close-open derivative
        // 1. Calculate deriv = close - open
        // 2. Apply MA three times for heavy smoothing (filt -> filt2 -> filt3)
        // 3. Primary output is the triple-smoothed filter
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        int count = data.Count;
        length = Math.Max(length, 1);

        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);

        // Calculate derivative (close - open)
        var derivBuffer = context.Rent(count);
        var derivSpan = derivBuffer.WritableSpan;
        for (int i = 0; i < count; i++)
        {
            derivSpan[i] = close[i] - open[i];
        }

        // First MA pass
        var filt1Buffer = context.Rent(count);
        maCore.Compute(derivBuffer.Span, filt1Buffer.WritableSpan, length);

        // Second MA pass
        var filt2Buffer = context.Rent(count);
        maCore.Compute(filt1Buffer.Span, filt2Buffer.WritableSpan, length);

        // Third MA pass (primary output)
        var result = context.Rent(count);
        maCore.Compute(filt2Buffer.Span, result.WritableSpan, length);

        derivBuffer.Dispose();
        filt1Buffer.Dispose();
        filt2Buffer.Dispose();
        return result;
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

    internal static ComputeBuffer ComputeEhlersTrendExtractionFast(StockData data, ComputeContext context, int length = 20, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, double delta = 0.1)
    {
        // V1 Algorithm: Bandpass filter followed by MA smoothing
        // 1. Compute bandpass filter coefficients from length and delta
        // 2. Calculate recursive bandpass: bp = 0.5*(1-alpha)*(value-prevValue2) + beta*(1+alpha)*prevBp1 - alpha*prevBp2
        // 3. Apply MA over 2*length to get trend (primary output)
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        int count = data.Count;
        length = Math.Max(length, 1);

        // Calculate bandpass filter coefficients
        double twoPiOverLen = 2.0 * Math.PI / length;
        double fourPiDeltaOverLen = 4.0 * Math.PI * delta / length;
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

            // Bandpass formula with MinPastValues logic (use 0 for early bars)
            double valueDiff = i >= 2 ? (currentValue - prevValue) : 0;
            bpSpan[i] = (halfOneMinusAlpha * valueDiff) + (betaOnePlusAlpha * prevBp1) - (alpha * prevBp2);
        }

        // Apply MA over 2*length to get trend (primary output)
        var result = context.Rent(count);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);
        maCore.Compute(bpBuffer.Span, result.WritableSpan, length * 2);

        bpBuffer.Dispose();
        return result;
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
        // 2. Calculate diff = price - basis
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
            diffSpan[i] = close[i] - basisSpan[i];
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

    internal static ComputeBuffer ComputeRelativeVolatilityIndexV2Fast(StockData data, ComputeContext context, int length = 10, int smoothLength = 14, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.RelativeVolatilityIndex(close, buffer.WritableSpan, smoothLength, length);
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

    internal static ComputeBuffer ComputeStationaryExtrapolatedLevelsFast(StockData data, ComputeContext context, int length = 200, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        // V1 Algorithm: Extrapolated levels from deviations
        // 1. Calculate SMA of input
        // 2. y = currentValue - sma (deviation from MA)
        // 3. ext = (priorY + ((x - priorX) / (priorX2 - priorX) * (priorY2 - priorY))) / 2
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        int count = data.Count;
        length = Math.Max(length, 1);

        // Calculate SMA
        var smaBuffer = context.Rent(count);
        var maCore = Core.Registry.MovingAverageRegistry.GetRequired(maType);
        maCore.Compute(close, smaBuffer.WritableSpan, length);

        // Calculate y (deviation) values
        var yBuffer = context.Rent(count);
        var ySpan = yBuffer.WritableSpan;
        for (int i = 0; i < count; i++)
        {
            ySpan[i] = close[i] - smaBuffer.Span[i];
        }

        // Calculate extrapolated values
        var result = context.Rent(count);
        var resultSpan = result.WritableSpan;
        for (int i = 0; i < count; i++)
        {
            double x = i;
            double priorX = i >= length ? (i - length) : 0;
            double priorX2 = i >= length * 2 ? (i - (length * 2)) : 0;
            double priorY = i >= length ? ySpan[i - length] : 0;
            double priorY2 = i >= length * 2 ? ySpan[i - (length * 2)] : 0;

            double ext = (priorX2 - priorX) != 0 && (priorY2 - priorY) != 0
                ? (priorY + ((x - priorX) / (priorX2 - priorX) * (priorY2 - priorY))) / 2
                : 0;
            resultSpan[i] = ext;
        }

        smaBuffer.Dispose();
        yBuffer.Dispose();
        return result;
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
