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
