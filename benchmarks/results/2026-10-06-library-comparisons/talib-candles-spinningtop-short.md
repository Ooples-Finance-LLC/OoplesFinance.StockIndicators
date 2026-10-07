```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean       | Error      | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|-----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)ngTop [25]** | **2,692.0 μs** |   **806.1 μs** |  **44.19 μs** |  **1.00** |    **0.02** |  **266.16 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)ngTop [25] |   599.6 μs |   694.1 μs |  38.05 μs |  0.22 |    0.01 |   17.28 KB |        0.06 |
|            |       |                      |            |            |           |       |         |            |             |
| **Ooples**     | **10000** | **TaLib(...)ngTop [25]** | **4,950.8 μs** | **7,077.7 μs** | **387.95 μs** |  **1.00** |    **0.09** | **3714.57 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)ngTop [25] |   512.7 μs |   155.5 μs |   8.52 μs |  0.10 |    0.01 |  123.13 KB |        0.03 |
