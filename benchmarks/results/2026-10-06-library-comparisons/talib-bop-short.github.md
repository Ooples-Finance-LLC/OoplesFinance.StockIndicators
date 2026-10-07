```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId              | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0      | Allocated   | Alloc Ratio |
|----------- |------ |-------------------- |-------------:|-------------:|-----------:|------:|--------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Bop** |  **4,105.87 μs** |  **4,163.75 μs** | **228.229 μs** | **1.002** |    **0.07** |         **-** |   **955.88 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Bop |     28.63 μs |     94.87 μs |   5.200 μs | 0.007 |    0.00 |         - |    13.68 KB |        0.01 |
|            |       |                     |              |              |            |       |         |           |             |             |
| **Ooples**     | **10000** | **TaLib.Functions.Bop** | **12,590.45 μs** | **13,653.28 μs** | **748.382 μs** | **1.002** |    **0.07** | **1000.0000** | **10437.52 KB** |       **1.000** |
| Competitor | 10000 | TaLib.Functions.Bop |     52.53 μs |     23.53 μs |   1.290 μs | 0.004 |    0.00 |         - |    83.71 KB |       0.008 |
