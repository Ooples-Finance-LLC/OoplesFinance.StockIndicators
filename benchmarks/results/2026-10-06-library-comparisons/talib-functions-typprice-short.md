```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean         | Error       | StdDev     | Median       | Ratio | RatioSD | Allocated | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|------------:|-----------:|-------------:|------:|--------:|----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)Price [24]** |  **1,889.93 μs** |   **568.37 μs** |  **31.154 μs** |  **1,874.40 μs** |  **1.00** |    **0.02** | **385.08 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)Price [24] |     28.87 μs |    75.76 μs |   4.153 μs |     30.80 μs |  0.02 |    0.00 |  21.23 KB |        0.06 |
|            |       |                      |              |             |            |              |       |         |           |             |
| **Ooples**     | **10000** | **TaLib(...)Price [24]** | **10,463.27 μs** | **6,862.45 μs** | **376.154 μs** | **10,307.40 μs** |  **1.00** |    **0.04** | **4888.5 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)Price [24] |    275.37 μs | 6,020.81 μs | 330.021 μs |     93.90 μs |  0.03 |    0.03 | 157.35 KB |        0.03 |
