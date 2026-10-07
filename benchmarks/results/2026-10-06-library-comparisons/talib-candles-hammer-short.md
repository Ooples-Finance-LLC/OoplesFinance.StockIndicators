```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean     | Error      | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |---------:|-----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Candles.Hammer** | **2.607 ms** |  **1.8374 ms** | **0.1007 ms** |  **1.00** |    **0.05** |  **266.99 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Candles.Hammer | 1.428 ms |  0.1195 ms | 0.0065 ms |  0.55 |    0.02 |   17.61 KB |        0.07 |
|            |       |                      |          |            |           |       |         |            |             |
| **Ooples**     | **10000** | **TaLib.Candles.Hammer** | **6.531 ms** | **11.1758 ms** | **0.6126 ms** |  **1.01** |    **0.12** | **3714.37 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Candles.Hammer | 1.225 ms |  0.8500 ms | 0.0466 ms |  0.19 |    0.02 |  121.48 KB |        0.03 |
