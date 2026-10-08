using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Returns one for a bearish close below the midpoint of a prior bullish candle after an uptrend, reported with a configurable delay.</summary>
/// <remarks>The bearish candle must open strictly above the prior close. Uptrend counts strictly rising highs and lows ending on the bullish anchor.
/// Delay one reports on the bearish candle; larger delays defer the same result without requiring subsequent downward movement.
/// There is no long-body requirement or lower bound on the bearish close. Incomplete history returns zero.</remarks>
public sealed class DelayedDarkCloudCoverPattern : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a pattern with positive uptrend and reporting-delay counts.</summary>
    public DelayedDarkCloudCoverPattern(int upTrendPeriod = 3, int delay = 3)
    {
        if (upTrendPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(upTrendPeriod));
        if (delay < 1)
            throw new ArgumentOutOfRangeException(nameof(delay));
        UpTrendPeriod = upTrendPeriod;
        Delay = delay;
    }

    /// <summary>Number of strictly rising high/low transitions preceding the bearish candle.</summary>
    public int UpTrendPeriod { get; }

    /// <summary>Output offset from the bullish anchor; one reports on the following candle.</summary>
    public int Delay { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Delay;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(UpTrendPeriod, Delay);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, 0, 1),
            IndicatorValidationRule.Reference(0, Reference, 0, 0),
        ];

    private IReadOnlyList<double> Reference(IReadOnlyList<Bar> bars)
    {
        var result = new double[bars.Count];
        for (var i = Delay; i < bars.Count; i++)
        {
            var anchor = i - Delay;
            if (anchor < UpTrendPeriod)
                continue;
            var a = bars[anchor];
            var b = bars[anchor + 1];
            if (a.Close <= a.Open || b.Close >= b.Open || b.Open <= a.Close)
                continue;
            var midpoint =
                (ReferenceFraction.FromDouble(a.Open) + ReferenceFraction.FromDouble(a.Close))
                / new ReferenceFraction(2);
            if (ReferenceFraction.FromDouble(b.Close).CompareTo(midpoint) >= 0)
                continue;
            var trend = true;
            for (var j = anchor - UpTrendPeriod + 1; j <= anchor; j++)
                trend &= bars[j].High > bars[j - 1].High && bars[j].Low > bars[j - 1].Low;
            if (trend)
                result[i] = 1;
        }
        return result;
    }

    private sealed class State(int period, int delay) : IIndicatorState
    {
        private Bar _previous;
        private bool _initialized;
        private int _trend;
        private readonly Queue<bool> _pending = new();

        public void Reset()
        {
            _previous = default;
            _initialized = false;
            _trend = 0;
            _pending.Clear();
        }

        public double Update(in Bar bar)
        {
            var match = false;
            if (_initialized)
            {
                if (
                    _trend >= period
                    && _previous.Close > _previous.Open
                    && bar.Close < bar.Open
                    && bar.Open > _previous.Close
                )
                {
                    var margin = new ExactMeanAccumulator();
                    margin.Add(_previous.Open);
                    margin.Add(_previous.Close);
                    margin.Add(bar.Close, -2);
                    match = margin.Sign > 0;
                }
                _trend =
                    bar.High > _previous.High && bar.Low > _previous.Low
                        ? _trend == period
                            ? period
                            : _trend + 1
                        : 0;
            }
            _previous = bar;
            _initialized = true;
            _pending.Enqueue(match);
            return _pending.Count >= delay && _pending.Dequeue() ? 1 : 0;
        }
    }
}
