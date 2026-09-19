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

[Serializable]
public class StockData : IStockData
{
    private List<double>? _inputValues;
    private List<double> _customValues = new List<double>();
    private Dictionary<string, List<double>> _outputValues = new Dictionary<string, List<double>>();
    private List<double>? _openPrices;
    private List<double>? _highPrices;
    private List<double>? _lowPrices;
    private List<double>? _closePrices;
    private List<double>? _volumes;
    private List<DateTime>? _dates;
    private List<TickerData>? _tickerDataList;
    private bool _columnsInitialized;
    private bool _rowsInitialized;

    // The columns as the caller handed them over, when they were handed over rather than copied in. Every
    // List<double> above is then built only if something asks for one, which the compute layer never does: it
    // reads the spans below. See the adopting constructor for why that matters.
    private ReadOnlyMemory<double>? _openMemory;
    private ReadOnlyMemory<double>? _highMemory;
    private ReadOnlyMemory<double>? _lowMemory;
    private ReadOnlyMemory<double>? _closeMemory;
    private ReadOnlyMemory<double>? _volumeMemory;
    private ReadOnlyMemory<DateTime>? _dateMemory;

    public IndicatorName IndicatorName { get; set; }

    public List<double> InputValues
    {
        get
        {
            if (_inputValues == null)
            {
                // Straight from the adopted column when there is one. Going through ClosePrices would build that
                // column's list as well, so the closes would be copied twice to answer for them once.
                _inputValues = _closeMemory.HasValue
                    ? Materialize(_closeMemory.Value.Span)
                    : new List<double>(ClosePrices);
            }

            return _inputValues;
        }
        set => _inputValues = value ?? new List<double>();
    }


    public List<double> OpenPrices
    {
        get
        {
            if (_openPrices is null && _openMemory.HasValue)
            {
                _openPrices = Materialize(_openMemory.Value.Span);
            }

            EnsureColumns();
            return _openPrices!;
        }
        set
        {
            _openPrices = value ?? new List<double>();
            // The caller has replaced this column, so the view it was handed over as no longer describes it.
            _openMemory = null;
            _columnsInitialized = true;
        }
    }

    public List<double> HighPrices
    {
        get
        {
            if (_highPrices is null && _highMemory.HasValue)
            {
                _highPrices = Materialize(_highMemory.Value.Span);
            }

            EnsureColumns();
            return _highPrices!;
        }
        set
        {
            _highPrices = value ?? new List<double>();
            // The caller has replaced this column, so the view it was handed over as no longer describes it.
            _highMemory = null;
            _columnsInitialized = true;
        }
    }

    public List<double> LowPrices
    {
        get
        {
            if (_lowPrices is null && _lowMemory.HasValue)
            {
                _lowPrices = Materialize(_lowMemory.Value.Span);
            }

            EnsureColumns();
            return _lowPrices!;
        }
        set
        {
            _lowPrices = value ?? new List<double>();
            // The caller has replaced this column, so the view it was handed over as no longer describes it.
            _lowMemory = null;
            _columnsInitialized = true;
        }
    }

    public List<double> ClosePrices
    {
        get
        {
            if (_closePrices is null && _closeMemory.HasValue)
            {
                _closePrices = Materialize(_closeMemory.Value.Span);
            }

            EnsureColumns();
            return _closePrices!;
        }
        set
        {
            _closePrices = value ?? new List<double>();
            // The caller has replaced this column, so the view it was handed over as no longer describes it.
            _closeMemory = null;
            _columnsInitialized = true;
        }
    }

    public List<double> Volumes
    {
        get
        {
            if (_volumes is null && _volumeMemory.HasValue)
            {
                _volumes = Materialize(_volumeMemory.Value.Span);
            }

            EnsureColumns();
            return _volumes!;
        }
        set
        {
            _volumes = value ?? new List<double>();
            // The caller has replaced this column, so the view it was handed over as no longer describes it.
            _volumeMemory = null;
            _columnsInitialized = true;
        }
    }

    public List<DateTime> Dates
    {
        get
        {
            if (_dates is null && _dateMemory.HasValue)
            {
                _dates = Materialize(_dateMemory.Value.Span);
            }

            EnsureColumns();
            return _dates!;
        }
        set
        {
            _dates = value ?? new List<DateTime>();
            // The caller has replaced this column, so the view it was handed over as no longer describes it.
            _dateMemory = null;
            _columnsInitialized = true;
        }
    }

    public List<TickerData> TickerDataList
    {
        get
        {
            EnsureRows();
            return _tickerDataList!;
        }
        set
        {
            _tickerDataList = value ?? new List<TickerData>();
            _rowsInitialized = true;
        }
    }

    public List<double> CustomValuesList
    {
        get => _customValues;
        set
        {
            _customValues = value ?? new List<double>();
            ChainedValues = _customValues;
        }
    }

    /// <summary>
    /// The series the next calculation on this data reads: the last one published, or the caller's input.
    /// </summary>
    /// <remarks>
    /// The same list as <see cref="CustomValuesList"/> unless IncludeCustomValues is off. That option hides a
    /// result from the caller; it must not hide it from the composite that asked a component for it, or from
    /// the next indicator in a chain. Hiding the one list both read made 176 indicators throw and three
    /// compute on the close instead of their own components.
    /// </remarks>
    internal List<double> ChainedValues { get; private set; } = new List<double>();

    /// <summary>
    /// The series the compute layer reads, as a span over whatever is backing it.
    /// </summary>
    /// <remarks>
    /// This is the one accessor the fast arms use. It resolves to the chained series when a chain has
    /// published one, to the caller's input when one was set, and otherwise to the close column - without
    /// building a <see cref="List{T}"/> for any of them when the columns were handed over rather than copied
    /// in. Reading <see cref="InputValues"/> instead would materialise a copy of the closes on first touch,
    /// which is the copy this whole path exists to avoid.
    /// </remarks>
    internal ReadOnlySpan<double> ChainedSpanOrInput =>
        ChainedValues.Count > 0 ? Compatibility.SpanCompat.AsReadOnlySpan(ChainedValues) : InputSpan;

    /// <summary>The caller's input series, or the closes when none was set, without copying either.</summary>
    internal ReadOnlySpan<double> InputSpan
    {
        get
        {
            if (_inputValues is not null)
            {
                return Compatibility.SpanCompat.AsReadOnlySpan(_inputValues);
            }

            return _closeMemory.HasValue
                ? _closeMemory.Value.Span
                : Compatibility.SpanCompat.AsReadOnlySpan(ClosePrices);
        }
    }

    /// <summary>The open column, without copying it.</summary>
    internal ReadOnlySpan<double> OpenSpan => _openMemory.HasValue
        ? _openMemory.Value.Span
        : Compatibility.SpanCompat.AsReadOnlySpan(OpenPrices);

    /// <summary>The high column, without copying it.</summary>
    internal ReadOnlySpan<double> HighSpan => _highMemory.HasValue
        ? _highMemory.Value.Span
        : Compatibility.SpanCompat.AsReadOnlySpan(HighPrices);

    /// <summary>The low column, without copying it.</summary>
    internal ReadOnlySpan<double> LowSpan => _lowMemory.HasValue
        ? _lowMemory.Value.Span
        : Compatibility.SpanCompat.AsReadOnlySpan(LowPrices);

    /// <summary>The close column, without copying it.</summary>
    internal ReadOnlySpan<double> CloseSpan => _closeMemory.HasValue
        ? _closeMemory.Value.Span
        : Compatibility.SpanCompat.AsReadOnlySpan(ClosePrices);

    /// <summary>The volume column, without copying it.</summary>
    internal ReadOnlySpan<double> VolumeSpan => _volumeMemory.HasValue
        ? _volumeMemory.Value.Span
        : Compatibility.SpanCompat.AsReadOnlySpan(Volumes);

    /// <summary>Puts back a published list and a chained series saved by a caller that borrowed both.</summary>
    internal void RestoreSeries(List<double> published, List<double> chained)
    {
        _customValues = published;
        ChainedValues = chained;
    }

    public Dictionary<string, List<double>> OutputValues
    {
        get => _outputValues;
        set
        {
            _outputValues = value ?? new Dictionary<string, List<double>>();
            ChainedOutputs = _outputValues;
        }
    }

    /// <summary>
    /// Every named series of the last calculation, unrounded, for the calculation that reads them next.
    /// </summary>
    /// <remarks>
    /// The same dictionary as <see cref="OutputValues"/> unless IncludeOutputValues or RoundingDigits says to
    /// publish less. A composite reads its components' named series from here: from the published copy, turning
    /// output values off made 56 indicators throw and rounding fed rounded inputs to the rest.
    /// </remarks>
    internal Dictionary<string, List<double>> ChainedOutputs { get; private set; } = new Dictionary<string, List<double>>();

    /// <summary>Sets the named series a calculation keeps for the next one, and the copy it publishes.</summary>
    internal void SetOutputs(Dictionary<string, List<double>> chained, Dictionary<string, List<double>> published)
    {
        ChainedOutputs = chained;
        _outputValues = published;
    }
    public List<Signal> SignalsList { get; set; }
    public IndicatorOptions? Options { get; set; }
    public int Count { get; set; }

    /// <summary>
    /// Initializes the StockData Class using prebuilt lists of price information
    /// </summary>
    /// <param name="openPrices"></param>
    /// <param name="highPrices"></param>
    /// <param name="lowPrices"></param>
    /// <param name="closePrices"></param>
    /// <param name="volumes"></param>
    /// <param name="dates"></param>
    public StockData(IEnumerable<double> openPrices, IEnumerable<double> highPrices, IEnumerable<double> lowPrices, IEnumerable<double> closePrices,
        IEnumerable<double> volumes, IEnumerable<DateTime> dates)
    {
        _openPrices = new List<double>(openPrices);
        _highPrices = new List<double>(highPrices);
        _lowPrices = new List<double>(lowPrices);
        _closePrices = new List<double>(closePrices);
        _volumes = new List<double>(volumes);
        _dates = new List<DateTime>(dates);
        _columnsInitialized = true;
        _rowsInitialized = false;
        _tickerDataList = null;
        CustomValuesList = new List<double>();
        OutputValues = new Dictionary<string, List<double>>();
        SignalsList = new List<Signal>();
        IndicatorName = IndicatorName.None;
        Options = new IndicatorOptions();
        Count = CalculateCount(_openPrices, _highPrices, _lowPrices, _closePrices, _volumes, _dates);
    }

    /// <summary>
    /// Takes the caller's columns as they are, without copying them.
    /// </summary>
    /// <remarks>
    /// <para>The constructor above copies every column into a <see cref="List{T}"/>, six times. That is most of
    /// what a batch run costs before any arithmetic happens: at 10,000 bars it is 961,168 of the 1,208,528
    /// bytes a builder run allocated, against the 0 a library that reads a caller's span allocates.</para>
    /// <para>Nothing here is defensive. The caller keeps ownership of the arrays these views sit on, and
    /// writing to one while a run reads it changes what that run computes. That is the trade the caller makes
    /// by choosing <c>IndicatorDataSource.FromColumns</c> over <c>FromBatch</c>, and it is why this is
    /// internal: the facade is where the choice is offered and documented.</para>
    /// <para>The <see cref="List{T}"/> properties still work. They are built from these views on first use, so
    /// a caller that reaches for one pays exactly the copy it would have paid anyway, and the compute layer -
    /// which reads the spans instead - never triggers it.</para>
    /// </remarks>
    /// <remarks>
    /// A factory rather than a constructor overload. <c>double[]</c> converts to both
    /// <see cref="IEnumerable{T}"/> and <see cref="ReadOnlyMemory{T}"/>, so an overload would make every
    /// existing <c>new StockData(arrays...)</c> call ambiguous - a source-breaking change for callers who are
    /// not asking for any of this.
    /// </remarks>
    internal static StockData FromColumnViews(ReadOnlyMemory<double> openPrices,
        ReadOnlyMemory<double> highPrices, ReadOnlyMemory<double> lowPrices, ReadOnlyMemory<double> closePrices,
        ReadOnlyMemory<double> volumes, ReadOnlyMemory<DateTime> dates) =>
        new(openPrices, highPrices, lowPrices, closePrices, volumes, dates);

    private StockData(ReadOnlyMemory<double> openPrices, ReadOnlyMemory<double> highPrices,
        ReadOnlyMemory<double> lowPrices, ReadOnlyMemory<double> closePrices, ReadOnlyMemory<double> volumes,
        ReadOnlyMemory<DateTime> dates)
    {
        _openMemory = openPrices;
        _highMemory = highPrices;
        _lowMemory = lowPrices;
        _closeMemory = closePrices;
        _volumeMemory = volumes;
        _dateMemory = dates;
        _columnsInitialized = true;
        _rowsInitialized = false;
        _tickerDataList = null;
        CustomValuesList = new List<double>();
        OutputValues = new Dictionary<string, List<double>>();
        SignalsList = new List<Signal>();
        IndicatorName = IndicatorName.None;
        Options = new IndicatorOptions();

        // Mirrors CalculateCount: columns of unequal length describe no bars at all, and reporting a count for
        // them would have every indicator read past the end of the shortest one.
        var count = closePrices.Length;
        Count = openPrices.Length == count && highPrices.Length == count && lowPrices.Length == count
            && volumes.Length == count && dates.Length == count
            ? count
            : 0;
    }
    /// <summary>
    /// Initializes the StockData Class using classic list of ticker information
    /// </summary>
    /// <param name="tickerDataList"></param>
    public StockData(IEnumerable<TickerData> tickerDataList)
    {
        _tickerDataList = new List<TickerData>();
        foreach (var ticker in tickerDataList)
        {
            _tickerDataList.Add(ticker);
        }

        _rowsInitialized = true;
        _columnsInitialized = false;
        _openPrices = null;
        _highPrices = null;
        _lowPrices = null;
        _closePrices = null;
        _volumes = null;
        _dates = null;
        CustomValuesList = new List<double>();
        OutputValues = new Dictionary<string, List<double>>();
        SignalsList = new List<Signal>();
        Options = new IndicatorOptions();
        Count = _tickerDataList.Count;
    }

    /// <summary>
    /// Private constructor for <see cref="WithValues"/>. Shares the price series by reference rather
    /// than copying them, so deriving a view is cheap enough to do inside a calculation.
    /// </summary>
    private StockData(StockData source, List<double> values)
    {
        source.EnsureColumns();

        _openPrices = source._openPrices;
        _highPrices = source._highPrices;
        _lowPrices = source._lowPrices;
        _closePrices = source._closePrices;
        _volumes = source._volumes;
        _dates = source._dates;
        _inputValues = source._inputValues;
        _columnsInitialized = true;
        _rowsInitialized = false;
        _tickerDataList = null;

        CustomValuesList = values;
        OutputValues = new Dictionary<string, List<double>>();
        SignalsList = new List<Signal>();
        IndicatorName = IndicatorName.None;
        Options = source.Options;
        Count = source.Count;
    }

    /// <summary>
    /// Returns a new <see cref="StockData"/> over the same bars, but with <paramref name="values"/> as
    /// the series that calculations will read as their input.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the explicit form of chaining. Today an indicator hands its result to the next one by
    /// writing <see cref="CustomValuesList"/> on the caller's own object, which means anything else
    /// holding that object sees its input change underneath it - the cause of issue #145. A view
    /// leaves the original untouched:
    /// </para>
    /// <code>
    /// // implicit: mutates stockData, and every later call on it sees the new series
    /// var bands = stockData.CalculateSma(20).CalculateBollingerBands();
    ///
    /// // explicit: stockData is unchanged, and the chained input is visible at the call site
    /// var sma = stockData.CalculateSma(20).CustomValuesList;
    /// var bands = stockData.WithValues(sma).CalculateBollingerBands();
    /// </code>
    /// <para>
    /// The price series are shared by reference, not copied, so this is cheap. The returned object has
    /// its own outputs, signals and indicator name.
    /// </para>
    /// </remarks>
    /// <param name="values">The series to use as the calculation input.</param>
    public StockData WithValues(IReadOnlyList<double> values)
    {
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        // Reuse the caller's list when it already is one - WithValues does not take ownership, and
        // copying a long series on every derivation is exactly the cost this is meant to avoid.
        var list = values as List<double> ?? new List<double>(values);

        return new StockData(this, list);
    }

    /// <summary>
    /// Returns a view over the same bars whose input series is this result's named output, so the next
    /// calculation continues from that series.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Chaining an indicator onto another already works without any special syntax, because
    /// <c>Calculate*</c> takes a <see cref="StockData"/> and returns one:
    /// </para>
    /// <code>
    /// data.CalculateSimpleMovingAverage(20).CalculateBollingerBands();
    /// </code>
    /// <para>
    /// What that cannot express is <i>which</i> series to continue from when a result publishes several.
    /// That is a question about the result, so it belongs here rather than in some separate step the
    /// caller has to learn:
    /// </para>
    /// <code>
    /// var bands = data.CalculateBollingerBands();
    /// var upperRsi = bands.SeriesView("UpperBand").CalculateRsi(14);
    /// var lowerRsi = bands.SeriesView("LowerBand").CalculateRsi(14);
    /// </code>
    /// <para>
    /// The view is a <see cref="StockData"/>, so every existing calculation chains off it unchanged, and
    /// because it is a view rather than a mutation both <c>bands</c> and the original data are untouched
    /// - one result can be branched as many ways as you like.
    /// </para>
    /// <para>
    /// Generated accessors call this, so <c>bands.UpperBand()</c> fails to compile on a misspelling
    /// rather than failing here at run time.
    /// </para>
    /// </remarks>
    /// <param name="outputName">The published output to continue from.</param>
    /// <exception cref="CalculationException">
    /// Thrown when this result publishes no output of that name. The message lists what it does publish.
    /// </exception>
    public StockData SeriesView(string outputName)
    {
        if (outputName is null || outputName.Length == 0)
        {
            throw new ArgumentException("An output name is required.", nameof(outputName));
        }

        if (OutputValues is null || !OutputValues.TryGetValue(outputName, out var series))
        {
            var available = OutputValues is null || OutputValues.Count == 0
                ? "none"
                : string.Join(", ", OutputValues.Keys);

            throw new CalculationException(
                $"{IndicatorName} does not publish an output named '{outputName}'. Available outputs: {available}.");
        }

        return WithValues(series);
    }

    public void EnsureColumnView()
    {
        EnsureColumns();
    }

    public void EnsureRowView()
    {
        EnsureRows();
    }

    private static int CalculateCount(List<double> openPrices, List<double> highPrices, List<double> lowPrices,
        List<double> closePrices, List<double> volumes, List<DateTime> dates)
    {
        return (openPrices.Count + highPrices.Count + lowPrices.Count + closePrices.Count + volumes.Count + dates.Count) / 6 == closePrices.Count
            ? closePrices.Count
            : 0;
    }

    private void EnsureColumns()
    {
        if (_columnsInitialized)
        {
            // A column with a view is left null on purpose. Standing an empty list in for it here would satisfy
            // the null check in its getter, so the view would never be read and the column would answer empty
            // for the rest of this instance's life.
            if (_openPrices is null && !_openMemory.HasValue) _openPrices = new List<double>();
            if (_highPrices is null && !_highMemory.HasValue) _highPrices = new List<double>();
            if (_lowPrices is null && !_lowMemory.HasValue) _lowPrices = new List<double>();
            if (_closePrices is null && !_closeMemory.HasValue) _closePrices = new List<double>();
            if (_volumes is null && !_volumeMemory.HasValue) _volumes = new List<double>();
            if (_dates is null && !_dateMemory.HasValue) _dates = new List<DateTime>();
            return;
        }

        if (_tickerDataList == null || _tickerDataList.Count == 0)
        {
            _openPrices = new List<double>();
            _highPrices = new List<double>();
            _lowPrices = new List<double>();
            _closePrices = new List<double>();
            _volumes = new List<double>();
            _dates = new List<DateTime>();
            _columnsInitialized = true;
            return;
        }

        var count = _tickerDataList.Count;
        var openPrices = new List<double>(count);
        var highPrices = new List<double>(count);
        var lowPrices = new List<double>(count);
        var closePrices = new List<double>(count);
        var volumes = new List<double>(count);
        var dates = new List<DateTime>(count);

        for (var i = 0; i < count; i++)
        {
            var ticker = _tickerDataList[i];
            dates.Add(ticker.Date);
            openPrices.Add(ticker.Open);
            highPrices.Add(ticker.High);
            lowPrices.Add(ticker.Low);
            closePrices.Add(ticker.Close);
            volumes.Add(ticker.Volume);
        }

        _openPrices = openPrices;
        _highPrices = highPrices;
        _lowPrices = lowPrices;
        _closePrices = closePrices;
        _volumes = volumes;
        _dates = dates;
        _columnsInitialized = true;
    }

    /// <summary>
    /// Builds the list form of one adopted column, for a caller that asked for that column and no other.
    /// </summary>
    /// <remarks>
    /// Per column on purpose. Filling all six whenever one is touched costs 480,000 bytes at 10,000 bars to
    /// answer a question about 80,000 of them, which is most of what the adopting constructor just saved.
    /// </remarks>
    private static List<T> Materialize<T>(ReadOnlySpan<T> source)
    {
        var list = new List<T>(source.Length);
        for (var i = 0; i < source.Length; i++)
        {
            list.Add(source[i]);
        }

        return list;
    }

    private void EnsureRows()
    {
        if (_rowsInitialized)
        {
            _tickerDataList ??= new List<TickerData>();
            return;
        }

        EnsureColumns();
        var count = Count;
        var rows = new List<TickerData>(count);

        // Through the properties, not the fields: a column handed over as a view is built by its own getter, so
        // reading the field would find the null this instance deliberately left there.
        var dates = Dates;
        var opens = OpenPrices;
        var highs = HighPrices;
        var lows = LowPrices;
        var closes = ClosePrices;
        var volumes = Volumes;

        for (var i = 0; i < count; i++)
        {
            rows.Add(new TickerData
            {
                Date = dates[i],
                Open = opens[i],
                High = highs[i],
                Low = lows[i],
                Close = closes[i],
                Volume = volumes[i]
            });
        }

        _tickerDataList = rows;
        _rowsInitialized = true;
    }
}
