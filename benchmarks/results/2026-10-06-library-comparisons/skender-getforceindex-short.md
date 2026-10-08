```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean       | Error       | StdDev      | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|------------:|------------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Skend(...)Index [21]** | **4,023.7 μs** | **11,279.9 μs** |   **618.29 μs** |  **1.01** |    **0.18** |  **481.81 KB** |        **1.00** |
| Competitor | 1000  | Skend(...)Index [21] |   615.9 μs |    239.9 μs |    13.15 μs |  0.16 |    0.02 |  166.65 KB |        0.35 |
|            |       |                      |            |             |             |       |         |            |             |
| **Ooples**     | **10000** | **Skend(...)Index [21]** | **9,197.5 μs** | **40,824.5 μs** | **2,237.73 μs** |  **1.04** |    **0.29** | **5860.54 KB** |        **1.00** |
| Competitor | 10000 | Skend(...)Index [21] | 1,714.0 μs |  5,612.3 μs |   307.63 μs |  0.19 |    0.05 | 1614.31 KB |        0.28 |
