```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean        | Error        | StdDev      | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |------------:|-------------:|------------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **QuanT(...)ution [21]** | **14,230.1 μs** |   **2,953.6 μs** |   **161.90 μs** |  **1.00** |    **0.01** |  **538.02 KB** |        **1.00** |
| Competitor | 1000  | QuanT(...)ution [21] |    392.4 μs |     416.6 μs |    22.84 μs |  0.03 |    0.00 |  220.15 KB |        0.41 |
|            |       |                      |             |              |             |       |         |            |             |
| **Ooples**     | **10000** | **QuanT(...)ution [21]** | **20,701.0 μs** | **101,160.8 μs** | **5,544.96 μs** |  **1.04** |    **0.33** | **6439.27 KB** |        **1.00** |
| Competitor | 10000 | QuanT(...)ution [21] |  1,027.8 μs |   3,186.7 μs |   174.67 μs |  0.05 |    0.01 | 2179.01 KB |        0.34 |
