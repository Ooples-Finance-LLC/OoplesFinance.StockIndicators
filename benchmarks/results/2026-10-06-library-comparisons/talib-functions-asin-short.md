```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean       | Error      | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|-----------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Asin** | **2,687.8 μs** | **8,684.1 μs** | **476.01 μs** | **2,421.0 μs** |  **1.02** |    **0.21** |  **305.16 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Asin |   337.8 μs | 6,889.0 μs | 377.61 μs |   123.7 μs |  0.13 |    0.13 |   38.68 KB |        0.13 |
|            |       |                      |            |            |           |            |       |         |            |             |
| **Ooples**     | **10000** | **TaLib.Functions.Asin** | **3,023.3 μs** | **6,389.0 μs** | **350.20 μs** | **3,211.6 μs** |  **1.01** |    **0.15** | **4114.58 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Asin |   302.6 μs |   397.6 μs |  21.79 μs |   293.2 μs |  0.10 |    0.01 |  328.72 KB |        0.08 |
