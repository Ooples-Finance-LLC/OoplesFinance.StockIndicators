using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Period rounding and startup rules for Hull's three weighted averages.</summary>
public enum HullWindowConvention
{
    /// <summary>Floor half/root periods; normalize each available fixed-period weight prefix immediately.</summary>
    ExpandingFloor,

    /// <summary>Floor half/root periods; wait for the full input and final root-period windows.</summary>
    FullWindowFloor,

    /// <summary>Round half/root periods to nearest, with even half ties; wait for both full windows.</summary>
    FullWindowRounded,
}

/// <summary>Hull average: WMA(2*WMA(close,half)-WMA(close,period),root).</summary>
/// <remarks>Each complete stage rounds once. Unpublished synthetic values keep binary64
/// precision with an extended upper exponent, allowing a finite final average after an
/// oversized intermediate. Histories grow lazily and updates use rolling exact moments.
/// Full-window startup is represented by Value zero and IsDefined zero.</remarks>
public sealed class HullWindow : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a Hull average with a period of at least two.</summary>
    public HullWindow(
        int period = 14,
        HullWindowConvention convention = HullWindowConvention.FullWindowFloor
    )
        : base(2)
    {
        if (period < 2)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (!Enum.IsDefined(typeof(HullWindowConvention), convention))
            throw new ArgumentOutOfRangeException(nameof(convention));
        Period = period;
        Convention = convention;
        HalfPeriod =
            convention == HullWindowConvention.FullWindowRounded
                ? (int)Math.Round(period / 2d)
                : period / 2;
        RootPeriod =
            convention == HullWindowConvention.FullWindowRounded
                ? (int)Math.Round(Math.Sqrt(period))
                : (int)Math.Sqrt(period);
    }

    /// <summary>Full input window length.</summary>
    public int Period { get; }

    /// <summary>Startup and period-rounding convention.</summary>
    public HullWindowConvention Convention { get; }

    /// <summary>Length of the shorter first-stage weighted average.</summary>
    public int HalfPeriod { get; }

    /// <summary>Length of the final weighted average.</summary>
    public int RootPeriod { get; }

    /// <summary>Published Hull average, or zero during absent startup.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>One for a present average; otherwise zero.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];

    /// <inheritdoc/>
    public override int WarmupBars => (int)Math.Min(int.MaxValue, (long)Period + RootPeriod - 2);

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new State(
            Period,
            HalfPeriod,
            RootPeriod,
            Convention == HullWindowConvention.ExpandingFloor
        );

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 2)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars => Reference(bars)[slot],
                    IndicatorErrorBudget.Exact
                )
            );

    internal double[][] Reference(IReadOnlyList<Bar> bars)
    {
        var result = new[] { new double[bars.Count], new double[bars.Count] };
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var synthetic = new List<ReferenceFraction>();
        for (var i = 0; i < bars.Count; i++)
        {
            if (Convention != HullWindowConvention.ExpandingFloor && i < Period - 1)
                continue;
            var half = Average(prices, i, HalfPeriod);
            var full = Average(prices, i, Period);
            synthetic.Add((new ReferenceFraction(2) * half - full).RoundExtendedBinary64());
            if (Convention != HullWindowConvention.ExpandingFloor && synthetic.Count < RootPeriod)
                continue;
            result[0][i] = Average(synthetic, synthetic.Count - 1, RootPeriod).ToDouble();
            result[1][i] = 1;
        }
        return result;
    }

    private static ReferenceFraction Average(
        IReadOnlyList<ReferenceFraction> values,
        int end,
        int period
    )
    {
        var sum = new ReferenceFraction(0);
        long mass = 0;
        for (var lag = 0; lag < Math.Min(period, end + 1); lag++)
        {
            sum += values[end - lag] * new ReferenceFraction(period - lag);
            mass += period - lag;
        }
        return (sum / new ReferenceFraction(mass)).RoundExtendedBinary64();
    }

    private sealed class State(int period, int halfPeriod, int rootPeriod, bool expanding)
        : IMultiOutputState
    {
        private readonly WeightedStage _half = new(halfPeriod),
            _full = new(period),
            _final = new(rootPeriod);

        public void Reset()
        {
            _half.Reset();
            _full.Reset();
            _final.Reset();
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            var half = _half.Next(new RocBankValue(bar.Close));
            var full = _full.Next(new RocBankValue(bar.Close));
            if (!expanding && _full.Count < period)
                return;
            var difference = new ExactMeanAccumulator();
            half.AddTo(ref difference, 2);
            full.AddTo(ref difference, -1);
            var result = _final.Next(RocBankValue.Round(difference));
            if (!expanding && _final.Count < rootPeriod)
                return;
            output[0] = result.Publish();
            output[1] = 1;
        }
    }

    private sealed class WeightedStage(int period)
    {
        private readonly Queue<RocBankValue> _window = new();
        private ExactMeanAccumulator _sum,
            _weighted;
        internal int Count => _window.Count;

        internal void Reset()
        {
            _window.Clear();
            _sum = _weighted = default;
        }

        internal RocBankValue Next(RocBankValue value)
        {
            _weighted.Subtract(_sum);
            if (_window.Count == period)
                _window.Dequeue().AddTo(ref _sum, -1);
            _window.Enqueue(value);
            value.AddTo(ref _sum);
            value.AddTo(ref _weighted, period);
            var mass = (long)Count * (2L * period - Count + 1) / 2;
            return RocBankValue.Round(_weighted, count: mass);
        }
    }
}
