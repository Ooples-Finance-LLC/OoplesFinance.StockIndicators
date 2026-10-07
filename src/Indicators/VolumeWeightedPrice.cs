using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Volume-weighted close or typical price over a rolling window or cumulative prefix.</summary>
/// <remarks>A null period selects cumulative history; a positive period requires a full
/// window. Only candles on or after StartDate participate. An anchor before the first
/// candle includes all history; an anchor after the last candle leaves all readings absent.
/// Zero total volume is absent. Typical price is (high+low+close)/3 without intermediate
/// rounding, and the weighted ratio is rounded once. Histories grow lazily; cumulative
/// mode stores no price history. Unrepresentable final outputs are rejected by the runtime.</remarks>
public sealed class VolumeWeightedPrice
    : MultiOutputIndicatorBase,
        IVolumeIndicator,
        IIndicatorValidationContract
{
    /// <summary>Creates a weighted typical price, or weighted close when typicalPrice is false.</summary>
    public VolumeWeightedPrice(
        int? period = null,
        bool typicalPrice = true,
        DateTime? startDate = null
    )
        : base(2)
    {
        if (period is <= 0)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
        TypicalPrice = typicalPrice;
        StartDate = startDate;
    }

    /// <summary>Positive full-window length, or null for cumulative history.</summary>
    public int? Period { get; }

    /// <summary>Whether to weight the exact typical price instead of close.</summary>
    public bool TypicalPrice { get; }

    /// <summary>Optional inclusive time cutoff for participating candles.</summary>
    public DateTime? StartDate { get; }

    /// <summary>Weighted price, or zero while absent.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>One when the window is ready and its exact total volume is nonzero.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];

    /// <inheritdoc/>
    public override IIndicatorOutput PrimaryOutput => Value;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period, TypicalPrice, StartDate);

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
        var weighted = new List<ReferenceFraction> { new(0) };
        var mass = new List<ReferenceFraction> { new(0) };
        for (var i = 0; i < bars.Count; i++)
        {
            if (StartDate is DateTime anchor && bars[i].Time < anchor)
                continue;
            var bar = bars[i];
            var price = TypicalPrice
                ? (R(bar.High) + R(bar.Low) + R(bar.Close)) / new ReferenceFraction(3)
                : R(bar.Close);
            weighted.Add(weighted[^1] + price * R(bar.Volume));
            mass.Add(mass[^1] + R(bar.Volume));
            var count = mass.Count - 1;
            if (Period is int required && count < required)
                continue;
            var first = Period is int length ? count - length : 0;
            var numerator = weighted[count] - weighted[first];
            var denominator = mass[count] - mass[first];
            if (denominator.Sign == 0)
                continue;
            output[0][i] = (numerator / denominator).ToDouble();
            output[1][i] = 1;
        }
        return output;
    }

    private static ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);

    private sealed class State(int? period, bool typical, DateTime? start) : IMultiOutputState
    {
        private readonly Queue<Bar> _window = new();
        private ExactMeanAccumulator _weighted,
            _mass;

        public void Reset()
        {
            _window.Clear();
            _weighted = _mass = default;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            if (start is DateTime anchor && bar.Time < anchor)
                return;
            if (period is int length)
            {
                if (_window.Count == length)
                    Add(_window.Dequeue(), -1);
                _window.Enqueue(bar);
            }
            Add(bar, 1);
            if (period is int required && _window.Count < required || _mass.IsExactlyZero)
                return;
            output[0] = _weighted.Ratio(_mass);
            output[1] = 1;
        }

        private void Add(in Bar bar, int sign)
        {
            _weighted.AddProduct(bar.Close, bar.Volume, sign);
            if (typical)
            {
                _weighted.AddProduct(bar.High, bar.Volume, sign);
                _weighted.AddProduct(bar.Low, bar.Volume, sign);
            }
            _mass.Add(bar.Volume, typical ? 3 * sign : sign);
        }
    }
}
