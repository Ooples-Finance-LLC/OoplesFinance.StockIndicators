namespace OoplesFinance.StockIndicators.Indicators.Fast;

/// <summary>A fast kernel: reads the columns it needs and writes every element of every output.</summary>
internal delegate void FastKernel(FastColumns columns, double[][] outputs);

/// <summary>
/// Executes indicators with ordinary double arithmetic over borrowed columns, for builds that asked for
/// <see cref="Builder.IndicatorNumerics.Fast"/>.
/// </summary>
/// <remarks>
/// Only indicators that read price directly and take no components have kernels here. A build containing any
/// other indicator is not supported, and the builder then keeps its exact route for the whole build rather
/// than mixing the two arithmetics in one run.
/// </remarks>
internal static partial class FastExecution
{
    // Keyed by the indicator's exact type. A binder returns null for a configuration its kernel does not
    // cover, which sends the build down the exact route.
    private static readonly Dictionary<Type, Func<IIndicator, FastKernel?>> Binders = CreateBinders();

    /// <summary>Every indicator type that has a fast kernel, for the parity tests to enumerate.</summary>
    internal static IReadOnlyCollection<Type> KernelTypes => Binders.Keys;

    internal static bool Supports(IReadOnlyList<IIndicator> indicators)
    {
        if (indicators.Count == 0) return false;
        for (var i = 0; i < indicators.Count; i++)
            if (Resolve(indicators[i]) is null) return false;
        return true;
    }

    /// <summary>Computes every configured output. The caller owns the returned arrays.</summary>
    internal static Dictionary<IIndicatorOutput, double[]> Execute(FastColumns columns, IReadOnlyList<IIndicator> indicators,
        CancellationToken cancellation, out int warmup)
    {
        var series = new Dictionary<IIndicatorOutput, double[]>();
        warmup = 0;
        for (var i = 0; i < indicators.Count; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var indicator = indicators[i];
            if (series.ContainsKey(indicator.Outputs[0])) continue;
            var kernel = Resolve(indicator) ?? throw new InvalidOperationException("Unqualified fast execution plan.");
            var outputs = new double[indicator.Outputs.Count][];
            for (var slot = 0; slot < outputs.Length; slot++) outputs[slot] = FastMath.Allocate(columns.Count);
            kernel(columns, outputs);
            RejectInvalidOutputs(indicator, outputs);
            for (var slot = 0; slot < outputs.Length; slot++) series[indicator.Outputs[slot]] = outputs[slot];
            warmup = Math.Max(warmup, indicator.WarmupBars);
        }
        return series;
    }

    // The exact routes reject an infinite or unexpected NaN output as they publish it. Here one vectorized
    // pass per output decides the usual all-finite case, and only a failure walks the bars to name the first.
    private static void RejectInvalidOutputs(IIndicator indicator, double[][] outputs)
    {
        var finite = true;
        for (var slot = 0; slot < outputs.Length && finite; slot++) finite = FastMath.AllFinite(outputs[slot]);
        if (finite) return;
        for (var index = 0; index < outputs[0].Length; index++)
            for (var slot = 0; slot < outputs.Length; slot++)
                Validation.IndicatorOutputPolicy.Validate(indicator, slot, index, outputs[slot][index]);
    }

    private static FastKernel? Resolve(IIndicator indicator) =>
        indicator.Source is null && indicator.Components.Count == 0
        && Binders.TryGetValue(indicator.GetType(), out var bind) ? bind(indicator) : null;

    private static void Bind<T>(Dictionary<Type, Func<IIndicator, FastKernel?>> binders, Func<T, FastKernel?> bind)
        where T : class, IIndicator => binders.Add(typeof(T), indicator => bind((T)indicator));

    private static Dictionary<Type, Func<IIndicator, FastKernel?>> CreateBinders()
    {
        var binders = new Dictionary<Type, Func<IIndicator, FastKernel?>>();
        BindAverages(binders);
        BindClassic(binders);
        BindRange(binders);
        BindTransforms(binders);
        return binders;
    }

    private static void BindAverages(Dictionary<Type, Func<IIndicator, FastKernel?>> binders)
    {
        Bind<Sma>(binders, sma => (c, o) => FastKernels.Sma(c.Close, o[0], sma.Length));
        Bind<FirstValueEma>(binders, ema => (c, o) => FastKernels.FirstValueEma(c.Close, o[0], ema.Period));
        Bind<Ema>(binders, ema => (c, o) => FastKernels.RunningSeedEma(c.Close, o[0], ema.Length));
        Bind<Wma>(binders, wma => (c, o) => FastKernels.FullDivisorWma(c.Close, o[0], wma.Length));
        Bind<MedianPrice>(binders, _ => (c, o) => FastKernels.PricePoint(c, o[0], 0, false));
        Bind<TypicalPrice>(binders, _ => (c, o) => FastKernels.PricePoint(c, o[0], 1, false));
        Bind<WeightedClose>(binders, _ => (c, o) => FastKernels.PricePoint(c, o[0], 2, false));
        Bind<FullTypicalPrice>(binders, _ => (c, o) => FastKernels.PricePoint(c, o[0], 1, true));
        Bind<TrueRange>(binders, _ => (c, o) =>
        {
            c.RequireHighLowClose();
            FastKernels.TrueRangeSeries(c.High, c.Low, c.Close, o[0]);
        });
        Bind<HighestHigh>(binders, t => (c, o) => FastKernels.RollingExtreme(c.High, o[0], Math.Max(1, t.Length), true));
        Bind<LowestLow>(binders, t => (c, o) => FastKernels.RollingExtreme(c.Low, o[0], Math.Max(1, t.Length), false));
        Bind<WilliamsR>(binders, t => (c, o) => FastKernels.WilliamsPercent(c, o[0], t.Length));
        Bind<SeededExponentialAverage>(binders, seeded => (c, o) => FastKernels.SeededExponential(c.Close, o[0],
            seeded.Period, seeded.Order, seeded.SeedMode == ExponentialSeedMode.Shared));
    }

    private static void BindClassic(Dictionary<Type, Func<IIndicator, FastKernel?>> binders)
    {
        Bind<ClassicMovingAverage>(binders, average => !FastKernels.SupportsClassic(average.Method) ? null : (c, o) =>
        {
            FastKernels.ClassicAverage(c.Close, o[0], average, average.First);
            FastKernels.DefinedFrom(o[1], average.First);
        });
        Bind<ClassicPriceOscillator>(binders, oscillator => !FastKernels.SupportsClassic(oscillator.Method) ? null
            : (c, o) => FastKernels.PriceOscillator(c.Close, o[0], o[1], oscillator));
        Bind<ClassicMacd>(binders, macd => !FastKernels.SupportsClassic(macd.FastAverage.Method)
            || !FastKernels.SupportsClassic(macd.SlowAverage.Method) || !FastKernels.SupportsClassic(macd.SignalAverage.Method)
            ? null : (c, o) => FastKernels.ClassicMacd(c.Close, o, macd));
        Bind<AlignedEmaMacd>(binders, macd => (c, o) => FastKernels.AlignedMacd(c.Close, o, macd));
        Bind<TillsonAverage>(binders, tillson => (c, o) => FastKernels.Tillson(c.Close, o[0], o[1], tillson));
    }

    private static void BindRange(Dictionary<Type, Func<IIndicator, FastKernel?>> binders)
    {
        Bind<SeededAverageTrueRange>(binders, atr => (c, o) =>
        {
            c.RequireHighLowClose();
            FastKernels.SeededAverageTrueRange(c.High, c.Low, c.Close, o[0], atr.Period, false);
        });
        Bind<NormalizedSeededAverageTrueRange>(binders, atr => (c, o) =>
        {
            c.RequireHighLowClose();
            FastKernels.SeededAverageTrueRange(c.High, c.Low, c.Close, o[0], atr.Period, true);
        });
        Bind<SmoothedAccumulationOscillator>(binders, oscillator => (c, o) => FastKernels.SmoothedAccumulation(c, o, oscillator));
        Bind<WilderStrengthOscillator>(binders, strength => (c, o) => FastKernels.WilderStrength(c.Close, o[0], o[1],
            strength.Period, strength.Convention, strength.UnstablePeriods));
    }

    private static void BindTransforms(Dictionary<Type, Func<IIndicator, FastKernel?>> binders)
    {
        Bind<PriceRoundingTransform>(binders, t => (c, o) => FastKernels.Rounding(c.Close, o[0], o[1], t.Operation));
        Bind<PriceCircularTransform>(binders, t => (c, o) => FastKernels.Circular(c.Close, o[0], o[1], t.Operation));
        Bind<PriceTranscendentalTransform>(binders, t => (c, o) => FastKernels.Transcendental(c.Close, o[0], o[1], t.Operation));
        Bind<TripleExponentialRate>(binders, t => (c, o) => FastKernels.TripleRate(c.Close, o, t));
        Bind<PriorSeededDirectionalMeasure>(binders, t => (c, o) => FastKernels.Directional(c, o[0], o[1], t));
        Bind<WindowDispersion>(binders, t => (c, o) => FastKernels.Dispersion(c.Close, o[0], t));
        Bind<ArnaudLegouxWindow>(binders, t => (c, o) => FastKernels.ArnaudLegoux(c.Close, o[0], o[1], t));
        Bind<WindowLogVolatility>(binders, t => t.Period < 2 ? null : (c, o) => FastKernels.LogVolatility(c.Close, o[0], o[1], t));
        Bind<WindowAtrBands>(binders, t => (c, o) => FastKernels.AtrBands(c, o, t));
        Bind<RelativeVolatilityStrength>(binders, t => (c, o) => FastKernels.RelativeVolatility(c.Close, o[0], t.Period));
        Bind<HullWindow>(binders, t => (c, o) => FastKernels.Hull(c.Close, o[0], o[1], t));
        Bind<FixedPeriodWma>(binders, t => (c, o) => FastKernels.ExpandingWeightedWindow(c.Close, o[0], t.Period));
        Bind<RateOfChangeRmsBands>(binders, t => (c, o) => FastKernels.RateOfChangeBands(c.Close, o, t.Period, t.EmaPeriod, t.DeviationPeriod));
        Bind<HoltWinterForecast>(binders, t => (c, o) => FastKernels.HoltWinter(c.Close, o[0], t));
        Bind<WindowCorrelation>(binders, t => (c, o) => FastKernels.Correlation(c, o[0], o[1], t));
        Bind<SingleStochasticCycle>(binders, t => (c, o) => FastKernels.StochasticCycle(c.Close, o[0], o[1], t.CyclePeriod, t.FastPeriod, t.SlowPeriod));
        Bind<WindowSampleShape>(binders, t => (c, o) => FastKernels.SampleShape(c.Close, o[0], o[1], t.Period, t.Kurtosis));
        Bind<WindowAverageEnvelope>(binders, t => (c, o) => FastKernels.AverageEnvelope(c.Close, o, t));
        Bind<GatorWithDetails>(binders, t => (c, o) => FastKernels.GatorDetails(c, o, t.Alligator));
        Bind<StandardDeviationWithDetails>(binders, t => (c, o) => FastKernels.StandardDeviationDetails(c.Close, o, t.Period, t.SmoothingPeriod));
        Bind<WindowFisherTransform>(binders, t => (c, o) => FastKernels.FisherTransform(c, o, t.Period, t.UseMidpoint));
        Bind<MoneyFlowWithDetails>(binders, t => (c, o) => FastKernels.MoneyFlowDetails(c, o, t.Period));
        Bind<WindowChandelierExit>(binders, t => (c, o) => FastKernels.ChandelierExit(c, o, t.Period, t.Multiplier, t.Selection, t.ZeroFloorHigh));
        Bind<WindowVortex>(binders, t => (c, o) => FastKernels.Vortex(c, o, t.Period));
        Bind<WindowChoppinessIndex>(binders, t => (c, o) => FastKernels.Choppiness(c, o[0], o[1], t.Period));
        Bind<AlligatorWithDetails>(binders, t => (c, o) => FastKernels.AlligatorDetails(c, o, t));
        Bind<ElderRayWithDetails>(binders, t => (c, o) => FastKernels.ElderRayDetails(c, o, t.Period));
        Bind<AccumulationDistributionWithAverage>(binders, t => (c, o) => FastKernels.AccumulationDetails(c, o, t.AveragePeriod));
        Bind<AwesomeWithDetails>(binders, t => (c, o) => FastKernels.AwesomeDetails(c, o, t.FastPeriod, t.SlowPeriod));
        Bind<AverageTrueRangeWithDetails>(binders, t => (c, o) => FastKernels.AverageTrueRangeDetails(c, o, t.Period));
        Bind<RateOfChangeWithAverage>(binders, t => (c, o) => FastKernels.RateOfChangeAverage(c.Close, o, t.Period, t.AveragePeriod));
        Bind<BalanceOfPowerWithValidity>(binders, t => (c, o) => FastKernels.BalanceOfPowerMean(c, o[0], o[1], t.Period));
        Bind<BreakawayCandle>(binders, t => (c, o) => FastKernels.ExtendedReversalCandle(c, o[0], t.Period, ExtendedReversalKind.Breakaway));
        Bind<ConcealingBabySwallowCandle>(binders, t => (c, o) => FastKernels.ExtendedReversalCandle(c, o[0], t.Period, ExtendedReversalKind.ConcealingBabySwallow));
        Bind<LadderBottomCandle>(binders, t => (c, o) => FastKernels.ExtendedReversalCandle(c, o[0], t.Period, ExtendedReversalKind.LadderBottom));
        Bind<MorningStarCandle>(binders, t => (c, o) => FastKernels.StarReversalCandle(c, o[0], t.LongPeriod, t.ShortPeriod, 0, t.Penetration, true, false));
        Bind<EveningStarCandle>(binders, t => (c, o) => FastKernels.StarReversalCandle(c, o[0], t.LongPeriod, t.ShortPeriod, 0, t.Penetration, false, false));
        Bind<MorningDojiStarCandle>(binders, t => (c, o) => FastKernels.StarReversalCandle(c, o[0], t.LongPeriod, t.ShortPeriod, t.DojiPeriod, t.Penetration, true, false));
        Bind<EveningDojiStarCandle>(binders, t => (c, o) => FastKernels.StarReversalCandle(c, o[0], t.LongPeriod, t.ShortPeriod, t.DojiPeriod, t.Penetration, false, false));
        Bind<AbandonedBabyCandle>(binders, t => (c, o) => FastKernels.StarReversalCandle(c, o[0], t.LongPeriod, t.ShortPeriod, t.DojiPeriod, t.Penetration, null, true));
        Bind<KickingCandle>(binders, t => (c, o) => FastKernels.KickingCandle(c, o[0], t.BodyPeriod, t.ShadowPeriod, false));
        Bind<KickingByLengthCandle>(binders, t => (c, o) => FastKernels.KickingCandle(c, o[0], t.BodyPeriod, t.ShadowPeriod, true));
        Bind<AdvanceBlockCandle>(binders, t => (c, o) => FastKernels.ExhaustionCandle(c, o[0], ExhaustionCandleKind.AdvanceBlock, t.LongPeriod, 0, t.ShadowPeriod, t.NearPeriod, t.FarPeriod));
        Bind<StalledCandle>(binders, t => (c, o) => FastKernels.ExhaustionCandle(c, o[0], ExhaustionCandleKind.Stalled, t.LongPeriod, t.ShortPeriod, t.ShadowPeriod, t.NearPeriod, 0));
        Bind<ThreeStarsInSouthCandle>(binders, t => (c, o) => FastKernels.ExhaustionCandle(c, o[0], ExhaustionCandleKind.ThreeStarsInSouth, t.LongPeriod, t.ShortPeriod, t.ShadowPeriod, 0, 0));
        Bind<ThreeBlackCrowsCandle>(binders, t => (c, o) => FastKernels.CrowSoldierCandle(c, o[0], t.ShadowPeriod, 0, 0, 0, CrowSoldierKind.Black));
        Bind<IdenticalThreeCrowsCandle>(binders, t => (c, o) => FastKernels.CrowSoldierCandle(c, o[0], t.ShadowPeriod, t.EqualPeriod, 0, 0, CrowSoldierKind.Identical));
        Bind<ThreeWhiteSoldiersCandle>(binders, t => (c, o) => FastKernels.CrowSoldierCandle(c, o[0], t.ShadowPeriod, t.NearPeriod, t.FarPeriod, t.BodyPeriod, CrowSoldierKind.White));
        Bind<StrictHaramiCandle>(binders, t => (c, o) => FastKernels.StrictHarami(c, o[0], 0, 1, t.ContainedShadows));
        Bind<BullishHaramiPattern>(binders, t => (c, o) => FastKernels.StrictHarami(c, o[0], 1, t.Period, t.ContainedShadows));
        Bind<BearishHaramiPattern>(binders, t => (c, o) => FastKernels.StrictHarami(c, o[0], -1, t.Period, t.ContainedShadows));
        Bind<CounterattackCandle>(binders, t => (c, o) => FastKernels.MatchedLinesCandle(c, o[0], t.BodyPeriod, 0, t.EqualPeriod, MatchedLinesKind.Counterattack));
        Bind<SeparatingLinesCandle>(binders, t => (c, o) => FastKernels.MatchedLinesCandle(c, o[0], t.BodyPeriod, t.ShadowPeriod, t.EqualPeriod, MatchedLinesKind.SeparatingLines));
        Bind<GapSideBySideWhiteLinesCandle>(binders, t => (c, o) => FastKernels.MatchedLinesCandle(c, o[0], 0, t.NearPeriod, t.EqualPeriod, MatchedLinesKind.GapSideBySideWhiteLines));
        Bind<ThreeInsideCandle>(binders, t => (c, o) => FastKernels.TripleBodyCandle(c, o[0], t.LongPeriod, t.ShortPeriod, TripleBodyCandleKind.ThreeInside));
        Bind<TwoCrowsCandle>(binders, t => (c, o) => FastKernels.TripleBodyCandle(c, o[0], t.LongPeriod, t.LongPeriod, TripleBodyCandleKind.TwoCrows));
        Bind<UpsideGapTwoCrowsCandle>(binders, t => (c, o) => FastKernels.TripleBodyCandle(c, o[0], t.LongPeriod, t.ShortPeriod, TripleBodyCandleKind.UpsideGapTwoCrows));
        Bind<UniqueThreeRiverCandle>(binders, t => (c, o) => FastKernels.TripleBodyCandle(c, o[0], t.LongPeriod, t.ShortPeriod, TripleBodyCandleKind.UniqueThreeRiver));
        Bind<AverageRangeDoji>(binders, t => (c, o) => FastKernels.AverageRangeDojiCandle(c, o[0], t.Period, AverageRangeDojiKind.Body));
        Bind<AverageRangeDragonflyDoji>(binders, t => (c, o) => FastKernels.AverageRangeDojiCandle(c, o[0], t.Period, AverageRangeDojiKind.Dragonfly));
        Bind<AverageRangeGravestoneDoji>(binders, t => (c, o) => FastKernels.AverageRangeDojiCandle(c, o[0], t.Period, AverageRangeDojiKind.Gravestone));
        Bind<AverageRangeLongLeggedDoji>(binders, t => (c, o) => FastKernels.AverageRangeDojiCandle(c, o[0], t.Period, AverageRangeDojiKind.LongLegged));
        Bind<TakuriLineCandle>(binders, t => (c, o) => FastKernels.AverageRangeDojiCandle(c, o[0], t.Period, AverageRangeDojiKind.TakuriLine));
        Bind<OnNeckCandle>(binders, t => (c, o) => FastKernels.NeckCandle(c, o[0], t.BodyPeriod, t.EqualPeriod, NeckCandleKind.OnNeck));
        Bind<InNeckCandle>(binders, t => (c, o) => FastKernels.NeckCandle(c, o[0], t.BodyPeriod, t.EqualPeriod, NeckCandleKind.InNeck));
        Bind<ThrustingCandle>(binders, t => (c, o) => FastKernels.NeckCandle(c, o[0], t.BodyPeriod, t.EqualPeriod, NeckCandleKind.Thrusting));
        Bind<PiercingLineCandle>(binders, t => (c, o) => FastKernels.PenetrationCandle(c, o[0], t.Period, true, 0.5));
        Bind<DarkCloudCoverCandle>(binders, t => (c, o) => FastKernels.PenetrationCandle(c, o[0], t.Period, false, t.Penetration));
        Bind<HaramiCandle>(binders, t => (c, o) => FastKernels.ContainmentCandle(c, o[0], t.Period, ContainmentCandleKind.Harami));
        Bind<HaramiCrossCandle>(binders, t => (c, o) => FastKernels.ContainmentCandle(c, o[0], t.Period, ContainmentCandleKind.HaramiCross));
        Bind<HomingPigeonCandle>(binders, t => (c, o) => FastKernels.ContainmentCandle(c, o[0], t.Period, ContainmentCandleKind.HomingPigeon));
        Bind<DojiStarCandle>(binders, t => (c, o) => FastKernels.ContainmentCandle(c, o[0], t.Period, ContainmentCandleKind.DojiStar));
        Bind<TristarCandle>(binders, t => (c, o) => FastKernels.GapContinuationCandle(c, o[0], t.Period, GapContinuationKind.Tristar));
        Bind<TasukiGapCandle>(binders, t => (c, o) => FastKernels.GapContinuationCandle(c, o[0], t.Period, GapContinuationKind.TasukiGap));
        Bind<UpDownSideGapThreeMethodsCandle>(binders, t => (c, o) => FastKernels.GapContinuationCandle(c, o[0], 0, GapContinuationKind.UpDownSideGapThreeMethods));
        Bind<WindowBetaStatistics>(binders, t => (c, o) => FastKernels.BetaStatistics(c, o, t));
        Bind<WindowReturnBeta>(binders, t => (c, o) => FastKernels.ReturnBeta(c, o[0], o[1], t));
        Bind<EngulfingPattern>(binders, t => (c, o) => FastKernels.Engulfing(c, o[0]));
        Bind<MatchingLowCandle>(binders, t => (c, o) => FastKernels.SequenceCandle(c, o[0], t.Period, SequenceCandleKind.MatchingLow));
        Bind<StickSandwichCandle>(binders, t => (c, o) => FastKernels.SequenceCandle(c, o[0], t.Period, SequenceCandleKind.StickSandwich));
        Bind<ThreeLineStrikeCandle>(binders, t => (c, o) => FastKernels.SequenceCandle(c, o[0], t.Period, SequenceCandleKind.ThreeLineStrike));
        Bind<ThreeOutsideCandle>(binders, t => (c, o) => FastKernels.SequenceCandle(c, o[0], 0, SequenceCandleKind.ThreeOutside));
        Bind<HikkakeCandle>(binders, t => (c, o) => FastKernels.Hikkake(c, o[0], 0));
        Bind<ModifiedHikkakeCandle>(binders, t => (c, o) => FastKernels.Hikkake(c, o[0], t.Period));
        Bind<HammerCandle>(binders, t => (c, o) => FastKernels.ContextReversalCandle(c, o[0], t.Period, t.NearPeriod, ContextReversalKind.Hammer));
        Bind<HangingManCandle>(binders, t => (c, o) => FastKernels.ContextReversalCandle(c, o[0], t.Period, t.NearPeriod, ContextReversalKind.HangingMan));
        Bind<InvertedHammerCandle>(binders, t => (c, o) => FastKernels.ContextReversalCandle(c, o[0], t.Period, 0, ContextReversalKind.InvertedHammer));
        Bind<ShootingStarCandle>(binders, t => (c, o) => FastKernels.ContextReversalCandle(c, o[0], t.Period, 0, ContextReversalKind.ShootingStar));
        Bind<MarubozuCandle>(binders, t => (c, o) => FastKernels.BodyShadowCandle(c, o[0], t.Period, BodyShadowCandleKind.Marubozu));
        Bind<ClosingMarubozuCandle>(binders, t => (c, o) => FastKernels.BodyShadowCandle(c, o[0], t.Period, BodyShadowCandleKind.ClosingMarubozu));
        Bind<SpinningTopCandle>(binders, t => (c, o) => FastKernels.BodyShadowCandle(c, o[0], t.Period, BodyShadowCandleKind.SpinningTop));
        Bind<HighWaveCandle>(binders, t => (c, o) => FastKernels.BodyShadowCandle(c, o[0], t.Period, BodyShadowCandleKind.HighWave));
        Bind<LongLineCandle>(binders, t => (c, o) => FastKernels.BodyShadowCandle(c, o[0], t.Period, BodyShadowCandleKind.LongLine));
        Bind<ShortLineCandle>(binders, t => (c, o) => FastKernels.BodyShadowCandle(c, o[0], t.Period, BodyShadowCandleKind.ShortLine));
        Bind<BeltHoldCandle>(binders, t => (c, o) => FastKernels.BodyShadowCandle(c, o[0], t.Period, BodyShadowCandleKind.BeltHold));
        Bind<DelayedHilbertPhase>(binders, t => (c, o) => FastKernels.HilbertSignal(c.Close, o, HilbertCycleSignal.Phase, 63L + t.Suppression));
        Bind<DelayedHilbertSine>(binders, t => (c, o) => FastKernels.HilbertSignal(c.Close, o, HilbertCycleSignal.Sine, 63L + t.Suppression));
        Bind<DelayedHilbertTrendMode>(binders, t => (c, o) => FastKernels.HilbertSignal(c.Close, o, HilbertCycleSignal.Trend, 63L + t.Suppression));
        Bind<SeededHilbertTrendline>(binders, t => (c, o) => FastKernels.SeededTrendline(c, o, t.UseMidpoint));
        Bind<DelayedHilbertTrendline>(binders, t => (c, o) => FastKernels.DelayedTrendline(c.Close, o[0], o[1], 63L + t.Suppression));
        Bind<HilbertCyclePeriod>(binders, t => (c, o) => FastKernels.HilbertCycle(c.Close, o, true, 32L + t.Suppression));
        Bind<HilbertPhasor>(binders, t => (c, o) => FastKernels.HilbertCycle(c.Close, o, false, 32L + t.Suppression));
        Bind<SeededPhaseAdaptiveAverage>(binders, t => (c, o) => FastKernels.PhaseAdaptive(c, o, t.FastLimit, t.SlowLimit, false, t.RetainOlderZeroPhase, t.UseMidpoint, t.PublishStartup ? 0 : 5));
        Bind<DelayedPhaseAdaptiveAverage>(binders, t => (c, o) => FastKernels.PhaseAdaptive(c, o, t.FastLimit, t.SlowLimit, true, false, false, 32L + t.Suppression));
        Bind<RegressionWithFinalLine>(binders, t => (c, o) => FastKernels.RegressionFinalLine(c.Close, o, t.Period));
        Bind<WindowRegressionStatistics>(binders, t => (c, o) => FastKernels.RegressionStatistics(c.Close, o, t.Period));
        Bind<WindowStochasticKdj>(binders, t => (c, o) => FastKernels.StochasticKdj(c, o, t));
        Bind<RangeFractalAverage>(binders, t => (c, o) => FastKernels.FractalAdaptive(c.Close, o[0], t.Period));
        Bind<PriceRelativeStrength>(binders, t => (c, o) => FastKernels.PriceRelative(c, o, t));
        Bind<FilteredDeviationAverage>(binders, t => (c, o) => FastKernels.DeviationScaled(c.Close, o[0], t.Period, t.ScaleFactor));
        Bind<QuarticDynamicAverage>(binders, t => (c, o) => FastKernels.QuarticDynamic(c.Close, o[0], o[1], t));
        Bind<SumSeededDirectionalIndex>(binders, t => (c, o) => FastKernels.SumSeededDirectional(c, o, t.Period));
        Bind<WindowUltimateOscillator>(binders, t => (c, o) => FastKernels.UltimateOscillator(c, o[0], o[1], t));
        Bind<MassNormalizedQuadrupleEma>(binders, t => (c, o) => FastKernels.QuadrupleEma(c.Close, o[0], t));
        Bind<ZeroSeedLaguerreFilter>(binders, t => (c, o) => FastKernels.LaguerreFilter(c.Close, o[0], t.Gamma));
        Bind<WindowUlcerIndex>(binders, t => (c, o) => FastKernels.UlcerIndex(c.Close, o[0], o[1], t.Period));
        Bind<ClassicStochasticRsi>(binders, t => !FastKernels.SupportsClassic(t.Method) ? null : (c, o) => FastKernels.StochasticStrength(c.Close, o, t));
        Bind<KlingerVolumeWithSignal>(binders, t => (c, o) => FastKernels.KlingerVolume(c, o, t));
        Bind<ExpandingRegularizedEma>(binders, t => (c, o) => FastKernels.RegularizedEma(c.Close, o[0], t.Period, t.Lambda));
        Bind<ExpandingZeroLagEma>(binders, t => (c, o) => FastKernels.ZeroLagEma(c.Close, o[0], t.Period));
        Bind<SeededOnBalanceVolume>(binders, t => (c, o) => FastKernels.OnBalanceVolume(c, o, t));
        Bind<Adl>(binders, t => (c, o) => FastKernels.AccumulationLine(c, o[0], o[1], t.Length));
        Bind<BalanceOfPower>(binders, t => t.MaType is not null ? null : (c, o) => FastKernels.BalanceOfPower(c, o[0], o[1], t.Length));
        Bind<WindowDeviationBands>(binders, t => (c, o) => FastKernels.DeviationBands(c.Close, o, t));
        Bind<SineWeightedAverage>(binders, t => (c, o) => FastKernels.LagWeightedWindow(c.Close, o[0], FastKernels.SineWeights(t.Period)));
        Bind<GaussianWeightedAverage>(binders, t => (c, o) => FastKernels.LagWeightedWindow(c.Close, o[0], FastKernels.GaussianWeights(t.Period, t.Sigma)));
        Bind<TriangularWindowAverage>(binders, t => (c, o) => FastKernels.TriangularWindow(c.Close, o[0], t.Period));
        Bind<WindowMeanAbsoluteDeviation>(binders, t => (c, o) => FastKernels.MeanAbsoluteDeviation(c.Close, o[0], t.Period));
        Bind<MovingAverageDiagnostics>(binders, t => (c, o) => FastKernels.MeanDiagnostics(c.Close, o, t.Period));
        Bind<LaggedPriceChange>(binders, t => (c, o) => FastKernels.LaggedChange(c.Close, o[0], t));
        Bind<RangeAccelerationBands>(binders, t => (c, o) => FastKernels.AccelerationBands(c, o, t));
        Bind<SeededTrueStrength>(binders, t => (c, o) => FastKernels.TrueStrength(c.Close, o, t));
        Bind<SeededForceIndex>(binders, t => (c, o) => FastKernels.ForceIndex(c, o[0], o[1], t.Period));
        Bind<SeededPriceMomentum>(binders, t => (c, o) => FastKernels.PriceMomentum(c.Close, o, t));
        Bind<ClassicDeviationBands>(binders, t => !FastKernels.SupportsClassic(t.Method) ? null : (c, o) => FastKernels.ClassicBands(c.Close, o, t));
        Bind<SeededAtrTrailingStop>(binders, t => (c, o) => FastKernels.AtrTrailingStop(c, o, t));
        Bind<RollingPriceSum>(binders, t => (c, o) => FastKernels.RollingSum(c.Close, o[0], t.Period));
        Bind<ReverseWilderAverage>(binders, t => (c, o) => FastKernels.ReverseWilder(c.Close, o[0], t.Period));
        Bind<EmaDifferenceSignal>(binders, t => (c, o) => FastKernels.EmaDifference(c, o, t));
        Bind<WindowCommodityChannelIndex>(binders, t => (c, o) => FastKernels.CommodityChannel(c, o[0], o[1], t));
        Bind<WindowMoneyFlowIndex>(binders, t => (c, o) => FastKernels.MoneyFlow(c, o[0], o[1], t));
        Bind<WilderMovingAverage>(binders, t => (c, o) => FastKernels.WilderAverage(c.Close, o[0], t.Period, t.SeedWithAverage));
        Bind<CompensatedExponentialAverage>(binders, t => (c, o) => FastKernels.CompensatedExponential(c.Close, o[0], t.Period, t.Order));
        Bind<CandleArithmetic>(binders, t => (c, o) => FastKernels.CandleArithmetic(c, o[0], o[1], t));
        Bind<WindowAroon>(binders, t => (c, o) => FastKernels.Aroon(c, o, t));
        Bind<WindowExtremeIndex>(binders, t => (c, o) => FastKernels.RollingExtremeIndex(c.Close, o[0], t.Period, t.Maximum, true));
        Bind<WindowRangeMidpoint>(binders, t => (c, o) =>
        {
            if (t.CandleRanges) FastKernels.RangeMidpoint(c.Low, c.High, o[0], t.Period);
            else FastKernels.RangeMidpoint(c.Close, c.Close, o[0], t.Period);
        });
        Bind<SeededAdaptiveAverage>(binders, t => (c, o) => FastKernels.AdaptiveAverage(c.Close, o, t));
        Bind<WindowLinearRegression>(binders, t => (c, o) => FastKernels.LinearRegression(c.Close, o[0], t));
        Bind<ClassicStochastic>(binders, t => !FastKernels.SupportsClassic(t.KMethod) || !FastKernels.SupportsClassic(t.DMethod)
            ? null : (c, o) => FastKernels.Stochastic(c, o, t));
    }
}
