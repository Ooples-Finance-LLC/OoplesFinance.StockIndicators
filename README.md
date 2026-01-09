# OoplesFinance.StockIndicators (High-Precision Fork)

High-precision technical indicators with a growing streaming and performance-focused toolchain. This fork removes rounding, restores mathematical constants, and adds modern streaming, stateful indicators, and benchmark coverage while preserving the familiar API.

## Highlights
- Precision first: no `Math.Round`, real constants (`Math.PI`, `Math.Sqrt(2)`), and full double precision output.
- Streaming-ready: trade/quote/bar ingestion, timeframes, and stateful indicators that update incrementally.
- Multi-series streaming: register indicators that consume multiple symbols/timeframes with alignment policies.
- Performance focus: ongoing algorithmic and allocation optimizations with benchmarks to validate changes.
- Targets: `net461`, `net10.0`.

## Indicators
See the full list in `INDICATORS.md`.

## Quick start (batch)
```csharp
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators;

var data = new List<TickerData>
{
    new() { Date = DateTime.UtcNow, Open = 100, High = 101, Low = 99, Close = 100.5, Volume = 1000 },
    new() { Date = DateTime.UtcNow.AddMinutes(1), Open = 100.5, High = 102, Low = 100, Close = 101.8, Volume = 900 }
};

var stockData = new StockData(data);
var sma = stockData.CalculateSimpleMovingAverage(20).CustomValuesList;
```

## Streaming quick start (single-series)
```csharp
using OoplesFinance.StockIndicators.Streaming;

var engine = new StreamingIndicatorEngine(new StreamingIndicatorEngineOptions
{
    EmitUpdates = false
});

engine.RegisterStatefulIndicator(
    "AAPL",
    BarTimeframe.Tick,
    new SimpleMovingAverageState(5),
    update => Console.WriteLine($"SMA(5) = {update.Value:F4}"),
    new IndicatorSubscriptionOptions { IncludeUpdates = false });

engine.OnTrade(new StreamTrade("AAPL", DateTime.UtcNow, 100, 1));
engine.OnTrade(new StreamTrade("AAPL", DateTime.UtcNow.AddSeconds(1), 101, 1));
```

## Streaming quick start (multi-series)
```csharp
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Streaming;

var engine = new StreamingIndicatorEngine(new StreamingIndicatorEngineOptions
{
    EmitUpdates = false
});

var primary = new SeriesKey("AAPL", BarTimeframe.Minutes(1));
var secondary = new SeriesKey("MSFT", BarTimeframe.Minutes(1));

engine.RegisterMultiSeriesIndicator(
    primary,
    new[] { secondary },
    new SpreadState(primary, secondary),
    update => Console.WriteLine($"Spread = {update.Value:F4}"),
    new IndicatorSubscriptionOptions
    {
        IncludeUpdates = false,
        SeriesAlignmentPolicy = SeriesAlignmentPolicy.Strict
    });

// Custom multi-series example state
sealed class SpreadState : IMultiSeriesIndicatorState
{
    private readonly SeriesKey _left;
    private readonly SeriesKey _right;

    public SpreadState(SeriesKey left, SeriesKey right)
    {
        _left = left;
        _right = right;
    }

    public IndicatorName Name => IndicatorName.None;

    public void Reset() { }

    public MultiSeriesIndicatorStateResult Update(MultiSeriesContext context, SeriesKey series, OhlcvBar bar,
        bool isFinal, bool includeOutputs)
    {
        if (!context.TryGetLatest(_left, out var left) || !context.TryGetLatest(_right, out var right))
        {
            return new MultiSeriesIndicatorStateResult(false, 0d, null);
        }

        var value = left.Close - right.Close;
        return new MultiSeriesIndicatorStateResult(true, value, null);
    }
}
```

Notes:
- Default alignment is `SeriesAlignmentPolicy.LastKnown` (emit using most recent bars).
- `SeriesAlignmentPolicy.Strict` requires all series to share the same `EndTime`. With `IncludeUpdates = false`,
  alignment uses final bars only.

## Dev Console
A developer console is available to run batch, streaming, and multi-series examples locally.

```
dotnet run --project examples/OoplesFinance.StockIndicators.DevConsole/OoplesFinance.StockIndicators.DevConsole.csproj
```

Non-interactive:
```
dotnet run --project examples/OoplesFinance.StockIndicators.DevConsole/OoplesFinance.StockIndicators.DevConsole.csproj -- --run-all --no-pause
```

## Performance chart
Sample BenchmarkDotNet results (Count=10000, net10.0). Optimized = this fork, Baseline = original library.

<!-- PERF_TABLES_START -->
Length = 14
| Indicator | Optimized (us) | Baseline (us) | Speedup |
| --- | --- | --- | --- |
| SMA | 319.6 | 13636.9 | 42.7x |
| EMA | 323.0 | 814.4 | 2.5x |
| RSI | 889.0 | 18767.6 | 21.1x |
| MACD | 549.2 | 8872.7 | 16.2x |
| Bollinger Bands | 1292.2 | 33604.7 | 26.0x |
| ATR | 412.9 | 13023.5 | 31.5x |
| Chande CMO | 679.5 | 27632.6 | 40.7x |
| Ulcer Index | 2620.4 | 30663.0 | 11.7x |

Length = 50
| Indicator | Optimized (us) | Baseline (us) | Speedup |
| --- | --- | --- | --- |
| SMA | 321.8 | 28647.1 | 89.0x |
| EMA | 304.9 | 783.4 | 2.6x |
| RSI | 879.0 | 18738.7 | 21.3x |
| MACD | 551.2 | 8919.8 | 16.2x |
| Bollinger Bands | 1299.6 | 23400.2 | 18.0x |
| ATR | 375.1 | 13073.6 | 34.9x |
| Chande CMO | 644.5 | 11835.8 | 18.4x |
| Ulcer Index | 2756.0 | 14288.4 | 5.2x |

Length = 200
| Indicator | Optimized (us) | Baseline (us) | Speedup |
| --- | --- | --- | --- |
| SMA | 307.8 | 14493.6 | 47.1x |
| EMA | 304.2 | 950.9 | 3.1x |
| RSI | 925.7 | 18540.0 | 20.0x |
| MACD | 543.8 | 8948.7 | 16.5x |
| Bollinger Bands | 1820.9 | 67349.6 | 37.0x |
| ATR | 394.2 | 13098.8 | 33.2x |
| Chande CMO | 630.4 | 33713.7 | 53.5x |
| Ulcer Index | 2749.9 | 72270.8 | 26.3x |
<!-- PERF_TABLES_END -->

Streaming fanout (100k ticks, 5 timeframes):
| Scenario | Indicators | Outputs | Throughput (ticks/sec) | p95 latency (ms) | Updates |
| --- | --- | --- | --- | --- | --- |
| Core | 10 | off | 207,346 | 0.01 | 6,001,190 |
| Core + outputs | 10 | on | 109,301 | 0.02 | 6,001,190 |
| Extended | 15 | off | 140,680 | 0.01 | 9,001,785 |

Streaming scaling (100k ticks, outputs off):
| Timeframes | Indicators | Throughput (ticks/sec) | p95 latency (ms) | Updates |
| --- | --- | --- | --- | --- |
| 1 | 5 | 616,430 | 0.00 | 1,000,000 |
| 3 | 5 | 404,420 | 0.00 | 2,000,590 |
| 5 | 5 | 263,733 | 0.01 | 3,000,595 |
| 5 | 10 | 207,346 | 0.01 | 6,001,190 |
| 5 | 15 | 140,680 | 0.01 | 9,001,785 |

Full reports: `BenchmarkDotNet.Artifacts/results/OoplesFinance.StockIndicators.Benchmarks.IndicatorBenchmarks-report-github.md`

## Benchmarks
Benchmarks live in `benchmarks/` and include batch and streaming performance harnesses.

```
dotnet run --project benchmarks/OoplesFinance.StockIndicators.Benchmarks/OoplesFinance.StockIndicators.Benchmarks.csproj -c Release
```

Run streaming fanout throughput/latency:
```
dotnet run --project benchmarks/OoplesFinance.StockIndicators.Benchmarks/OoplesFinance.StockIndicators.Benchmarks.csproj -c Release -- --streaming-perf --ticks 100000
```

Compare optimized vs baseline:
```
.\benchmarks\setup-baseline.ps1 -Ref master
dotnet build -c Release -p:AssemblyName=OoplesFinance.StockIndicators.Original -p:TargetFramework=net10.0 benchmarks/.baseline/src/OoplesFinance.StockIndicators.csproj
dotnet run --project benchmarks/OoplesFinance.StockIndicators.Benchmarks/OoplesFinance.StockIndicators.Benchmarks.csproj -c Release -- --filter *IndicatorBenchmarks*
```

Results are written to `BenchmarkDotNet.Artifacts/`.

## Project layout
- `src/` core library
- `tests/` unit tests
- `benchmarks/` BenchmarkDotNet suite
- `examples/` developer console and examples

## Additional docs
- `INDICATORS.md` list of indicators
- `OPTIMIZATIONS.md` optimization backlog and notes
- `MODERNIZATION_PLAN.md` roadmap for refactors and performance work

## License
Apache 2.0. See `LICENSE.txt`.

