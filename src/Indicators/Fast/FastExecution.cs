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
            for (var slot = 0; slot < outputs.Length; slot++) series[indicator.Outputs[slot]] = outputs[slot];
            warmup = Math.Max(warmup, indicator.WarmupBars);
        }
        return series;
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
        return binders;
    }

    private static void BindAverages(Dictionary<Type, Func<IIndicator, FastKernel?>> binders)
    {
        Bind<Sma>(binders, sma => (c, o) => FastKernels.Sma(c.Close, o[0], sma.Length));
        Bind<FirstValueEma>(binders, ema => (c, o) => FastKernels.FirstValueEma(c.Close, o[0], ema.Period));
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
    }

    private static void BindRange(Dictionary<Type, Func<IIndicator, FastKernel?>> binders)
    {
        Bind<SeededAverageTrueRange>(binders, atr => (c, o) =>
            FastKernels.SeededAverageTrueRange(c.High, c.Low, c.Close, o[0], atr.Period, false));
        Bind<NormalizedSeededAverageTrueRange>(binders, atr => (c, o) =>
            FastKernels.SeededAverageTrueRange(c.High, c.Low, c.Close, o[0], atr.Period, true));
        Bind<SmoothedAccumulationOscillator>(binders, oscillator => (c, o) => FastKernels.SmoothedAccumulation(c, o, oscillator));
        Bind<WilderStrengthOscillator>(binders, strength => (c, o) => FastKernels.WilderStrength(c.Close, o[0], o[1],
            strength.Period, strength.Convention, strength.UnstablePeriods));
    }
}
