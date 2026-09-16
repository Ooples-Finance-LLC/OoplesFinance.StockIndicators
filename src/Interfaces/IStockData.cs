//     Ooples Finance Stock Indicator Library
//     https://ooples.github.io/OoplesFinance.StockIndicators/
//
//     Copyright © Franklin Moormann, 2020-2022
//     cheatcountry@gmail.com
//
//     This library is free software and it uses the Apache 2.0 license
//     so if you are going to re-use or modify my code then I just ask
//     that you include my copyright info and my contact info in a comment

namespace OoplesFinance.StockIndicators.Interfaces;

public interface IStockData
{
    IndicatorName IndicatorName { get; }
    List<double> InputValues { get; }
    List<double> OpenPrices { get; }
    List<double> HighPrices { get; }
    List<double> LowPrices { get; }
    List<double> ClosePrices { get; }
    List<double> Volumes { get; }
    List<DateTime> Dates { get; }
    List<double> CustomValuesList { get; }
    Dictionary<string, List<double>> OutputValues { get; }
    List<Signal> SignalsList { get; }
    int Count { get; }
}