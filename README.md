
![Nuget](https://img.shields.io/nuget/dt/OoplesFinance.StockIndicators?style=plastic)
![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/OoplesFinance.StockIndicators?style=plastic)
![GitHub](https://img.shields.io/github/license/ooples/OoplesFinance.StockIndicators?style=plastic)

## .Net Stock Indicator Library

This is a stock indicator library that is completely open source (Apache 2.0 license) and very easy to use. Current version contains [763 stock indicators](https://ooples.github.io/OoplesFinance.StockIndicators/indicators) and I will add more as I get requests for them!


### How to use this library

Here is an example to show how easy it is to create indicators using other indicators

```cs
var stockData = new StockData(openPrices, highPrices, lowPrices, closePrices, volumes);
var results = stockData.CalculateRelativeStrengthIndex().CalculateMovingAverageConvergenceDivergence();
```

Here is a simple example calculating default bollinger bands to get you started using the [Alpaca C# Api](https://github.com/alpacahq/alpaca-trade-api-csharp)

```cs
using Alpaca.Markets;
using OoplesFinance.StockIndicators.Models;
using static OoplesFinance.StockIndicators.Calculations;

const string paperApiKey = "REPLACEME";
const string paperApiSecret = "REPLACEME";
const string symbol = "AAPL";
var startDate = new DateTime(2021, 01, 01);
var endDate = new DateTime(2021, 12, 31);

var client = Environments.Paper.GetAlpacaDataClient(new SecretKey(paperApiKey, paperApiSecret));
var bars = (await client.ListHistoricalBarsAsync(new HistoricalBarsRequest(symbol, startDate, endDate, BarTimeFrame.Day)).ConfigureAwait(false)).Items;
var stockData = new StockData(bars.Select(x => x.Open), bars.Select(x => x.High), bars.Select(x => x.Low), bars.Select(x => x.Close), bars.Select(x => x.Volume), bars.Select(x => x.TimeUtc));

var results = stockData.CalculateBollingerBands();
var upperBandList = results.OutputValues["UpperBand"];
var middleBandList = results.OutputValues["MiddleBand"];
var lowerBandList = results.OutputValues["LowerBand"];
```

Here is a more advanced example showing how to calculate bollinger bands with full customization and using a custom input of high rather than the default close
```cs
var stockData = new StockData(bars.Select(x => x.Open), bars.Select(x => x.High), bars.Select(x => x.Low), 
bars.Select(x => x.Close), bars.Select(x => x.Volume), bars.Select(x => x.TimeUtc), InputName.High);

var results = stockData.CalculateBollingerBands(MovingAvgType.EhlersMesaAdaptiveMovingAverage, 15, 2.5m);
var upperBandList = results.OutputValues["UpperBand"];
var middleBandList = results.OutputValues["MiddleBand"];
var lowerBandList = results.OutputValues["LowerBand"];
```

It is extremely important to remember that if you use the same data source to calculate different indicators without using the chaining method then you need to clear the data in between each call. We have a great example for this below:
```cs
var stockData = new StockData(bars.Select(x => x.Open), bars.Select(x => x.High), bars.Select(x => x.Low), 
bars.Select(x => x.Close), bars.Select(x => x.Volume), bars.Select(x => x.TimeUtc), InputName.High);

var sma = stockData.CalculateSimpleMovingAverage(14);

// if you don't perform this clear method in between then your ema result will be calculated using the sma results
stockData.Clear();

var ema = stockData.CalculateExponentialMovingAverage(14);
```

For more detailed Alpaca examples then check out my more advanced [Alpaca example code](https://github.com/alpacahq/alpaca-trade-api-csharp/blob/develop/UsageExamples/IndicatorLibraryExample.cs)


### Support This Project

BTC: 36DRmZefJNW82q9pHY1kWYSZhLUWQkpgGq

ETH: 0x7D6e58754476189ffF736B63b6159D2647f74f34

USDC: 0x587Ae0709f45b970992bdD772bF693141D95CAED

DOGE: DF1nsK1nLASzmwHNAfNengBGS4w7bNyJ1e

SHIB: 0xCDe2355212764218355c9393FbE121Ae49B43382

Paypal: [https://www.paypal.me/cheatcountry](https://www.paypal.me/cheatcountry)

Patreon: [https://patreon.com/cheatcountry](https://patreon.com/cheatcountry)


### Support or Contact

Email me at cheatcountry@gmail.com for any help or support or to let me know of ways to further improve this library.
