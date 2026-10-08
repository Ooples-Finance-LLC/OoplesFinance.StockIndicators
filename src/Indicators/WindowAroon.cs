using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Aroon up, down, and oscillator with explicit window and equal-extreme conventions.</summary>
/// <remarks>By default the current window contains Period+1 candles and the newest equal
/// extreme wins. Setting includeExtraBar to false uses Period candles. Setting oldestTie
/// selects the oldest equal extreme. All outputs require a complete window and have presence
/// flags. Up/down are 100*(Period-age)/Period; the oscillator is their exact mathematical
/// difference before rounding. Storage grows lazily, including maximum periods.</remarks>
public sealed class WindowAroon : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates Aroon with a positive period and explicit window/tie conventions.</summary>
    public WindowAroon(int period = 14, bool includeExtraBar = true, bool oldestTie = false)
        : base(6)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
        IncludeExtraBar = includeExtraBar;
        OldestTie = oldestTie;
    }

    /// <summary>Positive divisor in the percentage age formula.</summary>
    public int Period { get; }

    /// <summary>Whether the current-inclusive window contains Period+1 rather than Period candles.</summary>
    public bool IncludeExtraBar { get; }

    /// <summary>Whether the oldest rather than newest equal extreme wins.</summary>
    public bool OldestTie { get; }

    /// <summary>Percentage age of the selected highest high.</summary>
    public IIndicatorOutput Up => Outputs[0];

    /// <summary>Percentage age of the selected lowest low.</summary>
    public IIndicatorOutput Down => Outputs[1];

    /// <summary>Exact percentage age difference, rounded once independently of Up and Down.</summary>
    public IIndicatorOutput Oscillator => Outputs[2];

    /// <summary>One when a complete window exists.</summary>
    public IIndicatorOutput UpIsDefined => Outputs[3];

    /// <summary>One when a complete window exists.</summary>
    public IIndicatorOutput DownIsDefined => Outputs[4];

    /// <summary>One when a complete window exists.</summary>
    public IIndicatorOutput OscillatorIsDefined => Outputs[5];

    /// <inheritdoc/>
    public override IIndicatorOutput PrimaryOutput => Oscillator;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new State(Period, IncludeExtraBar, OldestTie);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 6)
            .Select(slot =>
                IndicatorValidationRule.Reference(slot, bars => Reference(bars)[slot], 0, 0)
            );

    private double[][] Reference(IReadOnlyList<Bar> bars)
    {
        var result = Enumerable.Range(0, 6).Select(_ => new double[bars.Count]).ToArray();
        var size = (long)Period + (IncludeExtraBar ? 1 : 0);
        for (var i = 0; i < bars.Count; i++)
        {
            if (i + 1 < size)
                continue;
            var indices = Enumerable.Range(i - (int)size + 1, (int)size).ToArray();
            var highs = indices.OrderByDescending(j => bars[j].High);
            var lows = indices.OrderBy(j => bars[j].Low);
            var high = (OldestTie ? highs.ThenBy(j => j) : highs.ThenByDescending(j => j)).First();
            var low = (OldestTie ? lows.ThenBy(j => j) : lows.ThenByDescending(j => j)).First();
            result[0][i] = (
                new ReferenceFraction(100L * (Period - (i - high))) / new ReferenceFraction(Period)
            ).ToDouble();
            result[1][i] = (
                new ReferenceFraction(100L * (Period - (i - low))) / new ReferenceFraction(Period)
            ).ToDouble();
            result[2][i] = (
                new ReferenceFraction(100L * (high - low)) / new ReferenceFraction(Period)
            ).ToDouble();
            result[3][i] = result[4][i] = result[5][i] = 1;
        }
        return result;
    }

    private sealed class State(int period, bool extra, bool oldest) : IMultiOutputState
    {
        private readonly long _size = (long)period + (extra ? 1 : 0);
        private readonly WindowExtremeDeque _high =
                new((long)period + (extra ? 1 : 0), true, !oldest),
            _low = new((long)period + (extra ? 1 : 0), false, !oldest);
        private long _index;

        public void Reset()
        {
            _high.Reset();
            _low.Reset();
            _index = 0;
        }

        public void Update(in Bar bar, Span<double> outputs)
        {
            outputs.Clear();
            _high.Add(bar.High);
            _low.Add(bar.Low);
            if (_index + 1 >= _size)
            {
                outputs[0] = Percentage(period - (_index - _high.Index), period);
                outputs[1] = Percentage(period - (_index - _low.Index), period);
                outputs[2] = Percentage(_high.Index - _low.Index, period);
                outputs[3] = outputs[4] = outputs[5] = 1;
            }
            _index++;
        }

        private static double Percentage(long value, int period)
        {
            var sum = new ExactMeanAccumulator();
            sum.Add(value, 100);
            return sum.Mean(period);
        }
    }
}
