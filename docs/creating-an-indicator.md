# Creating an indicator

You write the calculation. Everything else — resolving the input series, the derived series most
indicators need, publishing outputs, chaining, branching — comes from `IndicatorBase`.

## A complete indicator

```csharp
using OoplesFinance.StockIndicators.Indicators;

[Indicator("Range Bands", Description = "A moving average with bands a multiple of ATR away.")]
public sealed class RangeBands : IndicatorBase
{
    public RangeBands(int length = 20, double multiplier = 2)
    {
        Length = length;
        Multiplier = multiplier;
    }

    public int Length { get; }

    public double Multiplier { get; }

    protected override void Calculate()
    {
        var basis = MovingAverage(MovingAvgType.SimpleMovingAverage, Length);
        var range = AverageTrueRange(Length);

        var upper = NewSeries();
        var lower = NewSeries();
        for (var i = 0; i < Count; i++)
        {
            upper.Add(basis[i] + (range[i] * Multiplier));
            lower.Add(basis[i] - (range[i] * Multiplier));
        }

        Publish("UpperBand", upper);
        Publish("MiddleBand", basis);
        Publish("LowerBand", lower);
        SetPrimary(basis);
    }
}
```

That is the whole thing. Run it:

```csharp
var bands = new RangeBands(20, 2).Run(data);

var upper = bands.OutputValues["UpperBand"];
var basis = bands.CustomValuesList;          // what SetPrimary named
```

## What you get for free

**Chaining, both directions.** `Run` returns a `StockData`, the same type the built-in indicators
return, so your indicator sits anywhere in a chain without any registration:

```csharp
// a built-in feeding yours
var onAnAverage = new RangeBands(10).Run(data.CalculateSimpleMovingAverage(50));

// yours feeding a built-in, continuing from a named output
var rsiOfUpper = bands.SeriesView("UpperBand").CalculateRelativeStrengthIndex(14);
```

**Branching.** `SeriesView` returns a view, so one result can be continued from several ways at once
and neither the result nor the original data is disturbed:

```csharp
var upperRsi = bands.SeriesView("UpperBand").CalculateRelativeStrengthIndex(14);
var lowerRsi = bands.SeriesView("LowerBand").CalculateRelativeStrengthIndex(14);
```

**`Run` never modifies what you hand it.** It works from a view over the same bars.

## What you write

Only `Calculate`. Inside it:

| member | what it gives you |
|---|---|
| `Input` | the series being measured — close prices, or a chained series if the caller passed one |
| `Count` | the number of bars |
| `Source` | the bars themselves: `Open`, `High`, `Low`, `Volume`, and `Values` |
| `MovingAverage(maType, length)` | a moving average of `Input` |
| `MovingAverage(values, maType, length)` | a moving average of any series |
| `StandardDeviation(length)` | rolling standard deviation of `Input` |
| `StandardDeviation(values, length)` | rolling standard deviation of any series |
| `TrueRange()` | true range per bar |
| `AverageTrueRange(length, maType)` | ATR, Wilder-smoothed by default |
| `NewSeries()` | an empty list sized to the bar count |
| `Publish(name, values)` | makes a named output available and chainable |
| `SetPrimary(values)` | what a chain continues from by default |

## Rules the base class enforces

**Every output must have one value per bar.** A series that does not line up with the price series
would silently misalign every reading taken from it, so `Publish` rejects it:

```
Range Bands published 'Short' with 3 values for 200 bars.
```

**A primary is optional.** Without `SetPrimary`, a chain continues from the first output you publish.

## Why the helpers take the series as a parameter

`MovingAverage` and `StandardDeviation` both take the series they measure. That is deliberate, and it
is the one piece of the design worth understanding.

The built-in `Calculate*` methods pass their input through `StockData.CustomValuesList`. That is how
chaining works between calls — and it is also how, within a single calculation, a moving average
could overwrite the series that a standard deviation on the same object then read back. The result
was a band drawn at the dispersion of its own average rather than of price. Issue #145 lists
twenty-three indicators where that happened, and twenty-one more carrying a hand-written guard
against it.

Because the helpers here take their input as a parameter, that cannot be written:

```csharp
// these two are independent; swapping them changes nothing
var basis = MovingAverage(MovingAvgType.SimpleMovingAverage, 20);
var spread = StandardDeviation(20);
```

`Input` is resolved once when the run starts and does not change underneath you.

One consequence worth knowing: `StandardDeviation` here is the population standard deviation about
each window's own mean — what Bollinger Bands and every other dispersion band are defined against,
and what TA-Lib's `TA_STDDEV` computes. It is not the same statistic as the library's older
`CalculateStandardDeviationVolatility`, which averages each bar's squared deviation from its own
contemporaneous moving average.

## Naming

`[Indicator]` is optional. Without it, the indicator is named after its type. Use it when you want a
display name the type name cannot carry:

```csharp
[Indicator("Squeeze Momentum", Description = "Bollinger and Keltner compression.")]
public sealed class SqueezeMomentum : IndicatorBase { ... }
```

## Choosing output names

Callers reach an output by name, so pick names that describe the series. `UpperBand`, `MiddleBand`
and `LowerBand` are the conventional names for a band indicator, and the library's built-in
indicators publish those, which means the generated `UpperBand()` accessor works on your result too:

```csharp
using OoplesFinance.StockIndicators.Series;

var upper = bands.UpperBand();     // compile-checked
```

A name no indicator in the library publishes has no generated accessor, but `SeriesView("MyName")`
always works.

## Testing your indicator

Compare against a reference you write independently, rather than against a recording of what the code
currently produces. A recorded baseline agrees with the implementation by construction, including
when the implementation is wrong — that is exactly how the Bollinger Bands defect survived: the test
compared against a "naive" reference that reproduced the same mistake, so the two agreed with each
other while both disagreed with the indicator.

Two properties are worth asserting for any band indicator, because they hold for every correct
implementation and need no reference at all: the bands are equidistant from the middle, and doubling
the multiplier doubles the distance.
