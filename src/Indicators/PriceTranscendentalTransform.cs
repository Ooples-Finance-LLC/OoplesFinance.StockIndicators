using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>A logarithmic, exponential, or hyperbolic transform of close.</summary>
public enum PriceTranscendentalOperation
{
    NaturalLogarithm,
    CommonLogarithm,
    Exponential,
    HyperbolicSine,
    HyperbolicCosine,
    HyperbolicTangent,
}

/// <summary>Applies a real transcendental function independently to each close.</summary>
/// <remarks>Nonpositive logarithm arguments are absent for that candle. All other
/// finite arguments are defined; results outside binary64 range are rejected by
/// the execution runtime. No history is retained. Validation uses independent
/// high precision series with a relative budget of 4e-15 and exact sign checks.</remarks>
public sealed class PriceTranscendentalTransform
    : MultiOutputIndicatorBase,
        IIndicatorValidationContract
{
    /// <summary>Creates the selected transform.</summary>
    public PriceTranscendentalTransform(PriceTranscendentalOperation operation)
        : base(2)
    {
        if (!Enum.IsDefined(operation.GetType(), operation))
            throw new ArgumentOutOfRangeException(nameof(operation));
        Operation = operation;
    }

    /// <summary>The selected function.</summary>
    public PriceTranscendentalOperation Operation { get; }

    /// <summary>Function value, or zero for an undefined logarithm.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>Zero for nonpositive logarithm arguments; otherwise one.</summary>
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
                            Defined(b.Close) ? TranscendentalReference.Value(b.Close, Operation) : 0
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
        Operation
            is not (
                PriceTranscendentalOperation.NaturalLogarithm
                or PriceTranscendentalOperation.CommonLogarithm
            )
        || x > 0;

    private sealed class State(PriceTranscendentalOperation operation) : IMultiOutputState
    {
        public void Reset() { }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            var x = bar.Close;
            if (
                operation
                    is PriceTranscendentalOperation.NaturalLogarithm
                        or PriceTranscendentalOperation.CommonLogarithm
                && x <= 0
            )
                return;
            output[0] = operation switch
            {
                PriceTranscendentalOperation.NaturalLogarithm => Math.Log(x),
                PriceTranscendentalOperation.CommonLogarithm => Math.Log10(x),
                PriceTranscendentalOperation.Exponential => Math.Exp(x),
                PriceTranscendentalOperation.HyperbolicSine => Math.Sinh(x),
                PriceTranscendentalOperation.HyperbolicCosine => Math.Cosh(x),
                _ => Math.Tanh(x),
            };
            output[1] = 1;
        }
    }
}
