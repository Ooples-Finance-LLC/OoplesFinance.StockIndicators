#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static bool IsWeightedChain(IReadOnlyList<IIndicator> indicators) => indicators.Count == 1
        && indicators[0] is FixedPeriodWma { Components.Count: 0,
            Source: FixedPeriodWma { Source: null, Components.Count: 0 } first } last
        && (long)first.Period + last.Period - 1 <= int.MaxValue;

    private static IIndicatorRun ExecuteWeightedChain(Bar[] source, FixedPeriodWma last,
        CancellationToken cancellation, OwnedBarHistory? history)
    {
        var first = (FixedPeriodWma)last.Source!;
        // Only configured outputs are public; keep the intermediate in rolling state.
        double[][] values = [new double[source.Length]];
        var owned = history is null ? null : new OwnedBarBuffer(source.Length);
        int replay = (int)((long)first.Period + last.Period - 1);
        var latest = FillBoundedWindow<WeightedChainKernel>(source, values, last, replay, cancellation, owned);
        var published = new Dictionary<IIndicatorOutput, double[]>
        {
            [last.Outputs[0]] = values[0]
        };
        int warmup = Math.Max(first.WarmupBars, last.WarmupBars);
        if (history is not null)
        {
            owned!.TransferTo(history);
            return new IndicatorRun(null, published, history, 0, warmup);
        }
        IBarSnapshot? snapshot = source.Length == 0 ? null
            : new BarSnapshot(latest, source.Length - 1, published, source.Length >= warmup);
        return new LatestOnlyIndicatorRun(published, source.Length, snapshot);
    }

    private readonly struct WeightedChainKernel(WmaValueState first, WmaValueState last)
        : IBoundedKernel<WeightedChainKernel>
    {
        public static WeightedChainKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target)
        {
            var last = (FixedPeriodWma)indicator;
            var first = (FixedPeriodWma)last.Source!;
            return new(new(first.Period, count, true), new(last.Period, count, true));
        }
        public double Update(in Bar bar)
        {
            double middle = first.Update(in bar);
            // Preserve the published binary64 intermediate, not an unrounded FIR.
            var projected = new Bar(bar.Time, bar.Open, bar.High, bar.Low, middle, bar.Volume);
            return last.Update(in projected);
        }
    }
}
#endif
