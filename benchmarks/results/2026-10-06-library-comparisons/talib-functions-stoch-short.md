```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean        | Error       | StdDev      | Median      | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |------------:|------------:|------------:|------------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)Stoch [21]** |  **9,119.0 μs** |  **7,092.3 μs** |   **388.75 μs** |  **9,015.6 μs** |  **1.00** |    **0.05** |         **-** |         **-** |  **2549.08 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)Stoch [21] |    678.6 μs | 12,499.6 μs |   685.15 μs |    289.8 μs |  0.07 |    0.07 |         - |         - |    95.11 KB |        0.04 |
|            |       |                      |             |             |             |             |       |         |           |           |             |             |
| **Ooples**     | **10000** | **TaLib(...)Stoch [21]** | **23,816.5 μs** | **21,660.3 μs** | **1,187.27 μs** | **24,113.8 μs** |  **1.00** |    **0.06** | **2000.0000** | **1000.0000** | **26617.86 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)Stoch [21] |    494.2 μs |    903.0 μs |    49.50 μs |    516.8 μs |  0.02 |    0.00 |         - |         - |   886.13 KB |        0.03 |
