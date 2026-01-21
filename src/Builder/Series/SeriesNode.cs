using OoplesFinance.StockIndicators.Builder.Specs;

namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Represents a node in the series computation graph.
/// </summary>
public sealed class SeriesNode
{
    private SeriesNode(
        SeriesNodeKind kind,
        SeriesKey seriesKey,
        SeriesHandle? input,
        IndicatorSpec? spec,
        SeriesHandle? left,
        SeriesHandle? right,
        Func<double, double, double>? formula)
    {
        Kind = kind;
        SeriesKey = seriesKey;
        Input = input;
        Spec = spec;
        Left = left;
        Right = right;
        Formula = formula;
    }

    /// <summary>
    /// Gets the node kind.
    /// </summary>
    public SeriesNodeKind Kind { get; }

    /// <summary>
    /// Gets the series key (symbol + timeframe).
    /// </summary>
    public SeriesKey SeriesKey { get; }

    /// <summary>
    /// Gets the input series handle for indicator nodes.
    /// </summary>
    public SeriesHandle? Input { get; }

    /// <summary>
    /// Gets the indicator specification for indicator nodes.
    /// </summary>
    public IndicatorSpec? Spec { get; }

    /// <summary>
    /// Gets the left operand for formula nodes.
    /// </summary>
    public SeriesHandle? Left { get; }

    /// <summary>
    /// Gets the right operand for formula nodes.
    /// </summary>
    public SeriesHandle? Right { get; }

    /// <summary>
    /// Gets the formula function for formula nodes.
    /// </summary>
    public Func<double, double, double>? Formula { get; }

    /// <summary>
    /// Creates a base series node.
    /// </summary>
    public static SeriesNode Base(SeriesKey key)
    {
        return new SeriesNode(SeriesNodeKind.Base, key, null, null, null, null, null);
    }

    /// <summary>
    /// Creates an indicator series node.
    /// </summary>
    public static SeriesNode Indicator(SeriesKey key, SeriesHandle input, IndicatorSpec spec)
    {
        return new SeriesNode(SeriesNodeKind.Indicator, key, input, spec, null, null, null);
    }

    /// <summary>
    /// Creates a formula series node.
    /// </summary>
    public static SeriesNode CreateFormula(SeriesKey key, SeriesHandle left, SeriesHandle right, Func<double, double, double> formula)
    {
        return new SeriesNode(SeriesNodeKind.Formula, key, null, null, left, right, formula);
    }
}
