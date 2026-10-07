```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId           | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0      | Allocated   | Alloc Ratio |
|----------- |------ |----------------- |------------:|------------:|------------:|------:|--------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetRenko** |  **2,160.4 μs** |  **3,970.6 μs** |   **217.64 μs** |  **1.01** |    **0.12** |         **-** |  **1032.64 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetRenko |    787.4 μs |  1,817.0 μs |    99.60 μs |  0.37 |    0.05 |         - |    79.95 KB |        0.08 |
|            |       |                  |             |             |             |       |         |           |             |             |
| **Ooples**     | **10000** | **Skender.GetRenko** | **16,082.7 μs** | **61,703.8 μs** | **3,382.19 μs** |  **1.03** |    **0.28** | **1000.0000** | **10311.96 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetRenko |  2,004.5 μs |  1,943.7 μs |   106.54 μs |  0.13 |    0.03 |         - |   729.84 KB |        0.07 |
