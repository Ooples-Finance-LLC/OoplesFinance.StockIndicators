using System.Numerics;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>A unary price transform with exact integer rounding or a real square root.</summary>
public enum PriceRoundingOperation
{
    Ceiling,
    Floor,
    SquareRoot,
}

/// <summary>Applies ceiling, floor, or square root independently to each close.</summary>
/// <remarks>Square root of a negative close is absent for that candle and recovers
/// immediately on a nonnegative close. Zero, including signed zero, is valid.
/// Every finite close is valid for ceiling and floor. No history is stored.</remarks>
public sealed class PriceRoundingTransform : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates the selected transform of close.</summary>
    public PriceRoundingTransform(PriceRoundingOperation operation)
        : base(2)
    {
        if (!Enum.IsDefined(operation.GetType(), operation))
            throw new ArgumentOutOfRangeException(nameof(operation));
        Operation = operation;
    }

    /// <summary>Unary operation applied to each close.</summary>
    public PriceRoundingOperation Operation { get; }

    /// <summary>Transformed close, or zero when a real root is undefined.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>One except for the square root of a negative close.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Operation);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 2)
            .Select(slot =>
                IndicatorValidationRule.Reference(slot, bars => Reference(bars)[slot], 0, 0)
            );

    private double[][] Reference(IReadOnlyList<Bar> bars)
    {
        var result = new[] { new double[bars.Count], new double[bars.Count] };
        for (var i = 0; i < bars.Count; i++)
        {
            var value = ReferenceFraction.FromDouble(bars[i].Close);
            if (Operation == PriceRoundingOperation.SquareRoot)
            {
                if (value.Sign < 0)
                    continue;
                result[0][i] = value.SqrtToDouble();
            }
            else
            {
                var (numerator, denominator) = value.Components;
                var integer = BigInteger.DivRem(numerator, denominator, out var remainder);
                if (Operation == PriceRoundingOperation.Ceiling && remainder.Sign > 0)
                    integer++;
                else if (Operation == PriceRoundingOperation.Floor && remainder.Sign < 0)
                    integer--;
                result[0][i] = new ReferenceFraction(integer).ToDouble();
            }
            result[1][i] = 1;
        }
        return result;
    }

    private sealed class State(PriceRoundingOperation operation) : IMultiOutputState
    {
        public void Reset() { }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            if (operation == PriceRoundingOperation.SquareRoot && bar.Close < 0)
                return;
            output[0] = operation switch
            {
                PriceRoundingOperation.Ceiling => Math.Ceiling(bar.Close),
                PriceRoundingOperation.Floor => Math.Floor(bar.Close),
                _ => Math.Sqrt(bar.Close),
            };
            output[1] = 1;
        }
    }
}
