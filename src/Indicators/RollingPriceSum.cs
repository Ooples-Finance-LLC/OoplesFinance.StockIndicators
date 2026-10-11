using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Sum of the last Period closes, rounded once after exact accumulation.</summary>
/// <remarks>Startup returns zero until a complete window exists. True output overflow is rejected by
/// the runtime. Storage grows with observed history; construction does not allocate a full period.</remarks>
public sealed class RollingPriceSum : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a rolling sum over a positive number of prices.</summary>
    public RollingPriceSum(int period = 14)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Number of prices to sum.</summary>
    public int Period { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Period - 1;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.ReferenceWithOverflowRejection(
                0,
                Reference,
                IndicatorErrorBudget.Exact
            ),
        ];

    private IReadOnlyList<double> Reference(IReadOnlyList<Bar> bars)
    {
        var values = new double[bars.Count];
        for (var i = Period - 1; i < bars.Count; i++)
        {
            var sum = new ReferenceFraction(0);
            for (var j = i - Period + 1; j <= i; j++)
                sum += ReferenceFraction.FromDouble(bars[j].Close);
            values[i] = sum.ToDouble();
        }
        return values;
    }

    private sealed class State(int period) : IIndicatorState, IOwnedHistoryBatchState
    {
        private readonly Queue<double> _prices = new();
        private ExactMeanAccumulator _sum;

        public bool TryComputeBatch(OwnedBarHistory bars, double[][] output)
        {
#if NETFRAMEWORK
            return false;
#else
            var values = output[0];
            if (period > bars.Count) { values.AsSpan().Clear(); return true; }
            // Reuse the pilot's grid proof. Every partial/window sum is exact;
            // eviction occurs first so no temporary contains period+1 terms.
            // Failed speculation does not mutate this state: the engine replays
            // its established exact accumulator before publishing anything.
            var proof = new Core.SmaCpuKernel.GridFacts();
            double sum = 0;
            var offset = 0;
            for (var chunk = 0; chunk < bars.ChunkCount; chunk++)
            {
                var input = bars.Chunk(chunk);
                for (var j = 0; j < input.Length; j++)
                {
                    var i = offset + j;
                    var value = input[j].Close;
                    proof.Include(value);
                    if (!proof.Certifies(period)) return false;
                    if (i >= period) sum -= bars[i - period].Close;
                    sum += value;
                    values[i] = i < period - 1 ? 0 : sum;
                }
                offset += input.Length;
            }
            return true;
#endif
        }

        public void Reset()
        {
            _prices.Clear();
            _sum = default;
        }

        public double Update(in Bar bar)
        {
            if (_prices.Count == period)
                _sum.Add(_prices.Dequeue(), -1);
            _prices.Enqueue(bar.Close);
            _sum.Add(bar.Close);
            return _prices.Count < period ? 0 : _sum.Mean(1);
        }
    }
}
