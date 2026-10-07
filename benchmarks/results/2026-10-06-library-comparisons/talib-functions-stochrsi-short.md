```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |------------:|------------:|------------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)chRsi [24]** | **12,504.5 μs** | **13,688.9 μs** |   **750.34 μs** |  **1.00** |    **0.07** |         **-** |         **-** |  **3800.08 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)chRsi [24] |    323.2 μs |    227.3 μs |    12.46 μs |  0.03 |    0.00 |         - |         - |    87.05 KB |        0.02 |
|            |       |                      |             |             |             |       |         |           |           |             |             |
| **Ooples**     | **10000** | **TaLib(...)chRsi [24]** | **32,205.3 μs** | **39,601.3 μs** | **2,170.68 μs** |  **1.00** |    **0.08** | **4000.0000** | **1000.0000** | **39380.66 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)chRsi [24] |    634.8 μs |  1,307.7 μs |    71.68 μs |  0.02 |    0.00 |         - |         - |   805.41 KB |        0.02 |
