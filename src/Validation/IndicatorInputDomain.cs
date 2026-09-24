using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

[Flags]
public enum IndicatorInputFields
{
    None = 0, Open = 1, High = 2, Low = 4, Close = 8, Volume = 16, Prices = 15, All = 31
}

/// <summary>Declared field domains. These checks do not certify vendor data or impose OHLC ordering on derived series.</summary>
public sealed class IndicatorInputDomain
{
    private readonly IReadOnlyList<IndicatorFieldRange> _ranges = Array.Empty<IndicatorFieldRange>();
    public IndicatorInputDomain(IndicatorInputFields finiteFields = IndicatorInputFields.All,
        IndicatorInputFields strictlyPositiveFields = IndicatorInputFields.None,
        IndicatorInputFields nonnegativeFields = IndicatorInputFields.None)
    {
        if (((finiteFields | strictlyPositiveFields | nonnegativeFields) & ~IndicatorInputFields.All) != 0)
            throw new ArgumentOutOfRangeException(nameof(finiteFields));
        FiniteFields = finiteFields | strictlyPositiveFields | nonnegativeFields;
        StrictlyPositiveFields = strictlyPositiveFields; NonnegativeFields = nonnegativeFields;
    }
    public IndicatorInputFields FiniteFields { get; }
    public IndicatorInputFields StrictlyPositiveFields { get; }
    public IndicatorInputFields NonnegativeFields { get; }
    /// <summary>Additional inclusive field bounds; an empty list imposes no magnitude bounds.</summary>
    public IReadOnlyList<IndicatorFieldRange> Ranges => _ranges;

    private IndicatorInputDomain(IndicatorInputDomain parent, IndicatorInputFields fields, double minimum, double maximum)
        : this(parent.FiniteFields | fields, parent.StrictlyPositiveFields, parent.NonnegativeFields)
    {
        var ranges = parent.Ranges.ToDictionary(range => range.Field);
        for (var bit = 1; bit <= 16; bit <<= 1)
        {
            var field = (IndicatorInputFields)bit;
            if ((fields & field) == 0) continue;
            var lower = minimum;
            var upper = maximum;
            if (ranges.TryGetValue(field, out var previous))
            {
                lower = Math.Max(lower, previous.Minimum);
                upper = Math.Min(upper, previous.Maximum);
            }
            if (lower > upper || ((StrictlyPositiveFields & field) != 0 && upper <= 0)
                || ((NonnegativeFields & field) != 0 && upper < 0))
                throw new ArgumentException("The field constraints have no admissible value.", nameof(minimum));
            ranges[field] = new IndicatorFieldRange(field, lower, upper);
        }
        _ranges = Array.AsReadOnly(ranges.Values.OrderBy(range => range.Field).ToArray());
    }

    /// <summary>Returns a new domain intersected with inclusive finite bounds on the selected fields.
    /// Existing domains, including the shared defaults, are never modified.</summary>
    public IndicatorInputDomain WithRange(IndicatorInputFields fields, double minimum, double maximum)
    {
        if (fields == IndicatorInputFields.None || (fields & ~IndicatorInputFields.All) != 0)
            throw new ArgumentOutOfRangeException(nameof(fields));
        if (double.IsNaN(minimum) || double.IsInfinity(minimum)
            || double.IsNaN(maximum) || double.IsInfinity(maximum) || minimum > maximum)
            throw new ArgumentOutOfRangeException(nameof(minimum), "Bounds must be finite and ordered.");
        return new IndicatorInputDomain(this, fields, minimum, maximum);
    }
    public static IndicatorInputDomain Finite { get; } = new();
    public static IndicatorInputDomain PositiveClose { get; } = new(strictlyPositiveFields: IndicatorInputFields.Close);
    /// <summary>Returns the runtime domain used by the typed builder, including built-in requirements.</summary>
    public static IndicatorInputDomain For(IIndicator indicator)
    {
        if (indicator is null) throw new ArgumentNullException(nameof(indicator));
        if (indicator is IIndicatorInputDomainContract contract)
            return contract.InputDomain ?? throw new InvalidOperationException("An input domain cannot be null.");
        // ASI accumulates relative-price gains/losses into nonnegative probability masses.
        // Signed prices make those masses negative and can create a singular denominator.
        return indicator is IBuiltInIndicator builtIn && builtIn.BatchName == IndicatorName.AbsoluteStrengthIndex
            ? PositiveClose : Finite;
    }

    public string? Violation(in Bar bar)
    {
        var hasRanges = _ranges.Count != 0;
        for (var bit = 1; bit <= 16; bit <<= 1)
        {
            var field = (IndicatorInputFields)bit;
            var value = field switch { IndicatorInputFields.Open => bar.Open, IndicatorInputFields.High => bar.High,
                IndicatorInputFields.Low => bar.Low, IndicatorInputFields.Close => bar.Close, _ => bar.Volume };
            if ((FiniteFields & field) != 0 && (double.IsNaN(value) || double.IsInfinity(value))) return field + " must be finite.";
            if ((StrictlyPositiveFields & field) != 0 && value <= 0) return field + " must be strictly positive.";
            if ((NonnegativeFields & field) != 0 && value < 0) return field + " must be nonnegative.";
            if (hasRanges)
                foreach (var range in _ranges)
                    if (range.Field == field && (value < range.Minimum || value > range.Maximum))
                        return field + " must be in [" + range.Minimum.ToString("R") + ", " + range.Maximum.ToString("R") + "].";
        }
        return null;
    }

    public void Validate(in Bar bar)
    {
        var violation = Violation(bar);
        if (violation is not null) throw new ArgumentOutOfRangeException(nameof(bar), violation);
    }

    internal IEnumerable<Bar> InvalidExamples()
    {
        var valid = ValidExample(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        for (var bit = 1; bit <= 16; bit <<= 1)
        {
            var field = (IndicatorInputFields)bit;
            var invalid = new List<double>();
            if ((FiniteFields & field) != 0) invalid.AddRange(new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity });
            if ((StrictlyPositiveFields & field) != 0) invalid.Add(0);
            if (((StrictlyPositiveFields | NonnegativeFields) & field) != 0) invalid.Add(-1);
            foreach (var range in _ranges)
                if (range.Field == field)
                {
                    invalid.Add(Adjacent(range.Minimum, -1));
                    invalid.Add(Adjacent(range.Maximum, 1));
                }
            foreach (var value in invalid)
                yield return new Bar(valid.Time,
                    bit == 1 ? value : valid.Open, bit == 2 ? value : valid.High, bit == 4 ? value : valid.Low,
                    bit == 8 ? value : valid.Close, bit == 16 ? value : valid.Volume);
        }
    }

    internal Bar ValidExample(DateTime time) => Example(time, field => field == IndicatorInputFields.Volume ? 1000 : 100);

    internal Bar PreviewExample(Bar bar, double change)
    {
        double Changed(IndicatorInputFields field, double value)
        {
            var proposed = value + change;
            foreach (var range in _ranges)
                if (range.Field == field && (proposed < range.Minimum || proposed > range.Maximum))
                    proposed = value == range.Maximum ? range.Minimum : range.Maximum;
            return proposed;
        }
        var candidate = new Bar(bar.Time, bar.Open, Changed(IndicatorInputFields.High, bar.High), bar.Low,
            Changed(IndicatorInputFields.Close, bar.Close), bar.Volume);
        // A singleton/positive intersection may have no distinct admissible preview value.
        return Violation(candidate) is null ? candidate : ValidExample(bar.Time);
    }

    internal IEnumerable<(string Name, IReadOnlyList<Bar> Bars)> BoundaryFixtures(int count, string prefix = "declared-domain")
    {
        if (_ranges.Count == 0) yield break;
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var bars = new Bar[count];
        for (var i = 0; i < count; i++)
        {
            var index = i;
            bars[i] = Example(start.AddMinutes(i), field =>
            {
                var range = _ranges.FirstOrDefault(r => r.Field == field);
                if (range is null) return field == IndicatorInputFields.Volume ? 1000 : 100;
                return (index + (int)field) % 2 == 0 ? range.Minimum : range.Maximum;
            });
        }
        yield return (prefix + "/alternating-boundaries", Array.AsReadOnly(bars));
    }

    private Bar Example(DateTime time, Func<IndicatorInputFields, double> preferred)
    {
        double Value(IndicatorInputFields field)
        {
            var value = preferred(field);
            foreach (var range in _ranges)
                if (range.Field == field) value = Math.Max(range.Minimum, Math.Min(range.Maximum, value));
            if ((StrictlyPositiveFields & field) != 0 && value <= 0) value = double.Epsilon;
            if ((NonnegativeFields & field) != 0 && value < 0) value = 0;
            return value;
        }
        return new Bar(time, Value(IndicatorInputFields.Open), Value(IndicatorInputFields.High),
            Value(IndicatorInputFields.Low), Value(IndicatorInputFields.Close), Value(IndicatorInputFields.Volume));
    }

    private static double Adjacent(double value, int direction)
        => value == 0 ? direction * double.Epsilon
            : BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(value) + (value > 0 ? direction : -direction));
}

/// <summary>An inclusive magnitude constraint on one numeric bar field.</summary>
public sealed class IndicatorFieldRange
{
    internal IndicatorFieldRange(IndicatorInputFields field, double minimum, double maximum)
    { Field = field; Minimum = minimum; Maximum = maximum; }
    public IndicatorInputFields Field { get; }
    public double Minimum { get; }
    public double Maximum { get; }
}

/// <summary>Optional declarative input domains for generated paired-series validation, including rejection/recovery checks.</summary>
public interface IMultiSeriesInputDomainContract
{
    IndicatorInputDomain PrimaryInputDomain { get; }
    IndicatorInputDomain BenchmarkInputDomain { get; }
}

/// <summary>Runtime input requirements for a built-in or customer indicator. Chained
/// indicators validate the derived close they actually receive.</summary>
public interface IIndicatorInputDomainContract
{
    IndicatorInputDomain InputDomain { get; }
}
