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
        // Only use fast path for single-output indicators with typed options
        if (spec.Output != IndicatorOutput.Primary)
        {
            return null;
        }

        return spec.Options switch
        {
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

            _ => null
        };
    }

    #region Moving Averages

    /// <summary>
    /// Computes Simple Moving Average using zero-allocation fast path.
    /// Uses MovingAverageCore with span-based computation directly into pooled buffer.
    /// </summary>
    public static ComputeBuffer ComputeSmaFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeEmaFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeWmaFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeDemaFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeTemaFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeHmaFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeTmaFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeWwmaFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeLinRegFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeKamaFast(StockData data, ComputeContext context, int length = 10)
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
    public static ComputeBuffer ComputeZlemaFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeRsiFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeRocFast(StockData data, ComputeContext context, int length = 12)
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
    public static ComputeBuffer ComputeMomentumFast(StockData data, ComputeContext context, int length = 10)
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
    public static ComputeBuffer ComputeWilliamsRFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeCciFast(StockData data, ComputeContext context, int length = 20)
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
    public static ComputeBuffer ComputeStochasticKFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeAdxFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeCmoFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputePpoFast(StockData data, ComputeContext context, int fastLength = 12, int slowLength = 26)
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
    public static ComputeBuffer ComputeApoFast(StockData data, ComputeContext context, int fastLength = 12, int slowLength = 26)
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
    public static ComputeBuffer ComputeUltimateOscillatorFast(StockData data, ComputeContext context, int length1 = 7, int length2 = 14, int length3 = 28)
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
    public static ComputeBuffer ComputeTsiFast(StockData data, ComputeContext context, int longLength = 25, int shortLength = 13)
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
    public static ComputeBuffer ComputeStochRsiFast(StockData data, ComputeContext context, int rsiLength = 14, int stochLength = 14)
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
    public static ComputeBuffer ComputeAroonOscillatorFast(StockData data, ComputeContext context, int length = 25)
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
    public static ComputeBuffer ComputeDpoFast(StockData data, ComputeContext context, int length = 20)
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
    public static ComputeBuffer ComputeTrixFast(StockData data, ComputeContext context, int length = 15)
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
    public static ComputeBuffer ComputeMassIndexFast(StockData data, ComputeContext context, int emaLength = 9, int sumLength = 25)
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
    public static ComputeBuffer ComputeObvFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeAdlFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeCmfFast(StockData data, ComputeContext context, int length = 20)
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
    public static ComputeBuffer ComputeForceIndexFast(StockData data, ComputeContext context, int length = 13)
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
    public static ComputeBuffer ComputeVrocFast(StockData data, ComputeContext context, int length = 12)
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
    public static ComputeBuffer ComputeNviFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputePviFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputePvtFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeVwapFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeChaikinOscillatorFast(StockData data, ComputeContext context, int fastLength = 3, int slowLength = 10)
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
    public static ComputeBuffer ComputeEmvFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeAtrFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeStdDevFast(StockData data, ComputeContext context, int length = 20)
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
    public static ComputeBuffer ComputeParabolicSarFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeSuperTrendFast(StockData data, ComputeContext context, int length = 10)
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
    public static ComputeBuffer ComputeDonchianChannelFast(StockData data, ComputeContext context, int length = 20)
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
    public static ComputeBuffer ComputeHighestHighFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeLowestLowFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeAdrFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeTypicalPriceFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeMedianPriceFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeWeightedCloseFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputePercentageChangeFast(StockData data, ComputeContext context, int length = 1)
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
    public static ComputeBuffer ComputeLinRegSlopeFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeRSquaredFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeStandardErrorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeVhfFast(StockData data, ComputeContext context, int length = 28)
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
    public static ComputeBuffer ComputeHistoricalVolatilityFast(StockData data, ComputeContext context, int length = 20)
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
    public static ComputeBuffer ComputeChaikinVolatilityFast(StockData data, ComputeContext context, int length = 10)
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
    public static ComputeBuffer ComputeUlcerIndexFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeNormalizedAtrFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeVarianceFast(StockData data, ComputeContext context, int length = 20)
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
    public static ComputeBuffer ComputeCoefficientOfVariationFast(StockData data, ComputeContext context, int length = 20)
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
    public static ComputeBuffer ComputeTrueRangeFast(StockData data, ComputeContext context, int length = 14)
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
    /// Computes Bollinger Bands Middle (SMA) using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeBollingerBandsFast(StockData data, ComputeContext context, int length = 20)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        // Bollinger Bands middle is just SMA
        MovingAverageCore.SimpleMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Additional Volume Indicators

    /// <summary>
    /// Computes Klinger Volume Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeKlingerVolumeFast(StockData data, ComputeContext context, int length = 34)
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
    public static ComputeBuffer ComputeVpciFast(StockData data, ComputeContext context, int length = 5)
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
    public static ComputeBuffer ComputeMoneyFlowIndexFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeBalanceOfPowerFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeRelativeVigorIndexFast(StockData data, ComputeContext context, int length = 10)
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
    public static ComputeBuffer ComputeAroonUpFast(StockData data, ComputeContext context, int length = 25)
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
    public static ComputeBuffer ComputeAroonDownFast(StockData data, ComputeContext context, int length = 25)
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
    public static ComputeBuffer ComputeKeltnerChannelMiddleFast(StockData data, ComputeContext context, int length = 20)
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
    public static ComputeBuffer ComputeTrendDetectionFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputePriceChannelMiddleFast(StockData data, ComputeContext context, int length = 20)
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
    public static ComputeBuffer ComputeStochasticDFast(StockData data, ComputeContext context, int length = 14)
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
        OscillatorCore.StochasticD(high, low, close, buffer.WritableSpan, length, 3);
        return buffer;
    }

    /// <summary>
    /// Computes Awesome Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAwesomeOscillatorFast(StockData data, ComputeContext context, int length = 5)
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
    public static ComputeBuffer ComputeAcceleratorOscillatorFast(StockData data, ComputeContext context, int length = 5)
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
    public static ComputeBuffer ComputePvoFast(StockData data, ComputeContext context, int length = 12)
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
    public static ComputeBuffer ComputeFisherTransformFast(StockData data, ComputeContext context, int length = 10)
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
    public static ComputeBuffer ComputeConnorsRsiFast(StockData data, ComputeContext context, int length = 3)
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
    public static ComputeBuffer ComputePmoFast(StockData data, ComputeContext context, int length = 35)
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
    public static ComputeBuffer ComputeKstFast(StockData data, ComputeContext context, int length = 10)
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
    public static ComputeBuffer ComputePercentRankFast(StockData data, ComputeContext context, int length = 100)
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
    public static ComputeBuffer ComputeChoppinessIndexFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeSmmaFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeMcGinleyDynamicFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeT3Fast(StockData data, ComputeContext context, int length = 5)
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
    public static ComputeBuffer ComputeVidyaFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeVmaFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeMacdLineFast(StockData data, ComputeContext context, int fastLength = 12, int slowLength = 26)
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
    public static ComputeBuffer ComputeMacdSignalFast(StockData data, ComputeContext context, int fastLength = 12, int slowLength = 26, int signalLength = 9)
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
    public static ComputeBuffer ComputeMacdHistogramFast(StockData data, ComputeContext context, int fastLength = 12, int slowLength = 26, int signalLength = 9)
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
    public static ComputeBuffer ComputeAbsoluteStrengthIndexFast(StockData data, ComputeContext context, int length = 10)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.AbsoluteStrengthIndex(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Relative Momentum Index using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeRelativeMomentumIndexFast(StockData data, ComputeContext context, int length = 14, int momentum = 4)
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
    public static ComputeBuffer ComputeIntradayMomentumIndexFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeSwingIndexFast(StockData data, ComputeContext context, double limitMove = 0)
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
    public static ComputeBuffer ComputeAccumulativeSwingIndexFast(StockData data, ComputeContext context, double limitMove = 0)
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
    public static ComputeBuffer ComputeCoppockCurveFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeChandeForecastOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeBullPowerFast(StockData data, ComputeContext context, int length = 13)
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
    public static ComputeBuffer ComputeBearPowerFast(StockData data, ComputeContext context, int length = 13)
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
    public static ComputeBuffer ComputePolarizedFractalEfficiencyFast(StockData data, ComputeContext context, int length = 10)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.PolarizedFractalEfficiency(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Schaff Trend Cycle using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeSchaffTrendCycleFast(StockData data, ComputeContext context, int length = 10)
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
    public static ComputeBuffer ComputePriceZoneOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.PriceZoneOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Elder Force Index using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeElderForceIndexFast(StockData data, ComputeContext context, int length = 13)
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
    public static ComputeBuffer ComputePrettyGoodOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeRelativeVolatilityIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.RelativeVolatilityIndex(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Qstick using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeQstickFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeSpecialKFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeAlmaFast(StockData data, ComputeContext context, int length = 9)
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
    public static ComputeBuffer ComputeLsmaFast(StockData data, ComputeContext context, int length = 25)
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
    public static ComputeBuffer ComputeFramaFast(StockData data, ComputeContext context, int length = 16)
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
    public static ComputeBuffer ComputeAmaFast(StockData data, ComputeContext context, int length = 10)
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
    public static ComputeBuffer ComputeSineWmaFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeHammingMaFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeGeoMaFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeRegularizedEmaFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeModifiedMaFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeZigZagFast(StockData data, ComputeContext context, int length = 5)
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
    public static ComputeBuffer ComputeChandelierExitLongFast(StockData data, ComputeContext context, int length = 22)
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
    public static ComputeBuffer ComputeChandelierExitShortFast(StockData data, ComputeContext context, int length = 22)
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
    public static ComputeBuffer ComputeTrendIntensityIndexFast(StockData data, ComputeContext context, int length = 30)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.TrendIntensityIndex(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Average Price using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAveragePriceFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputePivotPointFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeRangeFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputePriceMomentumFast(StockData data, ComputeContext context, int length = 10)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.PriceMomentum(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Midpoint using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeMidpointFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.Midpoint(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Midprice using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeMidpriceFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeMfiCoreFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeTradeVolumeIndexFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeVolumeOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.VolumeOscillator(volume, buffer.WritableSpan, 5, length);
        return buffer;
    }

    /// <summary>
    /// Computes Volume Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeVwmaFast(StockData data, ComputeContext context, int length = 20)
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
    public static ComputeBuffer ComputeTwiggsMoneyFlowFast(StockData data, ComputeContext context, int length = 21)
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
    public static ComputeBuffer ComputeVolumeZoneOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeDemandIndexFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeDisparityIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DisparityIndex(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Directional Trend Index using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeDirectionalTrendIndexFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeDoubleSmoothedStochasticFast(StockData data, ComputeContext context, int length = 10)
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
    public static ComputeBuffer ComputeDynamicMomentumIndexFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeErgodicCandlestickOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeDemarkerFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeSmoothedRocFast(StockData data, ComputeContext context, int length = 12)
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
    public static ComputeBuffer ComputeStandardErrorCoreFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.StandardError(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Keltner Channel Width using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeKeltnerChannelWidthFast(StockData data, ComputeContext context, int length = 20)
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
    public static ComputeBuffer ComputeBollingerBandsWidthFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.BollingerBandsWidth(close, buffer.WritableSpan, length, 2);
        return buffer;
    }

    /// <summary>
    /// Computes Relative Volatility Index using zero-allocation fast path via VolatilityCore.
    /// </summary>
    public static ComputeBuffer ComputeRviVolatilityFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.RelativeVolatilityIndex(close, buffer.WritableSpan, length, 10);
        return buffer;
    }

    /// <summary>
    /// Computes Donchian Channel Width using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeDonchianChannelWidthFast(StockData data, ComputeContext context, int length = 20)
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
    public static ComputeBuffer ComputeMassIndexCoreFast(StockData data, ComputeContext context, int length = 25)
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
    public static ComputeBuffer ComputeCloseToCloseVolatilityFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.CloseToCloseVolatility(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Parkinson Volatility using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeParkinsonVolatilityFast(StockData data, ComputeContext context, int length = 20)
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
    public static ComputeBuffer ComputeGarmanKlassVolatilityFast(StockData data, ComputeContext context, int length = 20)
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
    public static ComputeBuffer ComputeJmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.JurikMovingAverage(close, buffer.WritableSpan, length, 0);
        return buffer;
    }

    /// <summary>
    /// Computes Butterworth Filter using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeButterworthFilterFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.ButterworthFilter(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes SuperSmoother Filter using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeSuperSmootherFast(StockData data, ComputeContext context, int length = 10)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.SuperSmoother(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes EndPoint Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeEndPointMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EndpointMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Cubic Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeCubicWmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.CubicWeightedMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Natural Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeNaturalMaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.NaturalMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Elliott Wave Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeElliottWaveOscillatorFast(StockData data, ComputeContext context, int fastLength = 5, int slowLength = 35)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ElliottWaveOscillator(close, buffer.WritableSpan, fastLength, slowLength);
        return buffer;
    }

    /// <summary>
    /// Computes Forecast Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeForecastOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ForecastOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Derivative Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeDerivativeOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DerivativeOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Gator Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeGatorOscillatorFast(StockData data, ComputeContext context, int length = 13)
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
    public static ComputeBuffer ComputeFractalChaosOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeRahulMohindarOscillatorFast(StockData data, ComputeContext context, int length = 10)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.RahulMohindarOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Premier Stochastic Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputePremierStochasticFast(StockData data, ComputeContext context, int length = 8)
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
    public static ComputeBuffer ComputeRepulseFast(StockData data, ComputeContext context, int length = 5)
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
    public static ComputeBuffer ComputeGannHiLoActivatorFast(StockData data, ComputeContext context, int length = 3)
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
    public static ComputeBuffer ComputeHalfTrendFast(StockData data, ComputeContext context, int length = 2)
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
    public static ComputeBuffer ComputeVortexPositiveFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeVortexNegativeFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeLinRegInterceptFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.LinearRegressionIntercept(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Elder Impulse System using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeElderImpulseSystemFast(StockData data, ComputeContext context, int length = 13)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.ElderImpulseSystem(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ichimoku Tenkan-sen using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeIchimokuTenkanSenFast(StockData data, ComputeContext context, int length = 9)
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
    public static ComputeBuffer ComputeIchimokuKijunSenFast(StockData data, ComputeContext context, int length = 26)
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
    public static ComputeBuffer ComputeMassThrustFast(StockData data, ComputeContext context, int length = 10)
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
    public static ComputeBuffer ComputeWilliamsADFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeNetVolumeFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeCumulativeVolumeIndexFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeVolumeMomentumFast(StockData data, ComputeContext context, int length = 10)
    {
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.VolumeMomentum(volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Volume Price Trend using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeVolumePriceTrendFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeElderRayBullPowerFast(StockData data, ComputeContext context, int length = 13)
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
    public static ComputeBuffer ComputeElderRayBearPowerFast(StockData data, ComputeContext context, int length = 13)
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
    public static ComputeBuffer ComputeNormalizedVolumeFast(StockData data, ComputeContext context, int length = 20)
    {
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.NormalizedVolume(volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Volume Weighted RSI using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeVolumeWeightedRsiFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeChandeCompositeMomentumIndexFast(StockData data, ComputeContext context, int shortLength = 3, int longLength = 10)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ChandeCompositeMomentumIndex(close, buffer.WritableSpan, shortLength, longLength);
        return buffer;
    }

    /// <summary>
    /// Computes Chande Kroll R-Squared Index using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeChandeKrollRSquaredIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ChandeKrollRSquaredIndex(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Bayesian Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeBayesianOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.BayesianOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Anchored Momentum using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAnchoredMomentumFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.AnchoredMomentum(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Chartmill Value Indicator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeChartmillValueIndicatorFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ChartmillValueIndicator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Center of Linearity using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeCenterOfLinearityFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.CenterOfLinearity(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Breakout RSI using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeBreakoutRsiFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.BreakoutRsi(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Asymmetrical RSI using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAsymmetricalRsiFast(StockData data, ComputeContext context, int upLength = 14, int downLength = 7)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.AsymmetricalRsi(close, buffer.WritableSpan, upLength, downLength);
        return buffer;
    }

    /// <summary>
    /// Computes Adaptive Stochastic using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAdaptiveStochasticFast(StockData data, ComputeContext context, int minLength = 5, int maxLength = 20)
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
    public static ComputeBuffer ComputeAdaptiveRsiFast(StockData data, ComputeContext context, int minLength = 5, int maxLength = 20)
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
    public static ComputeBuffer ComputeChandeTrendScoreFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.ChandeTrendScore(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Chop Zone using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeChopZoneFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeAutoLineFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.AutoLine(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Auto Line with Drift using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAutoLineWithDriftFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.AutoLineWithDrift(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Auto Filter using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAutoFilterFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.AutoFilter(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Buff Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeBuffAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.BuffAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Bryant Adaptive Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeBryantAdaptiveMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.BryantAdaptiveMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes ATR Trailing Stops using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAtrTrailingStopsFast(StockData data, ComputeContext context, int length = 14, double multiplier = 3)
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
    public static ComputeBuffer ComputeCompoundRatioMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.CompoundRatioMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Conditional Accumulator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeConditionalAccumulatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.ConditionalAccumulator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ahrens Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAhrensMovingAverageFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeRogersSatchellVolatilityFast(StockData data, ComputeContext context, int length = 20)
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
    public static ComputeBuffer ComputeYangZhangVolatilityFast(StockData data, ComputeContext context, int length = 20)
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
    public static ComputeBuffer ComputeCalmarRatioFast(StockData data, ComputeContext context, int length = 252)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.CalmarRatio(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Downside Deviation using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeDownsideDeviationFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.DownsideDeviation(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes ATR Channel Width using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAtrChannelWidthFast(StockData data, ComputeContext context, int length = 14, double multiplier = 2)
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
    public static ComputeBuffer ComputeCommoditySelectionIndexFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeAlphaDecreasingEmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.AlphaDecreasingEma(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Adaptive EMA using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAdaptiveEmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.AdaptiveExponentialMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Autonomous Recursive MA using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAutonomousRecursiveMaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.AutonomousRecursiveMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Adaptive Least Squares using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAdaptiveLeastSquaresFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.AdaptiveLeastSquares(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes ATR Filtered EMA using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAtrFilteredEmaFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeMedianMaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.MedianMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Volume Adjusted Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeVolumeAdjustedMaFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeQuadraticWmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.QuadraticWeightedMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Parabolic Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeParabolicWmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.ParabolicWeightedMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Oscillators - Additional Batch 8

    public static ComputeBuffer ComputeSmoothedWilliamsRFast(StockData data, ComputeContext context, int length = 14, int smoothLength = 3)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.SmoothedWilliamsR(high, low, close, buffer.WritableSpan, length, smoothLength);
        return buffer;
    }

    public static ComputeBuffer ComputePriceOscillatorPercentFast(StockData data, ComputeContext context, int shortLength = 10, int longLength = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.PriceOscillatorPercent(close, buffer.WritableSpan, shortLength, longLength);
        return buffer;
    }

    public static ComputeBuffer ComputeNormalizedMacdFast(StockData data, ComputeContext context, int fastLength = 12, int slowLength = 26)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.NormalizedMacd(close, buffer.WritableSpan, fastLength, slowLength);
        return buffer;
    }

    public static ComputeBuffer ComputeRelativeVigorIndexSignalFast(StockData data, ComputeContext context, int length = 10, int signalLength = 4)
    {
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.RelativeVigorIndexSignal(open, high, low, close, buffer.WritableSpan, length, signalLength);
        return buffer;
    }

    public static ComputeBuffer ComputeVolumeMomentumOscillatorFast(StockData data, ComputeContext context, int shortLength = 5, int longLength = 20)
    {
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        OscillatorCore.VolumeMomentumOscillator(volume, buffer.WritableSpan, shortLength, longLength);
        return buffer;
    }

    public static ComputeBuffer ComputeTrendContinuationFactorFast(StockData data, ComputeContext context, int length = 35)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.TrendContinuationFactor(close, buffer.WritableSpan, length);
        return buffer;
    }

    public static ComputeBuffer ComputeTrendPersistenceRateFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.TrendPersistenceRate(close, buffer.WritableSpan, length);
        return buffer;
    }

    public static ComputeBuffer ComputeInertiaFast(StockData data, ComputeContext context, int rviLength = 14, int smoothLength = 20)
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

    public static ComputeBuffer ComputeStandardDeviationChannelFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.StandardDeviationChannel(close, buffer.WritableSpan, length);
        return buffer;
    }

    public static ComputeBuffer ComputeStandardDeviationVolatilityFast(StockData data, ComputeContext context, int length = 20, int annualizationFactor = 252)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.StandardDeviationVolatility(close, buffer.WritableSpan, length, annualizationFactor);
        return buffer;
    }

    public static ComputeBuffer ComputeAverageTrueRangeChannelFast(StockData data, ComputeContext context, int length = 14, double multiplier = 2)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.AverageTrueRangeChannel(high, low, close, buffer.WritableSpan, length, multiplier);
        return buffer;
    }

    public static ComputeBuffer ComputeVolatilityRatioFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.VolatilityRatio(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    public static ComputeBuffer ComputeVolatilityStopFast(StockData data, ComputeContext context, int length = 14, double multiplier = 2)
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
    public static ComputeBuffer ComputeBollingerBandsPercentBFast(StockData data, ComputeContext context, int length = 20, double multiplier = 2)
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
    public static ComputeBuffer ComputeBollingerBandsAtrFast(StockData data, ComputeContext context, int length = 20, double multiplier = 2)
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
    public static ComputeBuffer ComputeChandeMomentumOscillatorAbsoluteFast(StockData data, ComputeContext context, int length = 9)
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
    public static ComputeBuffer ComputePercentChangeFast(StockData data, ComputeContext context, int length = 1)
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
    public static ComputeBuffer ComputePriceChangeFast(StockData data, ComputeContext context)
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
    public static ComputeBuffer ComputeRangeFast(StockData data, ComputeContext context)
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
    public static ComputeBuffer ComputeMidRangeFast(StockData data, ComputeContext context)
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
    public static ComputeBuffer ComputeOhlcAverageFast(StockData data, ComputeContext context)
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
    public static ComputeBuffer ComputeHlcAverageFast(StockData data, ComputeContext context)
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
    public static ComputeBuffer ComputeDoubleSmoothedMomentaFast(StockData data, ComputeContext context, int momentumLength = 1, int firstSmooth = 25, int secondSmooth = 13)
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
    public static ComputeBuffer ComputeHighLowIndexFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeMarketFacilitationIndexFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeTrendScoreFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeMedianValueFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeLogReturnsFast(StockData data, ComputeContext context, int length = 1)
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
    public static ComputeBuffer ComputeSimpleReturnsFast(StockData data, ComputeContext context, int length = 1)
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
    public static ComputeBuffer ComputeCumulativeSumFast(StockData data, ComputeContext context)
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
    public static ComputeBuffer ComputeRollingMaxFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeRollingMinFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputePricePositionFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeAtrPercentFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeChandeMomentumOscillatorAbsoluteAverageFast(StockData data, ComputeContext context, int length = 9)
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
    public static ComputeBuffer ComputeChandeMomentumOscillatorAverageFast(StockData data, ComputeContext context, int length = 9)
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
    public static ComputeBuffer ComputeDoubleStochasticOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeDTOscillatorFast(StockData data, ComputeContext context, int length = 13)
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
    public static ComputeBuffer ComputeComparePriceMomentumOscillatorFast(StockData data, ComputeContext context, int length = 35)
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
    public static ComputeBuffer ComputeDailyAveragePriceDeltaFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeDemandOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeDoubleSmoothedRelativeStrengthIndexFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeDynamicMomentumOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeAverageMoneyFlowOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeDMIStochasticFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeCCTStochRelativeStrengthIndexFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeBilateralStochasticOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeChandeMomentumOscillatorAverageDisparityIndexFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeChandeMomentumOscillatorFilterFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ChandeMomentumOscillatorFilter(close, buffer.WritableSpan, length, 3);
        return buffer;
    }

    /// <summary>
    /// Computes DiNapoli Percentage Price Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeDiNapoliPercentagePriceOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeDiNapoliPreferredStochasticOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeErgodicPercentagePriceOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeFastSlowKurtosisOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeFastSlowRsiOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeFastSlowStochasticOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeGOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeGannSwingOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeGannTrendOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeFireflyOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeFisherTransformStochasticOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeKarobeinOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.KarobeinOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Grover Llorens Cycle Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeGroverLlorensCycleOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeImpulsePercentagePriceOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ImpulsePercentagePriceOscillator(close, buffer.WritableSpan, 12, 26, length);
        return buffer;
    }

    /// <summary>
    /// Computes Linda Raschke 3/10 Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeLindaRaschke310OscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeMidpointOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeMirroredPercentagePriceOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeMobilityOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputePercentChangeOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.PercentChangeOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Price Cycle Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputePriceCycleOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.PriceCycleOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Price Volume Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputePriceVolumeOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeProjectionOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeRainbowOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.RainbowOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Regression Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeRegressionOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.RegressionOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Rex Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeRexOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeSentimentZoneOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeWaveTrendOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeWamiOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeVolumeAccumulationOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeKasePeakOscillatorV1Fast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeVaradiOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.VaradiOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Prime Number Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputePrimeNumberOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.PrimeNumberOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Trigonometric Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeTrigonometricOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.TrigonometricOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ultimate Trader Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeUltimateTraderOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeSmoothedDeltaRatioOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.SmoothedDeltaRatioOscillator(close, buffer.WritableSpan, length, 3);
        return buffer;
    }

    /// <summary>
    /// Computes Fast Slow Degree Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeFastSlowDegreeOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.FastSlowDegreeOscillator(close, buffer.WritableSpan, length / 2, length);
        return buffer;
    }

    /// <summary>
    /// Computes Robust Weighting Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeRobustWeightingOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeKasePeakOscillatorV2Fast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeStochasticCustomOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputePivotDetectorOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeTickLineMomentumOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.TickLineMomentumOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Support and Resistance Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeSupportAndResistanceOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeTradingMadeMoreSimplerOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeNthOrderDifferencingOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.NthOrderDifferencingOscillator(close, buffer.WritableSpan, length, 2);
        return buffer;
    }

    /// <summary>
    /// Computes Osc Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeOscOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeEhlersCenterOfGravityOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.EhlersCenterOfGravityOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Decycler Oscillator V1 using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeEhlersDecyclerOscillatorV1Fast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.EhlersDecyclerOscillatorV1(close, buffer.WritableSpan, length, length * 2);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Hilbert Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeEhlersHilbertOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.EhlersHilbertOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Universal Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeEhlersUniversalOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.EhlersUniversalOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Recursive Median Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeEhlersRecursiveMedianOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.EhlersRecursiveMedianOscillator(close, buffer.WritableSpan, length > 2 ? length / 2 : 5, 3);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Stochastic Center of Gravity Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeEhlersStochasticCenterOfGravityOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.EhlersStochasticCenterOfGravityOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Fisherized Deviation Scaled Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeEhlersFisherizedDeviationScaledOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.EhlersFisherizedDeviationScaledOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Adaptive Center of Gravity Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeEhlersAdaptiveCenterOfGravityOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeVervoortSmoothedOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.VervoortSmoothedOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Relative Difference of Squares Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeRelativeDifferenceOfSquaresOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.RelativeDifferenceOfSquaresOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Linear Quadratic Convergence Divergence Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeLinearQuadraticConvergenceDivergenceOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.LinearQuadraticConvergenceDivergenceOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Stationary Extrapolated Levels Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeStationaryExtrapolatedLevelsOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.StationaryExtrapolatedLevelsOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Percentage Price Oscillator Leader using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputePercentagePriceOscillatorLeaderFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.PercentagePriceOscillatorLeader(close, buffer.WritableSpan, 12, 26);
        return buffer;
    }

    /// <summary>
    /// Computes Kaufman Adaptive Correlation Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeKaufmanAdaptiveCorrelationOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.KaufmanAdaptiveCorrelationOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Stochastic MACD Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeStochasticMacdOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.StochasticMacdOscillator(close, buffer.WritableSpan, 12, 26, 9, length);
        return buffer;
    }

    /// <summary>
    /// Computes McClellan Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeMcClellanOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeEhlersDecyclerOscillatorV2Fast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.EhlersDecyclerOscillatorV2(close, buffer.WritableSpan, length > 0 ? length * 9 : 125);
        return buffer;
    }

    /// <summary>
    /// Computes Vervoort Heiken Ashi Candlestick Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeVervoortHeikenAshiCandlestickOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeVervoortHeikenAshiLongTermCandlestickOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeDecisionPointBreadthSwenlinTradingOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DecisionPointBreadthSwenlinTradingOscillator(close, buffer.WritableSpan, Math.Max(1, length / 3), length * 7);
        return buffer;
    }

    /// <summary>
    /// Computes Decision Point Price Momentum Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeDecisionPointPriceMomentumOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DecisionPointPriceMomentumOscillator(close, buffer.WritableSpan, length > 0 ? length * 2 + 7 : 35, length > 0 ? length + 6 : 20);
        return buffer;
    }

    /// <summary>
    /// Computes TFS MBO Percentage Price Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeTFSMboPercentagePriceOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.TFSMboPercentagePriceOscillator(close, buffer.WritableSpan, length > 0 ? length + 11 : 25, length > 0 ? length * 14 : 200);
        return buffer;
    }

    /// <summary>
    /// Computes TFS Volume Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeTFSVolumeOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        OscillatorCore.TFSVolumeOscillator(volume, buffer.WritableSpan, length > 0 ? length - 1 : 13, length > 0 ? length * 4 : 55);
        return buffer;
    }

    /// <summary>
    /// Computes Mass Thrust Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeMassThrustOscillatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeUltimateMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.UltimateMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Symmetrically Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeSymmetricallyWeightedMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.SymmetricallyWeightedMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Square Root Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeSquareRootWeightedMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.SquareRootWeightedMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Spencer 15-Point Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeSpencer15PointMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.Spencer15PointMovingAverage(close, buffer.WritableSpan, 15);
        return buffer;
    }

    /// <summary>
    /// Computes Spencer 21-Point Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeSpencer21PointMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.Spencer21PointMovingAverage(close, buffer.WritableSpan, 21);
        return buffer;
    }

    /// <summary>
    /// Computes Slow Smoothed Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeSlowSmoothedMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.SlowSmoothedMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Repulsion Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeRepulsionMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.RepulsionMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Quick Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeQuickMovingAverageFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeEhlersBetterExponentialMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersBetterExponentialMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Deviation Scaled Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeEhlersDeviationScaledMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersDeviationScaledMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Hann Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeEhlersHannMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersHannMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Triangle Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeEhlersTriangleMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersTriangleMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Elastic Volume Weighted Moving Average V1 using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeElasticVolumeWeightedMovingAverageV1Fast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeHoltExponentialMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.HoltExponentialMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Pentuple Exponential Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputePentupleExponentialMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.PentupleExponentialMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Quadruple Exponential Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeQuadrupleExponentialMovingAverageFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeIchimokuSenkouSpanAFast(StockData data, ComputeContext context, int length = 26)
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
    public static ComputeBuffer ComputeIchimokuSenkouSpanBFast(StockData data, ComputeContext context, int length = 52)
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
    public static ComputeBuffer ComputeIchimokuChikouSpanFast(StockData data, ComputeContext context, int length = 26)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.IchimokuChikouSpan(close, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Williams Fractal Up using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeWilliamsFractalUpFast(StockData data, ComputeContext context, int length = 2)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var buffer = context.Rent(data.Count);
        TrendCore.WilliamsFractalUp(high, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Williams Fractal Down using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeWilliamsFractalDownFast(StockData data, ComputeContext context, int length = 2)
    {
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        TrendCore.WilliamsFractalDown(low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Alligator Jaw using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAlligatorJawFast(StockData data, ComputeContext context, int length = 13)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.AlligatorJaw(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Alligator Teeth using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAlligatorTeethFast(StockData data, ComputeContext context, int length = 8)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.AlligatorTeeth(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Alligator Lips using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAlligatorLipsFast(StockData data, ComputeContext context, int length = 5)
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
    public static ComputeBuffer ComputeEhlersLaguerreFilterFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeEhlersLaguerreRsiFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeEhlersZeroLagEmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersZeroLagExponentialMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Fractal Adaptive Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeEhlersFramaFast(StockData data, ComputeContext context, int length = 16)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersFractalAdaptiveMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Inverse Fisher Transform using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeEhlersInverseFisherTransformFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersInverseFisherTransform(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Cyber Cycle using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeEhlersCyberCycleFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeEhlersStochasticFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersStochastic(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Adaptive Laguerre Filter using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeEhlersAdaptiveLaguerreFilterFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeCoralTrendIndicatorFast(StockData data, ComputeContext context, int length = 21)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.CoralTrendIndicator(close, buffer.WritableSpan, length, 0.4);
        return buffer;
    }

    /// <summary>
    /// Computes Damped Sine Wave Weighted Filter using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeDampedSineWaveWeightedFilterFast(StockData data, ComputeContext context, int length = 50)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.DampedSineWaveWeightedFilter(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Fibonacci Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeFibonacciWeightedMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.FibonacciWeightedMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Generalized Double Exponential Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeGeneralizedDoubleEmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.GeneralizedDoubleExponentialMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Geometric Mean Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeGeometricMeanMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.GeometricMeanMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Harmonic Mean Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeHarmonicMeanMovingAverageFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeEhlers2PoleButterworthFilterV1Fast(StockData data, ComputeContext context, int length = 10)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.Ehlers2PoleButterworthFilterV1(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers 2-Pole Butterworth Filter V2 using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeEhlers2PoleButterworthFilterV2Fast(StockData data, ComputeContext context, int length = 15)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.Ehlers2PoleButterworthFilterV2(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers 3-Pole Butterworth Filter V1 using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeEhlers3PoleButterworthFilterV1Fast(StockData data, ComputeContext context, int length = 10)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.Ehlers3PoleButterworthFilterV1(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers 3-Pole Butterworth Filter V2 using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeEhlers3PoleButterworthFilterV2Fast(StockData data, ComputeContext context, int length = 15)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.Ehlers3PoleButterworthFilterV2(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers 2-Pole Super Smoother Filter V1 using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeEhlers2PoleSuperSmootherFilterV1Fast(StockData data, ComputeContext context, int length = 15)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.Ehlers2PoleSuperSmootherFilterV1(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers 2-Pole Super Smoother Filter V2 using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeEhlers2PoleSuperSmootherFilterV2Fast(StockData data, ComputeContext context, int length = 10)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.Ehlers2PoleSuperSmootherFilterV2(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers 3-Pole Super Smoother Filter using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeEhlers3PoleSuperSmootherFilterFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.Ehlers3PoleSuperSmootherFilter(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Decycler using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeEhlersDecyclerFast(StockData data, ComputeContext context, int length = 60)
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
    public static ComputeBuffer ComputeEhlersHammingMovingAverageFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersHammingMovingAverage(close, buffer.WritableSpan, length, 3);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Leading Indicator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeEhlersLeadingIndicatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersLeadingIndicator(close, buffer.WritableSpan, 0.25, 0.33);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers High Pass Filter V1 using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeEhlersHighPassFilterV1Fast(StockData data, ComputeContext context, int length = 125)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersHighPassFilterV1(close, buffer.WritableSpan, length, 1);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers High Pass Filter V2 using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeEhlersHighPassFilterV2Fast(StockData data, ComputeContext context, int length = 48)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersHighPassFilterV2(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Distance Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeDistanceWeightedMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.DistanceWeightedMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Filter using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeEhlersFilterFast(StockData data, ComputeContext context, int length = 15)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersFilter(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Finite Impulse Response Filter using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeEhlersFirFilterFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersFiniteImpulseResponseFilter(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ehlers Infinite Impulse Response Filter using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeEhlersIirFilterFast(StockData data, ComputeContext context, int length = 15)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EhlersInfiniteImpulseResponseFilter(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Simple Cycle oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeSimpleCycleFast(StockData data, ComputeContext context, int length = 50)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.SimpleCycle(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Simple Lines filter using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeSimpleLinesFast(StockData data, ComputeContext context, int length = 10, double mult = 10)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.SimpleLines(close, buffer.WritableSpan, length, mult);
        return buffer;
    }

    /// <summary>
    /// Computes Double Exponential Smoothing using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeDoubleExponentialSmoothingFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeDetrendedSyntheticPriceFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeBelkhayateTimingFast(StockData data, ComputeContext context, int length = 5)
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
    public static ComputeBuffer ComputeDemarkSetupIndicatorFast(StockData data, ComputeContext context, int length = 4)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DemarkSetupIndicator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Performance Index using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputePerformanceIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.PerformanceIndex(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Psychological Line using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputePsychologicalLineFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.PsychologicalLine(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Move Tracker using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeMoveTrackerFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeMultiLevelIndicatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeMarketDirectionIndicatorFast(StockData data, ComputeContext context, int length = 13)
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
    public static ComputeBuffer ComputeMorphedSineWaveFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeFullTypicalPriceFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeInternalBarStrengthIndicatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeZScoreFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ZScore(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Fast Z-Score using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeFastZScoreFast(StockData data, ComputeContext context, int length = 5)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.FastZScore(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Kurtosis Indicator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeKurtosisIndicatorFast(StockData data, ComputeContext context, int length = 14)
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
    public static ComputeBuffer ComputeDemarkRangeExpansionIndexFast(StockData data, ComputeContext context, int length = 5)
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
    public static ComputeBuffer ComputeDemarkPressureRatioV1Fast(StockData data, ComputeContext context, int length = 13)
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
    public static ComputeBuffer ComputeDemarkPressureRatioV2Fast(StockData data, ComputeContext context, int length = 10)
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
    public static ComputeBuffer ComputeDemarkReversalPointsFast(StockData data, ComputeContext context, int length1 = 9, int length2 = 4)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DemarkReversalPoints(close, buffer.WritableSpan, length1, length2);
        return buffer;
    }

    #endregion

    #endregion
}
