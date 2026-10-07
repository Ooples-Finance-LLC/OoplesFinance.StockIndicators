```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean        | Error       | StdDev      | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |------------:|------------:|------------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)Point [24]** |  **2,431.9 μs** |  **1,074.4 μs** |    **58.89 μs** |  **1.00** |    **0.03** |  **374.61 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)Point [24] |    174.4 μs |    109.0 μs |     5.98 μs |  0.07 |    0.00 |   31.55 KB |        0.08 |
|            |       |                      |             |             |             |       |         |            |             |
| **Ooples**     | **10000** | **TaLib(...)Point [24]** | **12,168.5 μs** | **52,312.0 μs** | **2,867.40 μs** |  **1.04** |    **0.33** | **4805.75 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)Point [24] |    884.5 μs |    583.0 μs |    31.96 μs |  0.08 |    0.02 |  279.24 KB |        0.06 |
