```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0      | Allocated   | Alloc Ratio |
|----------- |------ |--------------- |------------:|------------:|------------:|------:|--------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetAdl** |  **7,752.0 μs** |  **4,353.9 μs** |   **238.65 μs** |  **1.00** |    **0.04** |         **-** |  **1129.02 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetAdl |    712.2 μs |    571.9 μs |    31.35 μs |  0.09 |    0.00 |         - |   272.67 KB |        0.24 |
|            |       |                |             |             |             |       |         |           |             |             |
| **Ooples**     | **10000** | **Skender.GetAdl** | **23,069.0 μs** | **29,688.2 μs** | **1,627.31 μs** |  **1.00** |    **0.09** | **1000.0000** | **13183.13 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetAdl |  1,923.7 μs |  2,409.0 μs |   132.04 μs |  0.08 |    0.01 |         - |   2649.3 KB |        0.20 |
