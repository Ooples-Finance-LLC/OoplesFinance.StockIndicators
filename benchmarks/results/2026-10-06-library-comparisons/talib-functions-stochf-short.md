```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean        | Error       | StdDev      | Median      | Ratio | RatioSD | Gen0      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |------------:|------------:|------------:|------------:|------:|--------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)tochF [22]** |  **7,329.3 μs** | **12,671.7 μs** |   **694.58 μs** |  **6,962.2 μs** |  **1.01** |    **0.11** |         **-** |  **1772.95 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)tochF [22] |    672.3 μs | 12,372.9 μs |   678.20 μs |    299.1 μs |  0.09 |    0.08 |         - |    94.75 KB |        0.05 |
|            |       |                      |             |             |             |             |       |         |           |             |             |
| **Ooples**     | **10000** | **TaLib(...)tochF [22]** | **18,973.6 μs** | **47,495.5 μs** | **2,603.39 μs** | **18,178.0 μs** |  **1.01** |    **0.17** | **1000.0000** | **18803.41 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)tochF [22] |    450.1 μs |  1,397.5 μs |    76.60 μs |    411.3 μs |  0.02 |    0.00 |         - |   886.09 KB |        0.05 |
