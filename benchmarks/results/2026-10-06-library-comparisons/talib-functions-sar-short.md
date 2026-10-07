```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId              | Mean        | Error        | StdDev      | Median      | Ratio | RatioSD | Gen0      | Allocated   | Alloc Ratio |
|----------- |------ |-------------------- |------------:|-------------:|------------:|------------:|------:|--------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Sar** | **12,246.6 μs** | **14,131.40 μs** |   **774.59 μs** | **12,514.6 μs** |  **1.00** |    **0.08** |         **-** |   **3579.7 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Sar |    133.2 μs |     87.28 μs |     4.78 μs |    131.9 μs |  0.01 |    0.00 |         - |    37.92 KB |        0.01 |
|            |       |                     |             |              |             |             |       |         |           |             |             |
| **Ooples**     | **10000** | **TaLib.Functions.Sar** | **67,133.8 μs** | **23,119.49 μs** | **1,267.26 μs** | **66,702.8 μs** |  **1.00** |    **0.02** | **4000.0000** | **35859.17 KB** |       **1.000** |
| Competitor | 10000 | TaLib.Functions.Sar |    884.2 μs | 17,908.99 μs |   981.65 μs |    333.4 μs |  0.01 |    0.01 |         - |   323.09 KB |       0.009 |
