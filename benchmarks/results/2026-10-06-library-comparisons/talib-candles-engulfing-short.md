```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean         | Error       | StdDev     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|------------:|-----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)lfing [23]** |  **2,311.07 μs** |   **886.78 μs** |  **48.608 μs** |  **1.00** |    **0.03** |  **264.43 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)lfing [23] |     60.17 μs |    94.49 μs |   5.179 μs |  0.03 |    0.00 |   15.97 KB |        0.06 |
|            |       |                      |              |             |            |       |         |            |             |
| **Ooples**     | **10000** | **TaLib(...)lfing [23]** | **10,811.87 μs** | **2,996.16 μs** | **164.230 μs** |  **1.00** |    **0.02** | **3711.48 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)lfing [23] |    157.80 μs |   293.21 μs |  16.072 μs |  0.01 |    0.00 |   122.8 KB |        0.03 |
