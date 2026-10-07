using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Volume-seeded accumulation/distribution with price-change contributions on zero-range candles.</summary>
/// <remarks>The first value is the first volume. Later nonzero-range candles add
/// (2*close-high-low)/(high-low)*volume. A zero-range candle instead adds
/// (close/previousClose-1)*volume. A zero previous close in that branch makes this
/// and all subsequent values unavailable until reset. Each contribution is rounded to binary64
/// precision with an extended upper exponent before exact accumulation. The runtime rejects
/// unrepresentable published totals. These are Trady's initialization and flat-candle rules.</remarks>
public sealed class PriceAdjustedAccumulationDistribution
    : MultiOutputIndicatorBase,
        IVolumeIndicator,
        IIndicatorValidationContract
{
    /// <summary>Creates a volume-seeded accumulation/distribution line.</summary>
    public PriceAdjustedAccumulationDistribution()
        : base(2) => (Value, IsDefined) = DeclaredOutputs;

    /// <summary>Cumulative value, or a zero placeholder when unavailable.</summary>
    public IIndicatorOutput Value { get; }

    /// <summary>One until an undefined flat-candle ratio; then zero until reset.</summary>
    public IIndicatorOutput IsDefined { get; }

    /// <inheritdoc/>
    protected internal override object CreateState() => new State();

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.ReferenceWithOverflowRejection(
                0,
                bars => Reference(bars, false),
                IndicatorErrorBudget.Exact
            ),
            IndicatorValidationRule.Reference(1, bars => Reference(bars, true), 0, 0),
            IndicatorValidationRule.Bounds(1, 0, 1),
        ];

    private static IReadOnlyList<double> Reference(IReadOnlyList<Bar> bars, bool validity)
    {
        var values = new double[bars.Count];
        if (bars.Count == 0)
            return values;
        var total = R(bars[0].Volume);
        values[0] = validity ? 1 : bars[0].Volume;
        for (var i = 1; i < bars.Count; i++)
        {
            var b = bars[i];
            var flat = R(b.High).CompareTo(R(b.Low)) == 0;
            if (flat && bars[i - 1].Close == 0)
                break;
            var ratio = flat
                ? R(b.Close) / R(bars[i - 1].Close) - new ReferenceFraction(1)
                : ((R(b.Close) - R(b.Low)) - (R(b.High) - R(b.Close))) / (R(b.High) - R(b.Low));
            total += (ratio * R(b.Volume)).RoundExtendedBinary64();
            values[i] = validity ? 1 : total.ToDouble();
        }
        return values;
    }

    private static ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);

    private sealed class State : IMultiOutputState
    {
        private ExactMeanAccumulator _total;
        private double _previous;
        private bool _started,
            _defined = true;

        public void Reset()
        {
            _total = default;
            _previous = 0;
            _started = false;
            _defined = true;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            if (!_defined)
                return;
            if (!_started)
            {
                _total.Add(bar.Volume);
                _started = true;
            }
#pragma warning disable S1244 // The alternate price-change formula applies only to an exactly flat candle.
            else if (bar.High == bar.Low)
#pragma warning restore S1244
            {
                if (_previous == 0)
                {
                    _defined = false;
                    return;
                }
                var numerator = new ExactMeanAccumulator();
                numerator.AddProduct(bar.Close, bar.Volume);
                numerator.AddProduct(_previous, bar.Volume, -1);
                var flow = RocBankValue.Round(numerator, _previous);
                flow.AddTo(ref _total);
            }
            else
            {
                var flow = MoneyFlowAccumulationWindow.Flow(
                    bar.High,
                    bar.Low,
                    bar.Close,
                    bar.Volume
                );
                flow.AddTo(ref _total);
            }
            _previous = bar.Close;
            output[0] = _total.Mean(1);
            output[1] = 1;
        }
    }
}
