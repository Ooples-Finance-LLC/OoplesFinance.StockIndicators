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
| **Ooples**     | **1000**  | **TaLib(...)Index [24]** |  **2,231.33 μs** | **1,894.01 μs** | **103.817 μs** |  **1.00** |    **0.06** |  **319.73 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)Index [24] |     87.23 μs |   117.91 μs |   6.463 μs |  0.04 |    0.00 |   32.82 KB |        0.10 |
|            |       |                      |              |             |            |       |         |            |             |
| **Ooples**     | **10000** | **TaLib(...)Index [24]** | **10,276.27 μs** | **5,816.18 μs** | **318.804 μs** |  **1.00** |    **0.04** | **4259.95 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)Index [24] |    147.27 μs |   421.53 μs |  23.105 μs |  0.01 |    0.00 |  279.62 KB |        0.07 |
