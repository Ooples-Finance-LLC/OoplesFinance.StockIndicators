using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Difference of two full-window simple or first-price exponential averages.</summary>
/// <remarks>Each average and the final difference round separately. Simple averages
/// wait for both complete windows; exponential averages start at the first price.
/// Equal and reversed periods are supported. History grows lazily; genuine final
/// overflow is rejected by the runtime.</remarks>
public sealed class MovingAverageDifference : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a close-price oscillator with positive periods.</summary>
    public MovingAverageDifference(
        int firstPeriod = 12,
        int secondPeriod = 26,
        bool exponential = false
    )
        : base(2)
    {
        if (firstPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(firstPeriod));
        if (secondPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(secondPeriod));
        FirstPeriod = firstPeriod;
        SecondPeriod = secondPeriod;
        Exponential = exponential;
    }

    /// <summary>Period of the first average.</summary>
    public int FirstPeriod { get; }

    /// <summary>Period of the subtracted average.</summary>
    public int SecondPeriod { get; }

    /// <summary>Whether to use first-price EMA instead of full-window SMA.</summary>
    public bool Exponential { get; }

    /// <summary>First average minus second average, or zero before startup.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>One when both averages exist, otherwise zero.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new State(FirstPeriod, SecondPeriod, Exponential);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 2)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars => Reference(bars, slot),
                    IndicatorErrorBudget.Exact
                )
            );

    private IReadOnlyList<double> Reference(IReadOnlyList<Bar> bars, int slot)
    {
        var result = new double[bars.Count];
        double[] Average(int period)
        {
            var values = new double[bars.Count];
            for (var i = 0; i < bars.Count; i++)
            {
                if (Exponential)
                    values[i] =
                        i == 0
                            ? bars[i].Close
                            : (
                                (
                                    ReferenceFraction.FromDouble(bars[i].Close)
                                        * new ReferenceFraction(2)
                                    + ReferenceFraction.FromDouble(values[i - 1])
                                        * new ReferenceFraction(period - 1)
                                ) / new ReferenceFraction((long)period + 1)
                            ).ToDouble();
                else if (i >= period - 1)
                {
                    var sum = new ReferenceFraction(0);
                    for (var j = i - period + 1; j <= i; j++)
                        sum += ReferenceFraction.FromDouble(bars[j].Close);
                    values[i] = (sum / new ReferenceFraction(period)).ToDouble();
                }
            }
            return values;
        }
        var first = Average(FirstPeriod);
        var second = Average(SecondPeriod);
        for (var i = Exponential ? 0 : Math.Max(FirstPeriod, SecondPeriod) - 1; i < bars.Count; i++)
            result[i] =
                slot == 1
                    ? 1
                    : (
                        ReferenceFraction.FromDouble(first[i])
                        - ReferenceFraction.FromDouble(second[i])
                    ).ToDouble();
        return result;
    }

    private sealed class Average(int period, bool exponential)
    {
        private readonly Queue<double> _history = new();
        private ExactMeanAccumulator _sum;
        private double _previous;
        private int _count;
        internal bool Ready => exponential ? _count > 0 : _count == period;

        internal void Reset()
        {
            _history.Clear();
            _sum = default;
            _previous = 0;
            _count = 0;
        }

        internal double Update(double price)
        {
            if (exponential)
            {
                if (_count == 0)
                {
                    _count = 1;
                    return _previous = price;
                }
                var next = new ExactMeanAccumulator();
                next.Add(price, 2);
                next.Add(_previous, period - 1);
                return _previous = next.Mean((long)period + 1);
            }
            if (_count == period)
                _sum.Add(_history.Dequeue(), -1);
            else
                _count++;
            _history.Enqueue(price);
            _sum.Add(price);
            return Ready ? _sum.Mean(period) : 0;
        }
    }

    private sealed class State(int first, int second, bool exponential) : IMultiOutputState
    {
        private readonly Average _first = new(first, exponential),
            _second = new(second, exponential);

        public void Reset()
        {
            _first.Reset();
            _second.Reset();
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            var a = _first.Update(bar.Close);
            var b = _second.Update(bar.Close);
            if (!_first.Ready || !_second.Ready)
                return;
            var difference = new ExactMeanAccumulator();
            difference.Add(a);
            difference.Add(b, -1);
            output[0] = difference.Mean(1);
            output[1] = 1;
        }
    }
}
