using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>A dated set of optionally present Alligator lines.</summary>
public readonly record struct AlligatorSample(
    DateTime Time,
    double? Jaw,
    double? Teeth,
    double? Lips
);

/// <summary>Gator distances and expansion decisions with explicit nullable presence.</summary>
public readonly record struct GatorSample(
    DateTime Time,
    double? Upper,
    double? Lower,
    bool? UpperIsExpanding,
    bool? LowerIsExpanding
);

/// <summary>Absolute Alligator jaw/teeth distance, negative teeth/lips distance and expansion flags.</summary>
/// <remarks>Expansion requires a present previous distance. A missing current distance
/// with a present previous distance reports false. Ties do not expand. Chaining feeds
/// upstream prices to Alligator. Unrepresentable final distances are rejected.</remarks>
public sealed class GatorWithDetails : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    private readonly AlligatorWithDetails _alligator;

    /// <summary>Creates Gator from configurable Alligator periods and positive delays.</summary>
    public GatorWithDetails(
        int jawPeriod = 13,
        int jawOffset = 8,
        int teethPeriod = 8,
        int teethOffset = 5,
        int lipsPeriod = 5,
        int lipsOffset = 3
    )
        : base(8)
    {
        _alligator = new(jawPeriod, jawOffset, teethPeriod, teethOffset, lipsPeriod, lipsOffset);
    }

    /// <summary>Upper distance.</summary>
    public IIndicatorOutput Upper => Outputs[0];

    /// <summary>Negative lower distance.</summary>
    public IIndicatorOutput Lower => Outputs[1];

    /// <summary>One when upper distance strictly expands; otherwise zero.</summary>
    public IIndicatorOutput UpperIsExpanding => Outputs[2];

    /// <summary>One when lower absolute distance strictly expands; otherwise zero.</summary>
    public IIndicatorOutput LowerIsExpanding => Outputs[3];

    /// <summary>Upper distance presence.</summary>
    public IIndicatorOutput IsUpperDefined => Outputs[4];

    /// <summary>Lower distance presence.</summary>
    public IIndicatorOutput IsLowerDefined => Outputs[5];

    /// <summary>Upper expansion decision presence.</summary>
    public IIndicatorOutput IsUpperExpansionDefined => Outputs[6];

    /// <summary>Lower expansion decision presence.</summary>
    public IIndicatorOutput IsLowerExpansionDefined => Outputs[7];

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new State(_alligator.CreateState(Source is not null));

    /// <summary>Transforms supplied Alligator lines in enumeration order, retaining timestamps and missing values.</summary>
    /// <remarks>Rejects nonfinite supplied lines and unrepresentable distances. Each enumeration starts fresh.</remarks>
    public static IEnumerable<GatorSample> FromLines(IEnumerable<AlligatorSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        return Enumerate();
        IEnumerable<GatorSample> Enumerate()
        {
            var transform = new Transform();
            foreach (var row in samples)
            {
                foreach (var value in new[] { row.Jaw, row.Teeth, row.Lips })
                    if (value.HasValue && !double.IsFinite(value.Value))
                        throw new ArgumentOutOfRangeException(nameof(samples));
                var result = transform.Next(row);
                if (
                    result.Upper is { } upper && !double.IsFinite(upper)
                    || result.Lower is { } lower && !double.IsFinite(lower)
                )
                    throw new ArithmeticException("Gator distance is not representable.");
                yield return result;
            }
        }
    }

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 8)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars => Reference(bars)[slot],
                    IndicatorErrorBudget.Exact
                )
            );

    private double[][] Reference(IReadOnlyList<Bar> bars)
    {
        var lines = _alligator.Reference(bars, Source is not null);
        var result = Enumerable.Range(0, 8).Select(_ => new double[bars.Count]).ToArray();
        for (var i = 0; i < bars.Count; i++)
        {
            for (var slot = 0; slot < 2; slot++)
            {
                if (lines[slot + 3][i] > 0 && lines[slot + 4][i] > 0)
                {
                    var difference = (
                        ReferenceFraction.FromDouble(lines[slot][i])
                        - ReferenceFraction.FromDouble(lines[slot + 1][i])
                    ).ToDouble();
                    result[slot][i] = (slot == 0 ? 1 : -1) * Math.Abs(difference);
                    result[slot + 4][i] = 1;
                }
                if (i > 0 && result[slot + 4][i - 1] > 0)
                {
                    result[slot + 6][i] = 1;
                    result[slot + 2][i] =
                        result[slot + 4][i] > 0
                        && (
                            slot == 0
                                ? result[slot][i] > result[slot][i - 1]
                                : result[slot][i] < result[slot][i - 1]
                        )
                            ? 1
                            : 0;
                }
            }
        }
        return result;
    }

    private sealed class Transform
    {
        private double? _upper,
            _lower;

        internal void Reset()
        {
            _upper = null;
            _lower = null;
        }

        internal GatorSample Next(AlligatorSample row)
        {
            static double? Distance(double? a, double? b, int sign)
            {
                if (!a.HasValue || !b.HasValue)
                    return null;
                var difference = new ExactMeanAccumulator();
                difference.Add(a.Value);
                difference.Add(b.Value, -1);
                return sign * Math.Abs(difference.Mean(1));
            }
            var upper = Distance(row.Jaw, row.Teeth, 1);
            var lower = Distance(row.Teeth, row.Lips, -1);
            var result = new GatorSample(
                row.Time,
                upper,
                lower,
                _upper.HasValue ? upper > _upper : null,
                _lower.HasValue ? lower < _lower : null
            );
            _upper = upper;
            _lower = lower;
            return result;
        }
    }

    private sealed class State(IMultiOutputState alligator) : IMultiOutputState
    {
        private readonly Transform _transform = new();

        public void Reset()
        {
            alligator.Reset();
            _transform.Reset();
        }

        public void Update(in Bar bar, Span<double> output)
        {
            Span<double> lines = stackalloc double[6];
            alligator.Update(bar, lines);
            var row = _transform.Next(
                new(
                    bar.Time,
                    lines[3] > 0 ? lines[0] : null,
                    lines[4] > 0 ? lines[1] : null,
                    lines[5] > 0 ? lines[2] : null
                )
            );
            output[0] = row.Upper ?? 0;
            output[1] = row.Lower ?? 0;
            output[2] = row.UpperIsExpanding == true ? 1 : 0;
            output[3] = row.LowerIsExpanding == true ? 1 : 0;
            output[4] = row.Upper.HasValue ? 1 : 0;
            output[5] = row.Lower.HasValue ? 1 : 0;
            output[6] = row.UpperIsExpanding.HasValue ? 1 : 0;
            output[7] = row.LowerIsExpanding.HasValue ? 1 : 0;
        }
    }
}
