```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |------------:|------------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)Index [24]** |  **2,176.9 μs** |  **1,842.1 μs** | **100.97 μs** |  **1.00** |    **0.06** |  **319.73 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)Index [24] |    113.9 μs |    182.9 μs |  10.02 μs |  0.05 |    0.00 |   32.16 KB |        0.10 |
|            |       |                      |             |             |           |       |         |            |             |
| **Ooples**     | **10000** | **TaLib(...)Index [24]** | **11,059.1 μs** | **11,338.7 μs** | **621.51 μs** |  **1.00** |    **0.07** | **4259.63 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)Index [24] |    159.9 μs |    485.6 μs |  26.62 μs |  0.01 |    0.00 |  279.24 KB |        0.07 |
