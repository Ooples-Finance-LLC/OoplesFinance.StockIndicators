```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean        | Error        | StdDev      | Median      | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |------------:|-------------:|------------:|------------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Mavp** | **21,459.4 μs** |  **13,511.9 μs** |   **740.63 μs** | **21,798.7 μs** |  **1.00** |    **0.04** |         **-** |         **-** |  **7468.97 KB** |       **1.000** |
| Competitor | 1000  | TaLib.Functions.Mavp |    905.5 μs |     927.0 μs |    50.81 μs |    902.7 μs |  0.04 |    0.00 |         - |         - |    58.56 KB |       0.008 |
|            |       |                      |             |              |             |             |       |         |           |           |             |             |
| **Ooples**     | **10000** | **TaLib.Functions.Mavp** | **63,647.9 μs** | **113,970.6 μs** | **6,247.11 μs** | **67,022.5 μs** |  **1.01** |    **0.12** | **9000.0000** | **1000.0000** | **79972.62 KB** |       **1.000** |
| Competitor | 10000 | TaLib.Functions.Mavp |  1,226.7 μs |  15,502.5 μs |   849.74 μs |    765.2 μs |  0.02 |    0.01 |         - |         - |   524.38 KB |       0.007 |
