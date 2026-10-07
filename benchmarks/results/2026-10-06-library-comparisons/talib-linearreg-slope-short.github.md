```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean         | Error        | StdDev      | Median       | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|-------------:|------------:|-------------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)Slope [30]** |  **5,772.17 μs** | **10,483.38 μs** |   **574.63 μs** |  **5,473.90 μs** | **1.006** |    **0.12** |         **-** |         **-** |  **2541.51 KB** |       **1.000** |
| Competitor | 1000  | TaLib(...)Slope [30] |     56.37 μs |    323.99 μs |    17.76 μs |     48.50 μs | 0.010 |    0.00 |         - |         - |    20.23 KB |       0.008 |
|            |       |                      |              |              |             |              |       |         |           |           |             |             |
| **Ooples**     | **10000** | **TaLib(...)Slope [30]** | **18,271.95 μs** | **81,612.79 μs** | **4,473.47 μs** | **15,702.05 μs** |  **1.04** |    **0.29** | **2000.0000** | **1000.0000** | **26588.24 KB** |       **1.000** |
| Competitor | 10000 | TaLib(...)Slope [30] |    410.30 μs |  5,989.84 μs |   328.32 μs |    223.70 μs |  0.02 |    0.02 |         - |         - |   161.23 KB |       0.006 |
