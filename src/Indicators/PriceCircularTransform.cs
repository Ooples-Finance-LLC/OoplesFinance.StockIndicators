using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>A circular or inverse circular transform of close, in radians.</summary>
public enum PriceCircularOperation
{
    Sine,
    Cosine,
    Tangent,
    ArcSine,
    ArcCosine,
    ArcTangent,
}

/// <summary>Applies a circular function independently to each close.</summary>
/// <remarks>Asin/acos arguments outside [-1,1] are absent for that candle.
/// Other finite arguments are defined; nonfinite results are rejected by the runtime.
/// Validation uses independent high-precision range reduction and series, with
/// zero absolute tolerance, 4e-15 relative tolerance, and exact sign checks.</remarks>
public sealed class PriceCircularTransform : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates the selected radians transform.</summary>
    public PriceCircularTransform(PriceCircularOperation operation)
        : base(2)
    {
        if (!Enum.IsDefined(operation.GetType(), operation))
            throw new ArgumentOutOfRangeException(nameof(operation));
        Operation = operation;
    }

    /// <summary>Selected function.</summary>
    public PriceCircularOperation Operation { get; }

    /// <summary>Function value, or zero for an undefined argument.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>One when the real function is defined; otherwise zero.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Operation);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules
    {
        get
        {
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(
                0,
                bars =>
                    bars.Select(b =>
                            Defined(b.Close) ? CircularReference.Value(b.Close, Operation) : 0
                        )
                        .ToArray(),
                new IndicatorErrorBudget(0, 4e-15, true)
            );
            yield return IndicatorValidationRule.Reference(
                1,
                bars => bars.Select(b => Defined(b.Close) ? 1d : 0d).ToArray(),
                IndicatorErrorBudget.Exact
            );
        }
    }

    private bool Defined(double x) =>
        Operation is not (PriceCircularOperation.ArcSine or PriceCircularOperation.ArcCosine)
        || x is >= -1 and <= 1;

    private sealed class State(PriceCircularOperation operation) : IMultiOutputState
    {
        public void Reset() { }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            var x = bar.Close;
            if (
                operation is PriceCircularOperation.ArcSine or PriceCircularOperation.ArcCosine
                && (x < -1 || x > 1)
            )
                return;
            output[0] = operation switch
            {
#if NETFRAMEWORK
                PriceCircularOperation.Sine => FrameworkCircularMath.Value(x, 0),
                PriceCircularOperation.Cosine => FrameworkCircularMath.Value(x, 1),
                PriceCircularOperation.Tangent => FrameworkCircularMath.Value(x, 2),
#else
                PriceCircularOperation.Sine => Math.Sin(x),
                PriceCircularOperation.Cosine => Math.Cos(x),
                PriceCircularOperation.Tangent => Math.Tan(x),
#endif
                PriceCircularOperation.ArcSine => Math.Asin(x),
                PriceCircularOperation.ArcCosine => Math.Acos(x),
                _ => Math.Atan(x),
            };
            output[1] = 1;
        }
    }
}
