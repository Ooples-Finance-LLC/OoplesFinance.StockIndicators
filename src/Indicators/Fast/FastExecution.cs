namespace OoplesFinance.StockIndicators.Indicators.Fast;

/// <summary>
/// Executes indicators with ordinary double arithmetic over borrowed columns, for builds that asked for
/// <see cref="Builder.IndicatorNumerics.Fast"/>.
/// </summary>
/// <remarks>
/// Only indicators that read price directly and take no components have kernels here. A build containing any
/// other indicator is not supported, and the builder then keeps its exact route for the whole build rather
/// than mixing the two arithmetics in one run.
/// </remarks>
internal static class FastExecution
{
    private delegate void Kernel(FastColumns columns, double[][] outputs);

    internal static bool Supports(IReadOnlyList<IIndicator> indicators)
    {
        if (indicators.Count == 0) return false;
        for (var i = 0; i < indicators.Count; i++)
            if (Resolve(indicators[i]) is null) return false;
        return true;
    }

    /// <summary>Computes every configured output. The caller owns the returned arrays.</summary>
    internal static Dictionary<IIndicatorOutput, double[]> Execute(Bar[] source, IReadOnlyList<IIndicator> indicators,
        CancellationToken cancellation, out int warmup)
    {
        var columns = new FastColumns(source);
        var series = new Dictionary<IIndicatorOutput, double[]>();
        warmup = 0;
        for (var i = 0; i < indicators.Count; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var indicator = indicators[i];
            if (series.ContainsKey(indicator.Outputs[0])) continue;
            var kernel = Resolve(indicator) ?? throw new InvalidOperationException("Unqualified fast execution plan.");
            var outputs = new double[indicator.Outputs.Count][];
            for (var slot = 0; slot < outputs.Length; slot++) outputs[slot] = FastMath.Allocate(source.Length);
            kernel(columns, outputs);
            for (var slot = 0; slot < outputs.Length; slot++) series[indicator.Outputs[slot]] = outputs[slot];
            warmup = Math.Max(warmup, indicator.WarmupBars);
        }
        return series;
    }

    private static Kernel? Resolve(IIndicator indicator)
    {
        if (indicator.Source is not null || indicator.Components.Count != 0) return null;
        return indicator switch
        {
            Sma sma => (c, o) => FastKernels.Sma(c.Close, o[0], sma.Length),
            FirstValueEma ema => (c, o) => FastKernels.FirstValueEma(c.Close, o[0], ema.Period),
            SeededExponentialAverage seeded => (c, o) => FastKernels.SeededExponential(c.Close, o[0], seeded.Period,
                seeded.Order, seeded.SeedMode == ExponentialSeedMode.Shared),
            _ => null
        };
    }
}
