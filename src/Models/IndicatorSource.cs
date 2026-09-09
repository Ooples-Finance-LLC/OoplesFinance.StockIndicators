//     Ooples Finance Stock Indicator Library
//     https://ooples.github.io/OoplesFinance.StockIndicators/
//
//     Copyright © Franklin Moormann, 2020-2022
//     cheatcountry@gmail.com
//
//     This library is free software and it uses the Apache 2.0 license
//     so if you are going to re-use or modify my code then I just ask
//     that you include my copyright info and my contact info in a comment

namespace OoplesFinance.StockIndicators.Models;

/// <summary>
/// The bars a calculation reads, together with the single series it treats as "the close".
/// </summary>
/// <remarks>
/// <para>
/// This exists to separate two things that <see cref="StockData.CustomValuesList"/> currently does
/// through one channel:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Between</b> calls it carries one indicator's output into the next as its input. That is
/// chaining, and it is a feature.
/// </description></item>
/// <item><description>
/// <b>Within</b> one call it is written by a moving average and then read back by a standard
/// deviation or an average true range on the same <see cref="StockData"/>, so the dispersion is
/// measured against the average rather than against price. That is a defect - see issue #145,
/// and the twenty-one hand-written guards of the form
/// <c>stockData.SetCustomValues(new List&lt;double&gt;())</c> that exist to prevent it.
/// </description></item>
/// </list>
/// <para>
/// An <see cref="IndicatorSource"/> is resolved once, when a calculation starts, and cannot be
/// reassigned afterwards. A calculation that takes its inputs from one of these cannot have those
/// inputs redefined half way through by something it called, so the defect above is not a rule to
/// remember - it is unrepresentable.
/// </para>
/// <para>
/// Chaining is unaffected: <see cref="Resolve"/> applies exactly the same rule
/// <c>GetInputValuesList</c> already applies, so a chained series still arrives as
/// <see cref="Values"/>. The difference is that it is read once rather than continuously.
/// </para>
/// </remarks>
public readonly struct IndicatorSource
{
    private readonly IReadOnlyList<double>? _values;
    private readonly IReadOnlyList<double>? _open;
    private readonly IReadOnlyList<double>? _high;
    private readonly IReadOnlyList<double>? _low;
    private readonly IReadOnlyList<double>? _volume;

    /// <summary>
    /// Creates a source from an explicit set of series.
    /// </summary>
    public IndicatorSource(IReadOnlyList<double> values, IReadOnlyList<double> open, IReadOnlyList<double> high,
        IReadOnlyList<double> low, IReadOnlyList<double> volume)
    {
        _values = values ?? throw new ArgumentNullException(nameof(values));
        _open = open ?? throw new ArgumentNullException(nameof(open));
        _high = high ?? throw new ArgumentNullException(nameof(high));
        _low = low ?? throw new ArgumentNullException(nameof(low));
        _volume = volume ?? throw new ArgumentNullException(nameof(volume));
    }

    private static readonly IReadOnlyList<double> EmptySeries = new List<double>();

    /// <summary>
    /// The series this calculation treats as its input - the close prices, or a chained series when
    /// the caller supplied one.
    /// </summary>
    public IReadOnlyList<double> Values => _values ?? EmptySeries;

    /// <summary>The open prices of the underlying bars.</summary>
    public IReadOnlyList<double> Open => _open ?? EmptySeries;

    /// <summary>The high prices of the underlying bars.</summary>
    public IReadOnlyList<double> High => _high ?? EmptySeries;

    /// <summary>The low prices of the underlying bars.</summary>
    public IReadOnlyList<double> Low => _low ?? EmptySeries;

    /// <summary>The volumes of the underlying bars.</summary>
    public IReadOnlyList<double> Volume => _volume ?? EmptySeries;

    /// <summary>The number of values in <see cref="Values"/>.</summary>
    public int Count => Values.Count;

    /// <summary>
    /// Resolves the input series for a calculation, once.
    /// </summary>
    /// <remarks>
    /// Applies the same precedence <c>GetInputValuesList</c> uses - a non-empty
    /// <see cref="StockData.CustomValuesList"/> wins, otherwise <see cref="StockData.InputValues"/> -
    /// so chained callers see no change in behaviour.
    /// </remarks>
    public static IndicatorSource Resolve(StockData stockData)
    {
        if (stockData is null)
        {
            throw new ArgumentNullException(nameof(stockData));
        }

        var custom = stockData.CustomValuesList;
        var values = custom is not null && custom.Count > 0 ? custom : stockData.InputValues;

        return new IndicatorSource(values, stockData.OpenPrices, stockData.HighPrices, stockData.LowPrices,
            stockData.Volumes);
    }

    /// <summary>
    /// Returns a source over the same bars but a different input series.
    /// </summary>
    /// <remarks>
    /// This is how one series is derived from another without writing to shared state. It is the
    /// explicit form of what chaining does implicitly today.
    /// </remarks>
    public IndicatorSource With(IReadOnlyList<double> values) =>
        new(values, Open, High, Low, Volume);
}
