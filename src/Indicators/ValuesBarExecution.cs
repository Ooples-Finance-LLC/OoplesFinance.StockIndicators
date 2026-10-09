#if !NETFRAMEWORK
using System.Runtime.ExceptionServices;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

// Internal facade plan: no public field masks, array counts or kernel selection.
// Only sealed, independently qualified states enter this route. Other graphs use
// the existing builder automatically. Input bars are consumed once into local values.
internal static class ValuesBarExecution
{
    internal static bool Supports(IReadOnlyList<IIndicator> indicators) => indicators.All(i =>
        i.Source is null && i.Components.Count == 0 && i is Sma or JurikAdaptive or ScaledTrueRange
            or RollingPivotLevels or RetrospectiveFractals or RickshawManCandle or BullishShortBodyCandle
            or PriceCircularTransform { Operation: PriceCircularOperation.ArcSine });

    internal static IIndicatorRun Execute(Bar[] source, IReadOnlyList<IIndicator> indicators, CancellationToken cancellation)
    {
        var nodes = new List<Node>();
        try
        {
            foreach (var indicator in indicators.Distinct(IndicatorIdentity.Comparer)) nodes.Add(new Node(indicator, source.Length));
            // The guarded batch SMA contract needs replayable closes, not OHLCV
            // history. Multiple SMA periods share this one temporary input column.
            bool singleSma = nodes.Count == 1 && nodes[0].Indicator is Sma;
            double[]? close = singleSma ? nodes[0].Values[0]
                : nodes.Any(n => n.Indicator is Sma) ? new double[source.Length] : null;
            var active = nodes.Where(n => n.Indicator is not Sma).ToArray();
            var summary = new Core.SmaCpuKernel.GridSummary();
            var positiveRange = new Core.SmaCpuKernel.PositiveRangeSummary();
            Bar latest = default;
            if (singleSma)
                latest = FillSma(source, close!, ref summary, ref positiveRange, cancellation);
            else if (nodes.Count == 1 && nodes[0].Indicator is PriceCircularTransform)
                latest = FillAsin(source, nodes[0].Values, cancellation);
            else if (nodes.Count == 1 && nodes[0].HasScalarState)
                latest = nodes[0].FillScalar(source, cancellation);
            else for (int i = 0; i < source.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                var bar = source[i];
                latest = bar;
                if (!double.IsFinite(bar.Open) || !double.IsFinite(bar.High) || !double.IsFinite(bar.Low)
                    || !double.IsFinite(bar.Close) || !double.IsFinite(bar.Volume))
                    IndicatorInputDomain.Finite.Validate(in bar);
                if (close is not null) { close[i] = bar.Close; summary.Include(bar.Close); }
                foreach (var node in active) node.Append(in bar, i);
            }
            var published = new Dictionary<IIndicatorOutput, double[]>();
            foreach (var node in nodes)
            {
                cancellation.ThrowIfCancellationRequested();
                node.Failure?.Throw();
                if (node.Indicator is Sma sma)
                    ComputeSma(close!, node.Values[0], Math.Max(1, sma.Length), summary, cancellation, singleSma, positiveRange.Certifies(Math.Max(1, sma.Length)));
                for (int slot = 0; slot < node.Values.Length; slot++)
                {
                    // Every qualified node has finite startup/output policy. Asin
                    // produces finite values/flags by construction; other outputs
                    // need only take the general diagnostic path on a failure.
                    if (node.Indicator is not PriceCircularTransform)
                    for (int i = 0; i < source.Length; i++)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if (!double.IsFinite(node.Values[slot][i]))
                            IndicatorOutputPolicy.Validate(node.Indicator, slot, i, node.Values[slot][i]);
                    }
                    published.Add(node.Indicator.Outputs[slot], node.Values[slot]);
                }
            }
            int warmup = 0;
            foreach (var node in nodes) warmup = Math.Max(warmup, node.Indicator.WarmupBars);
            IBarSnapshot? snapshot = source.Length == 0 ? null
                : new BarSnapshot(latest, source.Length - 1, published,
                    source.Length >= warmup && published.Values.All(v => !double.IsNaN(v[source.Length - 1])));
            return new LatestOnlyIndicatorRun(published, source.Length, snapshot);
        }
        finally { foreach (var node in nodes) node.Dispose(); }
    }

    private static Bar FillSma(Bar[] source, double[] close, ref Core.SmaCpuKernel.GridSummary summary,
        ref Core.SmaCpuKernel.PositiveRangeSummary positiveRange, CancellationToken cancellation)
    {
        Bar latest = default;
        for (int i = 0; i < source.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var bar = source[i];
            latest = bar;
            if (!double.IsFinite(bar.Open) || !double.IsFinite(bar.High) || !double.IsFinite(bar.Low)
                || !double.IsFinite(bar.Close) || !double.IsFinite(bar.Volume))
                IndicatorInputDomain.Finite.Validate(in bar);
            close[i] = bar.Close;
            summary.Include(bar.Close);
            positiveRange.Include(bar.Close);
        }
        return latest;
    }

    private static Bar FillAsin(Bar[] source, double[][] output, CancellationToken cancellation)
    {
        var values = output[0];
        var flags = output[1];
        Bar latest = default;
        for (int i = 0; i < source.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var bar = source[i];
            latest = bar;
            if (!double.IsFinite(bar.Open) || !double.IsFinite(bar.High) || !double.IsFinite(bar.Low)
                || !double.IsFinite(bar.Close) || !double.IsFinite(bar.Volume))
                IndicatorInputDomain.Finite.Validate(in bar);
            bool defined = bar.Close is >= -1 and <= 1;
            values[i] = defined ? Math.Asin(bar.Close) : 0;
            flags[i] = defined ? 1 : 0;
        }
        return latest;
    }

    private static void ComputeSma(double[] close, double[] output, int period,
        Core.SmaCpuKernel.GridSummary summary, CancellationToken cancellation, bool inPlace, bool boundedPositive)
    {
        var proof = new Core.SmaCpuKernel.Certificate(period);
        bool certified = period == 1 || period > close.Length || summary.Certifies(period);
        if (!certified && summary.CanRefine)
        {
            certified = true;
            foreach (var value in close)
            {
                cancellation.ThrowIfCancellationRequested();
                if (!proof.Include(value)) { certified = false; break; }
            }
        }
        if (inPlace)
        {
            Core.SmaCpuKernel.ProcessInPlace(output, period, certified, cancellation, boundedPositive);
            return;
        }
        var reader = new Core.SmaCpuKernel.DoubleReader();
        var consumer = new Core.SmaCpuKernel.Identity();
        if (certified) Core.SmaCpuKernel.ProcessCertified<double, Core.SmaCpuKernel.DoubleReader, Core.SmaCpuKernel.Identity>(
            close, output, period, ref reader, ref consumer, cancellation);
        else Core.SmaCpuKernel.ProcessGuarded<double, Core.SmaCpuKernel.DoubleReader, Core.SmaCpuKernel.Identity>(
            close, output, period, ref reader, ref consumer, cancellation);
    }

    private sealed class Node : IDisposable
    {
        internal IIndicator Indicator { get; }
        internal double[][] Values { get; }
        internal ExceptionDispatchInfo? Failure { get; private set; }
        private readonly object? _state;
        private readonly double[] _scratch;
        internal bool HasScalarState => _state is IIndicatorState;
        internal Node(IIndicator indicator, int count)
        {
            Indicator = indicator;
            Values = Enumerable.Range(0, indicator.Outputs.Count).Select(_ => indicator is Sma or PriceCircularTransform ? GC.AllocateUninitializedArray<double>(count) : new double[count]).ToArray();
            _scratch = new double[indicator.Outputs.Count];
            _state = indicator switch
            {
                Sma or PriceCircularTransform => null,
                RetrospectiveFractals f => (long)f.LeftSpan + f.RightSpan + 1 > count ? null
                    : IndicatorKernels.Fractal(f.LeftSpan, f.RightSpan, f.UseClose),
                IndicatorBase single => single.CreateState(),
                MultiOutputIndicatorBase multi => multi.CreateState(),
                _ => throw new InvalidOperationException("Unqualified values plan.")
            };
        }
        internal void Append(in Bar bar, int index)
        {
            if (Failure is not null || Indicator is Sma) return;
            try
            {
                if (Indicator is PriceCircularTransform)
                {
                    bool defined = bar.Close is >= -1 and <= 1;
                    Values[0][index] = defined ? Math.Asin(bar.Close) : 0;
                    Values[1][index] = defined ? 1 : 0;
                }
                else if (Indicator is RetrospectiveFractals f)
                {
                    if (_state is not IndicatorKernel kernel) return;
                    kernel.Update(in bar, _scratch);
                    int center = index - f.RightSpan;
                    if (center < 0) return;
                    for (int slot = 0; slot < 2; slot++)
                    {
                        bool present = !double.IsNaN(_scratch[slot]);
                        Values[slot][center] = present ? _scratch[slot] : 0;
                        Values[slot + 2][center] = present ? 1 : 0;
                    }
                }
                else if (_state is IIndicatorState single) Values[0][index] = single.Update(in bar);
                else if (_state is IMultiOutputState multi)
                {
                    Array.Clear(_scratch);
                    multi.Update(in bar, _scratch);
                    for (int slot = 0; slot < Values.Length; slot++) Values[slot][index] = _scratch[slot];
                }
                else throw new InvalidOperationException("Values plan has no state.");
            }
            catch (Exception error)
            {
                // Validate remaining raw bars before surfacing arithmetic failures,
                // matching the finite builder's input-before-output validation order.
                Failure = ExceptionDispatchInfo.Capture(error);
            }
        }
        internal Bar FillScalar(Bar[] source, CancellationToken cancellation)
        {
            // Keep the existing Rickshaw batch's concrete Update call available to
            // the JIT; an interface call here loses its hot-loop specialization.
            if (_state is RickshawGridState grid) return FillScalar(source, new GridUpdate(grid), cancellation);
            return FillScalar(source, new StateUpdate((IIndicatorState)_state!), cancellation);
        }
        private interface IScalarUpdate { double Update(in Bar bar); }
        private readonly struct GridUpdate(RickshawGridState state) : IScalarUpdate
        {
            public double Update(in Bar bar) => state.Update(in bar);
        }
        private readonly struct StateUpdate(IIndicatorState state) : IScalarUpdate
        {
            public double Update(in Bar bar) => state.Update(in bar);
        }
        private Bar FillScalar<T>(Bar[] source, T state, CancellationToken cancellation) where T : struct, IScalarUpdate
        {
            var output = Values[0];
            Bar latest = default;
            for (int i = 0; i < source.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                var bar = source[i];
                latest = bar;
                if (!double.IsFinite(bar.Open) || !double.IsFinite(bar.High) || !double.IsFinite(bar.Low)
                    || !double.IsFinite(bar.Close) || !double.IsFinite(bar.Volume))
                    IndicatorInputDomain.Finite.Validate(in bar);
                if (Failure is not null) continue;
                try { output[i] = state.Update(in bar); }
                catch (Exception error) { Failure = ExceptionDispatchInfo.Capture(error); }
            }
            return latest;
        }
        public void Dispose() => (_state as IDisposable)?.Dispose();
    }
}
#endif
