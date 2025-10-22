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
    public InputName InputName { get; set; }
    public IndicatorName IndicatorName { get; set; }
    public List<double> InputValues { get; set; }
    public List<double> OpenPrices { get; set; }
    public List<double> HighPrices { get; set; }
    public List<double> LowPrices { get; set; }
    public List<double> ClosePrices { get; set; }
    public List<double> Volumes { get; set; }
    public List<DateTime> Dates { get; set; }
    public List<TickerData> TickerDataList { get; set; }
    public List<double> CustomValuesList { get; set; }
    public Dictionary<string, List<double>> OutputValues { get; set; }
    public List<Signal> SignalsList { get; set; }
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
        OpenPrices = new List<double>(openPrices);
        HighPrices = new List<double>(highPrices);
        LowPrices = new List<double>(lowPrices);
        ClosePrices = new List<double>(closePrices);
        Volumes = new List<double>(volumes);
        Dates = new List<DateTime>(dates);
        InputValues = new List<double>(closePrices);
        CustomValuesList = new List<double>();
        OutputValues = new Dictionary<string, List<double>>();
        SignalsList = new List<Signal>();
        InputName = inputName;
        IndicatorName = IndicatorName.None;
        Count = (OpenPrices.Count + HighPrices.Count + LowPrices.Count + ClosePrices.Count + Volumes.Count + Dates.Count) / 6 == ClosePrices.Count ? ClosePrices.Count : 0;

        TickerDataList = new List<TickerData>();
        for (var i = 0; i < Count; i++)
        {
            var open = OpenPrices[i];
            var high = HighPrices[i];
            var low = LowPrices[i];
            var close = ClosePrices[i];
            var volume = Volumes[i];
            var date = Dates[i];

            var ticker = new TickerData()
            {
                Date = date,
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = volume
            };
            TickerDataList.Add(ticker);
        }
    }

    /// <summary>
    /// Initializes the StockData Class using classic list of ticker information
    /// </summary>
    /// <param name="tickerDataList"></param>
    public StockData(IEnumerable<TickerData> tickerDataList, InputName inputName = InputName.Close)
    {
        OpenPrices = new List<double>();
        HighPrices = new List<double>();
        LowPrices = new List<double>();
        ClosePrices = new List<double>();
        Volumes = new List<double>();
        Dates = new List<DateTime>();
        CustomValuesList = new List<double>();
        OutputValues = new Dictionary<string, List<double>>();
        SignalsList = new List<Signal>();
        InputName = inputName;

        for (var i = 0; i < tickerDataList.Count(); i++)
        {
            var ticker = tickerDataList.ElementAt(i);

            var date = ticker.Date;
            Dates.Add(date);

            var open = ticker.Open;
            OpenPrices.AddRounded(open);

            var high = ticker.High;
            HighPrices.AddRounded(high);

            var low = ticker.Low;
            LowPrices.AddRounded(low);

            var close = ticker.Close;
            ClosePrices.AddRounded(close);

            var volume = ticker.Volume;
            Volumes.AddRounded(volume);
        }
        
        TickerDataList = tickerDataList.ToList();
        InputValues = new List<double>(ClosePrices);
        Count = (OpenPrices.Count + HighPrices.Count + LowPrices.Count + ClosePrices.Count + Volumes.Count + Dates.Count) / 6 == ClosePrices.Count ? ClosePrices.Count : 0;
    }
}