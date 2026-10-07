```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId        | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0      | Allocated   | Alloc Ratio |
|----------- |------ |-------------- |------------:|------------:|------------:|------:|--------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Atr** |  **4,690.6 μs** |  **7,416.8 μs** |   **406.54 μs** |  **1.00** |    **0.10** |         **-** |  **1701.36 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Atr |    291.3 μs |    309.8 μs |    16.98 μs |  0.06 |    0.01 |         - |    92.66 KB |        0.05 |
|            |       |               |             |             |             |       |         |           |             |             |
| **Ooples**     | **10000** | **QuanTAlib.Atr** | **14,970.3 μs** | **32,821.7 μs** | **1,799.07 μs** |  **1.01** |    **0.15** | **1000.0000** | **18086.61 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Atr |    531.5 μs |  2,039.4 μs |   111.79 μs |  0.04 |    0.01 |         - |   866.07 KB |        0.05 |
