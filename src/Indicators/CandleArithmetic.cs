using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>An elementary operation on two candle price fields.</summary>
public enum CandleArithmeticOperation
{
    Add,
    Subtract,
    Multiply,
    Divide,
}

/// <summary>A candle's untransformed price field.</summary>
public enum CandlePriceField
{
    Open,
    High,
    Low,
    Close,
}

/// <summary>Applies one arithmetic operation to two selected price fields per candle.</summary>
/// <remarks>The left operand defaults to close and the right operand to open.
/// Division by either signed zero is absent for that candle and recovers on the
/// next nonzero divisor. Each elementary operation rounds once to binary64;
/// an unrepresentable final result is rejected by the runtime. No history is stored.</remarks>
public sealed class CandleArithmetic : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates an arithmetic transform with independently selected operands.</summary>
    public CandleArithmetic(
        CandleArithmeticOperation operation,
        CandlePriceField left = CandlePriceField.Close,
        CandlePriceField right = CandlePriceField.Open
    )
        : base(2)
    {
        if (!Enum.IsDefined(operation))
            throw new ArgumentOutOfRangeException(nameof(operation));
        if (!Enum.IsDefined(left))
            throw new ArgumentOutOfRangeException(nameof(left));
        if (!Enum.IsDefined(right))
            throw new ArgumentOutOfRangeException(nameof(right));
        Operation = operation;
        Left = left;
        Right = right;
    }

    /// <summary>Operation applied in left-to-right order.</summary>
    public CandleArithmeticOperation Operation { get; }

    /// <summary>Left operand's field.</summary>
    public CandlePriceField Left { get; }

    /// <summary>Right operand's field.</summary>
    public CandlePriceField Right { get; }

    /// <summary>Arithmetic result, or zero for undefined division.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>One except when division has a zero divisor.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Operation, Left, Right);

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
        var result = new[] { new double[bars.Count], new double[bars.Count] };
        for (var i = 0; i < bars.Count; i++)
        {
            var a = ReferenceFraction.FromDouble(Select(bars[i], Left));
            var b = ReferenceFraction.FromDouble(Select(bars[i], Right));
            if (Operation == CandleArithmeticOperation.Divide && b.Sign == 0)
                continue;
            result[0][i] = (
                Operation switch
                {
                    CandleArithmeticOperation.Add => a + b,
                    CandleArithmeticOperation.Subtract => a - b,
                    CandleArithmeticOperation.Multiply => a * b,
                    _ => a / b,
                }
            ).ToDouble();
            result[1][i] = 1;
        }
        return result;
    }

    private static double Select(in Bar bar, CandlePriceField field) =>
        field switch
        {
            CandlePriceField.Open => bar.Open,
            CandlePriceField.High => bar.High,
            CandlePriceField.Low => bar.Low,
            _ => bar.Close,
        };

    private sealed class State(
        CandleArithmeticOperation operation,
        CandlePriceField left,
        CandlePriceField right
    ) : IMultiOutputState
    {
        public void Reset() { }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            var a = Select(bar, left);
            var b = Select(bar, right);
            if (operation == CandleArithmeticOperation.Divide && Math.Abs(b) <= 0)
                return;
            output[0] = operation switch
            {
                CandleArithmeticOperation.Add => a + b,
                CandleArithmeticOperation.Subtract => a - b,
                CandleArithmeticOperation.Multiply => a * b,
                _ => a / b,
            };
            output[1] = 1;
        }
    }
}
