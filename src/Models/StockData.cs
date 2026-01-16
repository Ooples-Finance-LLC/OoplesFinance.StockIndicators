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
    private List<double>? _openPrices;
    private List<double>? _highPrices;
    private List<double>? _lowPrices;
    private List<double>? _closePrices;
    private List<double>? _volumes;
    private List<DateTime>? _dates;
    private List<TickerData>? _tickerDataList;
    private bool _columnsInitialized;
    private bool _rowsInitialized;

    public InputName InputName { get; set; }
    public IndicatorName IndicatorName { get; set; }

    public List<double> InputValues
    {
        get
        {
            if (_inputValues == null)
            {
                _inputValues = new List<double>(ClosePrices);
            }

            return _inputValues;
        }
        set => _inputValues = value ?? new List<double>();
    }

    public List<double> OpenPrices
    {
        get
        {
            EnsureColumns();
            return _openPrices!;
        }
        set
        {
            _openPrices = value ?? new List<double>();
            _columnsInitialized = true;
        }
    }

    public List<double> HighPrices
    {
        get
        {
            EnsureColumns();
            return _highPrices!;
        }
        set
        {
            _highPrices = value ?? new List<double>();
            _columnsInitialized = true;
        }
    }

    public List<double> LowPrices
    {
        get
        {
            EnsureColumns();
            return _lowPrices!;
        }
        set
        {
            _lowPrices = value ?? new List<double>();
            _columnsInitialized = true;
        }
    }

    public List<double> ClosePrices
    {
        get
        {
            EnsureColumns();
            return _closePrices!;
        }
        set
        {
            _closePrices = value ?? new List<double>();
            _columnsInitialized = true;
        }
    }

    public List<double> Volumes
    {
        get
        {
            EnsureColumns();
            return _volumes!;
        }
        set
        {
            _volumes = value ?? new List<double>();
            _columnsInitialized = true;
        }
    }

    public List<DateTime> Dates
    {
        get
        {
            EnsureColumns();
            return _dates!;
        }
        set
        {
            _dates = value ?? new List<DateTime>();
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

    public List<double> CustomValuesList { get; set; }
    public Dictionary<string, List<double>> OutputValues { get; set; }
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
        IEnumerable<double> volumes, IEnumerable<DateTime> dates, InputName inputName = InputName.Close)
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
        InputName = inputName;
        IndicatorName = IndicatorName.None;
        Options = new IndicatorOptions();
        Count = CalculateCount(_openPrices, _highPrices, _lowPrices, _closePrices, _volumes, _dates);
    }

    /// <summary>
    /// Initializes the StockData Class using classic list of ticker information
    /// </summary>
    /// <param name="tickerDataList"></param>
    public StockData(IEnumerable<TickerData> tickerDataList, InputName inputName = InputName.Close)
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
        InputName = inputName;
        Options = new IndicatorOptions();
        Count = _tickerDataList.Count;
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
            _openPrices ??= new List<double>();
            _highPrices ??= new List<double>();
            _lowPrices ??= new List<double>();
            _closePrices ??= new List<double>();
            _volumes ??= new List<double>();
            _dates ??= new List<DateTime>();
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
        var dates = _dates!;
        var opens = _openPrices!;
        var highs = _highPrices!;
        var lows = _lowPrices!;
        var closes = _closePrices!;
        var volumes = _volumes!;

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
