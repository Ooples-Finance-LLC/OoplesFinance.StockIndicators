```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId              | Mean         | Error        | StdDev    | Median       | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |-------------------- |-------------:|-------------:|----------:|-------------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Min** |  **1,919.00 μs** |  **2,675.44 μs** | **146.65 μs** |  **1,959.00 μs** |  **1.00** |    **0.10** |  **369.86 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Min |     98.20 μs |    836.16 μs |  45.83 μs |     73.10 μs |  0.05 |    0.02 |   21.74 KB |        0.06 |
|            |       |                     |              |              |           |              |       |         |            |             |
| **Ooples**     | **10000** | **TaLib.Functions.Min** | **10,328.03 μs** | **12,510.38 μs** | **685.74 μs** | **10,596.40 μs** |  **1.00** |    **0.08** | **4732.33 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Min |    130.50 μs |    300.31 μs |  16.46 μs |    135.20 μs |  0.01 |    0.00 |  161.66 KB |        0.03 |
