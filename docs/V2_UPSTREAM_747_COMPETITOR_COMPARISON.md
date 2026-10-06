# Upstream integration competitor comparison

## Final correctness-candidate timings (2026-10-05)

The normal BenchmarkDotNet campaign completed all **116 declared rows** (88 batch,
28 incremental) in 43 minutes. Full retained-output, repeated-write and reset checks
passed **66,082 assertions** against hash-identical copies of the actual timed binaries;
the competitor agreement check passed too. Results and binary identities are in
[the retained artifacts](../benchmarks/results/2026-10-05-correctness/evidence.json),
with [batch tables](../benchmarks/results/2026-10-05-correctness/batch.github.md) and
[incremental tables](../benchmarks/results/2026-10-05-correctness/incremental.github.md).
CSV and compressed raw JSON are retained alongside them.

The run used the default job on source `c2773739`, with affinity mask 255. Verification
ran on other CPU cores of the same machine; cache, memory and thermal conditions were
shared. Incremental tests retain fixed-history iteration setup and one update per
iteration, so their short measurements trigger minimum-iteration-time warnings and
have substantial noise. These results do not establish an isolated-machine speed
ranking. Full recomputation is labeled separately from native incremental work.

The earlier integration observations below remain historical evidence.

Commands run on 2026-10-02 against the integration of correctness head `ff46d55c` and upstream `b492abfb`:

```text
dotnet benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0/OoplesFinance.StockIndicators.CompetitorBenchmarks.dll --coverage
dotnet benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0/OoplesFinance.StockIndicators.CompetitorBenchmarks.dll --verify
```

Both commands exited successfully. These are coverage and numerical observations, not performance measurements or full-trajectory agreement proofs. The benchmark executable loaded the same production assembly as the integration tests, SHA256 `6a54d1db87c0becb86728c4b01ba68bef041e0b03fbbbc3decabb408cecc3b93`.

The Stochastic control distinguishes this library's raw fast %K from competitors' three-bar-smoothed %K. The ATR control also records a QuanTAlib 1.0.0 result that differs from the known true range; the report does not assume equivalent arithmetic merely because names match. Rounding and warmup conventions must be considered alongside any later timings. The upstream harness labels full-history incremental recomputation separately from native incremental execution.

## Reported coverage

| Indicator | This library (v2) | This library (v1) | Skender 2.7.3 | TA-Lib 0.5.0 | Trady 3.2.8 | QuanTAlib 1.0.0 |
| --- | --- | --- | --- | --- | --- | --- |
| Sma | batch + streaming | batch only | batch only | batch only | batch only | batch + streaming |
| Ema | batch + streaming | batch only | batch only | batch only | batch only | batch + streaming |
| Rsi | batch + streaming | batch only | batch only | batch only | batch only | not shipped |
| Atr | batch + streaming | batch only | batch only | batch only | batch only | batch + streaming |
| BollingerBands | batch + streaming | batch only | batch only | batch only | batch only | not shipped |
| Macd | batch + streaming | batch only | batch only | batch only | batch only | not shipped |
| Stochastic | batch + streaming | batch only | batch only | batch only | shipped, not benchmarkable | not shipped |


## Numerical output

```text
Final value over 2000 seeded bars, per library:

SMA(20)
  OoplesV2    92.87448911474783
  OoplesV1    92.87448911474783
  Skender     92.87448911474789
  TaLib       92.87448911474794
  Trady       92.87448911474787
  QuanTAlib   92.87448911474786
  spread      1.1368683772161603E-13  (0.0000% of OoplesV2)

EMA(20)
  OoplesV2    92.89083698838029
  OoplesV1    92.89083698838029
  Skender     92.8908369883803
  TaLib       92.89083698838029
  Trady       92.89083698838029
  QuanTAlib   92.89083698838029
  spread      1.4210854715202004E-14  (0.0000% of OoplesV2)

RSI(14)
  OoplesV2    49.91645131158516
  OoplesV1    49.91645131158516
  Skender     49.91645131158519
  TaLib       49.91645131158514
  Trady       49.91645131158525
  spread      1.0658141036401503E-13  (0.0000% of OoplesV2)

ATR(14)
  OoplesV2    1.5183623134465687
  OoplesV1    1.5183623134465687
  Skender     1.518362313446557
  TaLib       1.5183623134465687
  Trady       1.518362313446556
  QuanTAlib   0.1352468850055664
  spread      1.3831154284410023  (91.0926% of OoplesV2)

BollingerBands(20,2) upper
  OoplesV2    94.51047456773419
  OoplesV1    94.51047456773419
  Skender     94.51047456773422
  TaLib       94.51047456772024
  Trady       94.5104745677342
  spread      1.3983481039758772E-11  (0.0000% of OoplesV2)

MACD(12,26,9) line
  OoplesV2    -0.5140232585825117
  OoplesV1    -0.5140232585825117
  Skender     -0.5140232585824975
  TaLib       -0.5140232585825117
  Trady       -0.5140232585824992
  spread      1.4210854715202004E-14  (0.0000% of OoplesV2)

Stochastic(14,3) %K
  OoplesV2    61.802156866586444
  OoplesV1    61.802156866586444
  Skender     51.135622477263695
  TaLib       51.1356224772631
  spread      10.666534389323346  (17.2592% of OoplesV2)

Control: ATR(14) where every true range is exactly 2, so the answer is 2
  OoplesV2    1.9999999999999987
  Skender     2
  TaLib       2
  Trady       2
  QuanTAlib   0.14285714285714285

Control: MACD(12,26,9) on a unit-slope ramp, so the answer is 7
  OoplesV2    7
  OoplesV1    7
  Skender     7
  TaLib       7
  Trady       6.999999999999999

Control: Stochastic %K on a strictly rising ramp, so the answer is 100
  OoplesV2    100
  OoplesV1    100
  Skender     100
  TaLib       100

Control: BollingerBands(20,2) upper minus middle on a unit-slope ramp, so the answer is 11.532562594670797 with a population sigma or 11.832159566199232 with a sample sigma
  OoplesV2    11.532562594670821
  OoplesV1    11.532562594670821
  Skender     11.532562594670821
  TaLib       11.532562594670821
  Trady       11.5325625946708

Control: Stochastic %K where the last three raw %K values are 100, 50 and 0, so the answer is 0 for a raw fast %K or 50 for a %K smoothed over 3
  OoplesV2    0
  OoplesV1    0
  Skender     50
  TaLib       50


```
