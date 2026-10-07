```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------- |------------:|------------:|------------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetRsi** |  **8,244.1 μs** | **11,652.5 μs** |   **638.71 μs** |  **1.00** |    **0.10** |         **-** |         **-** |  **2795.06 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetRsi |    545.0 μs |    237.1 μs |    12.99 μs |  0.07 |    0.00 |         - |         - |   128.37 KB |        0.05 |
|            |       |                |             |             |             |       |         |           |           |             |             |
| **Ooples**     | **10000** | **Skender.GetRsi** | **21,199.7 μs** | **22,192.8 μs** | **1,216.46 μs** |  **1.00** |    **0.07** | **3000.0000** | **1000.0000** | **29314.14 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetRsi |  1,073.3 μs |  2,742.0 μs |   150.30 μs |  0.05 |    0.01 |         - |         - |  1222.17 KB |        0.04 |
