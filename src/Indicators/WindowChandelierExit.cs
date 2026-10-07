using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Which Chandelier Exit levels to publish.</summary>
public enum ChandelierExitSelection
{
    /// <summary>High minus ATR times multiplier.</summary>
    Long,

    /// <summary>Low plus ATR times multiplier.</summary>
    Short,

    /// <summary>Both levels.</summary>
    Both,
}

/// <summary>Rolling high/low exits offset by mean-seeded Wilder ATR.</summary>
/// <remarks>Output starts at index period. ATR stages retain extended upper
/// exponents; each final level rounds once from the complete offset expression.
/// The optional zero floor on the rolling high reproduces Skender's long-side
/// convention for negative prices. Only selected outputs are evaluated. Histories
/// grow lazily; any finite multiplier, including zero and negative values, is supported.</remarks>
public sealed class WindowChandelierExit : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates Chandelier levels with a positive period and finite multiplier.</summary>
    public WindowChandelierExit(
        int period = 22,
        double multiplier = 3,
        ChandelierExitSelection selection = ChandelierExitSelection.Both,
        bool zeroFloorHigh = false
    )
        : base(4)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (!FrameworkCompatibility.IsFinite(multiplier))
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        if (selection < ChandelierExitSelection.Long || selection > ChandelierExitSelection.Both)
            throw new ArgumentOutOfRangeException(nameof(selection));
        Period = period;
        Multiplier = multiplier;
        Selection = selection;
        ZeroFloorHigh = zeroFloorHigh;
    }

    /// <summary>Window and ATR period.</summary>
    public int Period { get; }

    /// <summary>ATR offset multiplier.</summary>
    public double Multiplier { get; }

    /// <summary>Selected levels.</summary>
    public ChandelierExitSelection Selection { get; }

    /// <summary>Whether the rolling high is floored at zero.</summary>
    public bool ZeroFloorHigh { get; }

    /// <summary>Long exit, or zero when unselected or before startup.</summary>
    public IIndicatorOutput LongExit => Outputs[0];

    /// <summary>Short exit, or zero when unselected or before startup.</summary>
    public IIndicatorOutput ShortExit => Outputs[1];

    /// <summary>One when LongExit is selected and mature.</summary>
    public IIndicatorOutput LongIsDefined => Outputs[2];

    /// <summary>One when ShortExit is selected and mature.</summary>
    public IIndicatorOutput ShortIsDefined => Outputs[3];

    /// <inheritdoc/>
    public override IIndicatorOutput PrimaryOutput =>
        Selection == ChandelierExitSelection.Short ? ShortExit : LongExit;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new State(Period, Multiplier, Selection, ZeroFloorHigh);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 4)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        ChandelierReference.Calculate(
                            bars,
                            Period,
                            Multiplier,
                            Selection,
                            ZeroFloorHigh
                        )[slot],
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(
        int period,
        double multiplier,
        ChandelierExitSelection selection,
        bool zeroFloorHigh
    ) : IMultiOutputState
    {
        private readonly SeededAtrWindow _atr = new(period);
        private readonly Queue<(double High, double Low)> _window = new();

        public void Reset()
        {
            _atr.Reset();
            _window.Clear();
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            _atr.Add(bar);
            if (_window.Count == period)
                _window.Dequeue();
            _window.Enqueue((bar.High, bar.Low));
            if (!_atr.HasAverage)
                return;
            var product = new ExactMeanAccumulator();
            product.AddProduct(_atr.Average.Mantissa, multiplier);
            product.ScaleByPowerOfTwo(_atr.Average.UpperShift);
            if (selection != ChandelierExitSelection.Short)
            {
                var high = _window.Max(v => v.High);
                var sum = new ExactMeanAccumulator();
                sum.Add(zeroFloorHigh ? Math.Max(0, high) : high);
                sum.Subtract(product);
                output[0] = sum.Mean(1);
                output[2] = 1;
            }
            if (selection != ChandelierExitSelection.Long)
            {
                var sum = product;
                sum.Add(_window.Min(v => v.Low));
                output[1] = sum.Mean(1);
                output[3] = 1;
            }
        }
    }
}
