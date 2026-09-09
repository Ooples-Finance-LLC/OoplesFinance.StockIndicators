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

public sealed class IndicatorOptions
{
    public bool? IncludeOutputValues { get; set; }
    public bool? IncludeSignals { get; set; }
    public bool? IncludeCustomValues { get; set; }
    public bool? EnableDerivedSeriesCache { get; set; }
    public int? RoundingDigits { get; set; }
}
