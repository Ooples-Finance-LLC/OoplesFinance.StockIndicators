using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Index seeded at 100 and updated only on a strict volume increase or decrease.</summary>
/// <remarks>Updates multiply the published index by close/previousClose, retaining the
/// denominator's sign and rounding the complete product ratio once. Equal volume holds
/// the index. A selected update with zero previous close makes the index absent until
/// reset; an unselected update never divides. Unrepresentable published values are rejected
/// by the runtime. Storage is constant.</remarks>
public sealed class VolumeConditionedIndex
    : MultiOutputIndicatorBase,
        IVolumeIndicator,
        IIndicatorValidationContract
{
    /// <summary>Creates a negative-volume index, or a positive-volume index when positive is true.</summary>
    public VolumeConditionedIndex(bool positive = false)
        : base(2) => Positive = positive;

    /// <summary>Whether increasing volume, rather than decreasing volume, triggers an update.</summary>
    public bool Positive { get; }

    /// <summary>Published index, or zero when absent.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>One until an undefined selected price ratio occurs.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];

    /// <inheritdoc/>
    public override IIndicatorOutput PrimaryOutput => Value;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Positive);

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

    private double[][] Reference(IReadOnlyList<Bar> bars)
    {
        var output = new[] { new double[bars.Count], new double[bars.Count] };
        var index = 100d;
        for (var i = 0; i < bars.Count; i++)
        {
            if (
                i > 0
                && (
                    Positive
                        ? bars[i].Volume > bars[i - 1].Volume
                        : bars[i].Volume < bars[i - 1].Volume
                )
            )
            {
                var previous = ReferenceFraction.FromDouble(bars[i - 1].Close);
                if (previous.Sign == 0)
                    break;
                index = (
                    ReferenceFraction.FromDouble(index)
                    * ReferenceFraction.FromDouble(bars[i].Close)
                    / previous
                ).ToDouble();
            }
            output[0][i] = index;
            output[1][i] = 1;
            if (double.IsInfinity(index))
                break;
        }
        return output;
    }

    private sealed class State(bool positive) : IMultiOutputState
    {
        private double _price,
            _volume,
            _index = 100;
        private bool _started,
            _defined = true;

        public void Reset()
        {
            _price = _volume = 0;
            _index = 100;
            _started = false;
            _defined = true;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            if (_started && _defined && (positive ? bar.Volume > _volume : bar.Volume < _volume))
            {
                if (Math.Abs(_price) <= 0)
                    _defined = false;
                else
                {
                    var product = new ExactMeanAccumulator();
                    product.AddProduct(_index, bar.Close);
                    var denominator = new ExactMeanAccumulator();
                    denominator.Add(_price);
                    _index = product.Ratio(denominator);
                }
            }
            _started = true;
            _price = bar.Close;
            _volume = bar.Volume;
            if (_defined)
            {
                output[0] = _index;
                output[1] = 1;
            }
        }
    }
}
