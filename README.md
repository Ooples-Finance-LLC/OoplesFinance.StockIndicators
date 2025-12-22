# OoplesFinance.StockIndicators (High-Precision Fork)

> "Approximation is the enemy of alpha. In finance, 3.14 is not Pi. It is an error."

**Original Library:** [OoplesFinance.StockIndicators](https://github.com/Ooples/OoplesFinance.StockIndicators)

This library is a strict, high-precision fork of the original codebase.

## The Architecture of Rigor

The original library offers an impressive catalog of 700+ indicators. It also suffers from a catastrophic architectural decision: aggressive, pervasive rounding.

The upstream implementation rounds inputs. It rounds intermediate calculations. It rounds outputs. It truncates mathematical constants (using `3.14` for $\pi$). For visualization, this is acceptable. For algorithmic trading, backtesting, or any system where error propagation matters, it is disqualifying.

This fork exists to correct the math so it can be used for cross-validation of resuts in testing of [QuanTAlib](https://github.com/mihakralj/QuanTAlib) indicators.

### The Intervention

1. **Rounding Removal**: Every instance of `Math.Round` was excised. The code now respects the full precision of the `decimal` type.
2. **Constant Restoration**: Truncated literals were replaced with their full-precision `System.Math` equivalents. $\pi$ is now $\pi$, not a rough guess.
3. **Logic Preservation**: No algorithmic logic was altered. No features were added. No features were removed. This is the original code, stripped of its plague of rounding inaccuracies.

## Usage

The API surface remains identical to the original. If you know how to use Ooples, you know how to use this—you will just get different (correct) numbers.

### Basic Calculation

```csharp
var stockData = new StockData(openPrices, highPrices, lowPrices, closePrices, volumes);
var results = stockData.CalculateRelativeStrengthIndex().CalculateMovingAverageConvergenceDivergence();
```

### Alpaca Integration Example

```csharp
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

// Data is loaded without pre-rounding
var stockData = new StockData(
    bars.Select(x => x.Open), 
    bars.Select(x => x.High), 
    bars.Select(x => x.Low), 
    bars.Select(x => x.Close), 
    bars.Select(x => x.Volume), 
    bars.Select(x => x.TimeUtc)
);

var results = stockData.CalculateBollingerBands();
```

### State Management Warning

The library maintains internal state when chaining. If reusing the `stockData` object for disjoint calculations, state must be cleared.

```csharp
var stockData = new StockData(bars.Select(x => x.Open), ...);

var sma = stockData.CalculateSimpleMovingAverage(14);

// Failure to clear results in contamination of subsequent calculations
stockData.Clear();

var ema = stockData.CalculateExponentialMovingAverage(14);
```

## License

Open source under the Apache 2.0 license.

![GitHub](https://img.shields.io/github/license/ooples/OoplesFinance.StockIndicators?style=plastic)
