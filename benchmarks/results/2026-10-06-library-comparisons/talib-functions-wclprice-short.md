```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean         | Error        | StdDev     | Median       | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|-------------:|-----------:|-------------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)Price [24]** |  **1,929.20 μs** |  **1,952.75 μs** | **107.037 μs** |  **1,918.70 μs** |  **1.00** |    **0.07** |  **385.08 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)Price [24] |     36.40 μs |    119.51 μs |   6.551 μs |     36.30 μs |  0.02 |    0.00 |    21.6 KB |        0.06 |
|            |       |                      |              |              |            |              |       |         |            |             |
| **Ooples**     | **10000** | **TaLib(...)Price [24]** | **10,160.90 μs** | **14,918.57 μs** | **817.737 μs** | **10,169.90 μs** |  **1.00** |    **0.10** | **4886.53 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)Price [24] |    271.57 μs |  5,988.11 μs | 328.229 μs |     96.80 μs |  0.03 |    0.03 |  161.52 KB |        0.03 |
