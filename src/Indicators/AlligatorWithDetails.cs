using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Three SMA-seeded Wilder averages of midpoint, delayed by positive offsets.</summary>
/// <remarks>Each average has independent presence. Delays preserve input timestamps
/// and do not append future rows. Midpoints and complete smoothing updates round once.
/// Chained input replaces midpoint. Delay storage grows lazily with observed values.</remarks>
public sealed class AlligatorWithDetails : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates descending jaw/teeth/lips periods and descending period-plus-offset starts.</summary>
    public AlligatorWithDetails(
        int jawPeriod = 13,
        int jawOffset = 8,
        int teethPeriod = 8,
        int teethOffset = 5,
        int lipsPeriod = 5,
        int lipsOffset = 3
    )
        : base(6)
    {
        if (lipsPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(lipsPeriod));
        if (teethPeriod <= lipsPeriod)
            throw new ArgumentOutOfRangeException(nameof(teethPeriod));
        if (jawPeriod <= teethPeriod)
            throw new ArgumentOutOfRangeException(nameof(jawPeriod));
        if (lipsOffset < 1)
            throw new ArgumentOutOfRangeException(nameof(lipsOffset));
        if (teethOffset < 1)
            throw new ArgumentOutOfRangeException(nameof(teethOffset));
        if (jawOffset < 1)
            throw new ArgumentOutOfRangeException(nameof(jawOffset));
        if ((long)teethPeriod + teethOffset <= (long)lipsPeriod + lipsOffset)
            throw new ArgumentOutOfRangeException(nameof(teethOffset));
        if ((long)jawPeriod + jawOffset <= (long)teethPeriod + teethOffset)
            throw new ArgumentOutOfRangeException(nameof(jawOffset));
        JawPeriod = jawPeriod;
        JawOffset = jawOffset;
        TeethPeriod = teethPeriod;
        TeethOffset = teethOffset;
        LipsPeriod = lipsPeriod;
        LipsOffset = lipsOffset;
    }

    /// <summary>Jaw averaging period.</summary>
    public int JawPeriod { get; }

    /// <summary>Jaw delay.</summary>
    public int JawOffset { get; }

    /// <summary>Teeth averaging period.</summary>
    public int TeethPeriod { get; }

    /// <summary>Teeth delay.</summary>
    public int TeethOffset { get; }

    /// <summary>Lips averaging period.</summary>
    public int LipsPeriod { get; }

    /// <summary>Lips delay.</summary>
    public int LipsOffset { get; }

    /// <summary>Delayed jaw average, or zero before presence.</summary>
    public IIndicatorOutput Jaw => Outputs[0];

    /// <summary>Delayed teeth average, or zero before presence.</summary>
    public IIndicatorOutput Teeth => Outputs[1];

    /// <summary>Delayed lips average, or zero before presence.</summary>
    public IIndicatorOutput Lips => Outputs[2];

    /// <summary>Jaw presence.</summary>
    public IIndicatorOutput IsJawDefined => Outputs[3];

    /// <summary>Teeth presence.</summary>
    public IIndicatorOutput IsTeethDefined => Outputs[4];

    /// <summary>Lips presence.</summary>
    public IIndicatorOutput IsLipsDefined => Outputs[5];

    /// <inheritdoc/>
    protected internal override object CreateState() => CreateState(Source is not null);

    internal IMultiOutputState CreateState(bool chained) => new State(this, chained);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 6)
            .Select(slot =>
                IndicatorValidationRule.Reference(
                    slot,
                    bars => Reference(bars, Source is not null)[slot],
                    IndicatorErrorBudget.Exact
                )
            );

    internal double[][] Reference(IReadOnlyList<Bar> bars, bool chained)
    {
        var result = Enumerable.Range(0, 6).Select(_ => new double[bars.Count]).ToArray();
        var selected = bars.Select(b =>
            {
                var price = chained
                    ? b.Close
                    : (
                        (ReferenceFraction.FromDouble(b.High) + ReferenceFraction.FromDouble(b.Low))
                        / new ReferenceFraction(2)
                    ).ToDouble();
                return new Bar(b.Time, price, price, price, price, b.Volume);
            })
            .ToArray();
        var periods = new[] { JawPeriod, TeethPeriod, LipsPeriod };
        var offsets = new[] { JawOffset, TeethOffset, LipsOffset };
        for (var slot = 0; slot < 3; slot++)
        {
            var mean = new WilderMovingAverage(periods[slot]).Reference(selected);
            for (var i = periods[slot] - 1; i < bars.Count; i++)
            {
                var target = (long)i + offsets[slot];
                if (target >= bars.Count)
                    break;
                result[slot][(int)target] = mean[i];
                result[slot + 3][(int)target] = 1;
            }
        }
        return result;
    }

    private sealed class Delay(int period, int offset)
    {
        private readonly IIndicatorState _mean = (IIndicatorState)
            new WilderMovingAverage(period).CreateState();
        private readonly Queue<double> _pending = new();
        private int _observed;

        internal void Reset()
        {
            _mean.Reset();
            _pending.Clear();
            _observed = 0;
        }

        internal (double Value, double Present) Update(in Bar bar)
        {
            var value = _mean.Update(bar);
            if (_observed < period)
                _observed++;
            if (_observed < period)
                return (0, 0);
            _pending.Enqueue(value);
            return _pending.Count > offset ? (_pending.Dequeue(), 1) : (0, 0);
        }
    }

    private sealed class State(AlligatorWithDetails owner, bool chained) : IMultiOutputState
    {
        private readonly Delay[] _lines =
        [
            new(owner.JawPeriod, owner.JawOffset),
            new(owner.TeethPeriod, owner.TeethOffset),
            new(owner.LipsPeriod, owner.LipsOffset),
        ];

        public void Reset()
        {
            foreach (var line in _lines)
                line.Reset();
        }

        public void Update(in Bar bar, Span<double> output)
        {
            var sum = new ExactMeanAccumulator();
            sum.Add(bar.High);
            sum.Add(bar.Low);
            var price = chained ? bar.Close : sum.Mean(2);
            var selected = new Bar(bar.Time, price, price, price, price, bar.Volume);
            for (var slot = 0; slot < 3; slot++)
                (output[slot], output[slot + 3]) = _lines[slot].Update(selected);
        }
    }
}
