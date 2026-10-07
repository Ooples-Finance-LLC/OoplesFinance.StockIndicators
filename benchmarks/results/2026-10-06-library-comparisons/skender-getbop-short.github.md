```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean        | Error       | StdDev      | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------- |------------:|------------:|------------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetBop** |  **3,752.1 μs** |  **8,773.8 μs** |   **480.92 μs** |  **1.01** |    **0.15** |   **619.9 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetBop |    539.4 μs |    733.4 μs |    40.20 μs |  0.15 |    0.02 |  174.93 KB |        0.28 |
|            |       |                |             |             |             |       |         |            |             |
| **Ooples**     | **10000** | **Skender.GetBop** | **23,939.6 μs** | **50,256.1 μs** | **2,754.71 μs** |  **1.01** |    **0.14** | **7243.91 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetBop |  1,114.6 μs |  1,185.3 μs |    64.97 μs |  0.05 |    0.01 | 1696.38 KB |        0.23 |
