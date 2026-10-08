using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Wilder ATR seeded by the mean of the first period true ranges after the initial candle.</summary>
/// <remarks>The initial candle establishes previous close. The first ATR is published at
/// zero-based index period; earlier values are zero. Each true range and recurrence is rounded
/// to binary64 precision with extended upper exponents, so unpublished overflow cannot destroy
/// a finite average. Unrepresentable published averages are rejected by the runtime.</remarks>
public sealed class SeededAverageTrueRange : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a Wilder ATR with a positive seed and smoothing period.</summary>
    public SeededAverageTrueRange(int period = 14)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Number of ranges in the initial mean and denominator of subsequent Wilder updates.</summary>
    public int Period { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Period;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.ReferenceWithOverflowRejection(
                0,
                bars => SeededAtrReference.Values(bars, Period)[1],
                IndicatorErrorBudget.Exact
            ),
        ];

    private sealed class State(int period) : IIndicatorState
    {
        private readonly SeededAtrWindow _window = new(period);

        public void Reset() => _window.Reset();

        public double Update(in Bar bar)
        {
            _window.Add(bar);
            return _window.Average.Publish();
        }
    }
}

/// <summary>Seeded Wilder ATR with true range, ATR percentage, and explicit presence flags.</summary>
/// <remarks>TrueRange starts at the second candle; Average and Percent start at index period.
/// Percent is 100*Average/close and is absent when close is zero. Missing values use zero
/// placeholders. Exact range comparisons and extended-exponent stages preserve finite outputs;
/// the runtime rejects any unrepresentable published output.</remarks>
public sealed class AverageTrueRangeWithDetails
    : MultiOutputIndicatorBase,
        IIndicatorValidationContract
{
    /// <summary>Creates detailed Wilder ATR with a positive period.</summary>
    public AverageTrueRangeWithDetails(int period = 14)
        : base(6)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
        (TrueRange, Average, Percent, TrueRangeIsDefined, AverageIsDefined, PercentIsDefined) = (
            Outputs[0],
            Outputs[1],
            Outputs[2],
            Outputs[3],
            Outputs[4],
            Outputs[5]
        );
    }

    /// <summary>Seed and smoothing period.</summary>
    public int Period { get; }

    /// <summary>Maximum of high-low, absolute high-previousClose and absolute low-previousClose.</summary>
    public IIndicatorOutput TrueRange { get; }

    /// <summary>Mean-seeded Wilder average of true range.</summary>
    public IIndicatorOutput Average { get; }

    /// <summary>ATR as a percentage of current close; zero placeholder when unavailable.</summary>
    public IIndicatorOutput Percent { get; }

    /// <summary>One after the first candle, otherwise zero.</summary>
    public IIndicatorOutput TrueRangeIsDefined { get; }

    /// <summary>One after the complete seed window, otherwise zero.</summary>
    public IIndicatorOutput AverageIsDefined { get; }

    /// <summary>One when the average exists and close is nonzero, otherwise zero.</summary>
    public IIndicatorOutput PercentIsDefined { get; }

    /// <inheritdoc/>
    public override IIndicatorOutput PrimaryOutput => Average;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 6)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars => SeededAtrReference.Values(bars, Period)[slot],
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(int period) : IMultiOutputState
    {
        private readonly SeededAtrWindow _window = new(period);

        public void Reset() => _window.Reset();

        public void Update(in Bar bar, Span<double> output)
        {
            _window.Add(bar);
            output.Clear();
            output[0] = _window.Range.Publish();
            output[1] = _window.Average.Publish();
            output[3] = _window.HasRange ? 1 : 0;
            output[4] = _window.HasAverage ? 1 : 0;
            if (!_window.HasAverage || bar.Close == 0)
                return;
            output[2] = _window.Percent(bar.Close);
            output[5] = 1;
        }
    }
}

internal sealed class SeededAtrWindow(int period)
{
    internal double Percent(double close)
    {
        if (!HasAverage || close == 0)
            return 0;
        var numerator = new ExactMeanAccumulator();
        Average.AddTo(ref numerator, 100);
        var denominator = new ExactMeanAccumulator();
        denominator.Add(close);
        return numerator.Ratio(denominator);
    }

    private bool _started;
    private double _previousClose;
    private int _seedCount;
    private ExactMeanAccumulator _seed;
    internal RocBankValue Range { get; private set; }
    internal RocBankValue Average { get; private set; }
    internal bool HasRange { get; private set; }
    internal bool HasAverage => _seedCount == period;

    internal void Reset()
    {
        _started = HasRange = false;
        _previousClose = 0;
        _seedCount = 0;
        _seed = default;
        Range = Average = default;
    }

    internal void Add(in Bar bar)
    {
        if (!_started)
        {
            _started = true;
            _previousClose = bar.Close;
            return;
        }
        HasRange = true;
        var high = ExactVarianceWindow.Units(bar.High);
        var low = ExactVarianceWindow.Units(bar.Low);
        var previous = ExactVarianceWindow.Units(_previousClose);
        var range = System.Numerics.BigInteger.Max(
            high - low,
            System.Numerics.BigInteger.Max(
                System.Numerics.BigInteger.Abs(high - previous),
                System.Numerics.BigInteger.Abs(low - previous)
            )
        );
        // Convert the exact selected difference once, retaining an extended exponent when needed.
        var difference = new ExactMeanAccumulator();
        difference.Add(double.Epsilon, range);
        Range = RocBankValue.Round(difference);
        if (_seedCount < period)
        {
            Range.AddTo(ref _seed);
            if (++_seedCount == period)
            {
                Average = RocBankValue.Round(_seed, count: period);
                _seed = default;
            }
        }
        else
        {
            var sum = new ExactMeanAccumulator();
            Average.AddTo(ref sum, period - 1);
            Range.AddTo(ref sum);
            Average = RocBankValue.Round(sum, count: period);
        }
        _previousClose = bar.Close;
    }
}

internal static class SeededAtrReference
{
    internal static double[][] Values(IReadOnlyList<Bar> bars, int period)
    {
        var result = Enumerable.Range(0, 6).Select(_ => new double[bars.Count]).ToArray();
        var seed = new ReferenceFraction(0);
        var average = new ReferenceFraction(0);
        for (var i = 1; i < bars.Count; i++)
        {
            var high = ReferenceFraction.FromDouble(bars[i].High);
            var low = ReferenceFraction.FromDouble(bars[i].Low);
            var previous = ReferenceFraction.FromDouble(bars[i - 1].Close);
            var range = new[] { high - low, (high - previous).Abs(), (low - previous).Abs() }
                .Max()
                .RoundExtendedBinary64();
            result[0][i] = range.ToDouble();
            result[3][i] = 1;
            if (i <= period)
                seed += range;
            if (i < period)
                continue;
            average = (
                i == period
                    ? seed / new ReferenceFraction(period)
                    : (average * new ReferenceFraction(period - 1) + range)
                        / new ReferenceFraction(period)
            ).RoundExtendedBinary64();
            result[1][i] = average.ToDouble();
            result[4][i] = 1;
            if (bars[i].Close == 0)
                continue;
            result[2][i] = (
                average * new ReferenceFraction(100) / ReferenceFraction.FromDouble(bars[i].Close)
            ).ToDouble();
            result[5][i] = 1;
        }
        return result;
    }
}
