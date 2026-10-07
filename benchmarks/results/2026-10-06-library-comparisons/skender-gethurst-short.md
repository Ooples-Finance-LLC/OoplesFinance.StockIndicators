```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId           | Mean     | Error     | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |----------------- |---------:|----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetHurst** | **1.395 ms** | **8.1149 ms** | **0.4448 ms** |  **1.08** |    **0.44** |  **243.29 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetHurst | 3.367 ms | 6.5527 ms | 0.3592 ms |  2.60 |    0.79 |  499.38 KB |        2.05 |
|            |       |                  |          |           |           |       |         |            |             |
| **Ooples**     | **10000** | **Skender.GetHurst** | **3.801 ms** | **1.5361 ms** | **0.0842 ms** |  **1.00** |    **0.03** | **2446.46 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetHurst | 8.289 ms | 0.5553 ms | 0.0304 ms |  2.18 |    0.04 | 4982.41 KB |        2.04 |
