```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId          | Mean        | Error       | StdDev      | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |---------------- |------------:|------------:|------------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetVwma** |  **4,331.3 μs** | **10,704.7 μs** |   **586.76 μs** |  **1.01** |    **0.17** |   **608.3 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetVwma |    529.2 μs |    410.2 μs |    22.49 μs |  0.12 |    0.01 |  167.68 KB |        0.28 |
|            |       |                 |             |             |             |       |         |            |             |
| **Ooples**     | **10000** | **Skender.GetVwma** | **10,956.6 μs** | **47,463.1 μs** | **2,601.61 μs** |  **1.04** |    **0.31** | **7173.73 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetVwma |  1,180.7 μs |  7,416.6 μs |   406.53 μs |  0.11 |    0.04 | 1618.16 KB |        0.23 |
