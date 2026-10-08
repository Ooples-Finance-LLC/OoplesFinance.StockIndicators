```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean       | Error        | StdDev      | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|-------------:|------------:|-----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)ammer [28]** | **2,405.4 μs** |   **3,181.2 μs** |   **174.37 μs** | **2,364.9 μs** |  **1.00** |    **0.09** |  **266.38 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)ammer [28] | 1,330.7 μs |     104.2 μs |     5.71 μs | 1,331.7 μs |  0.56 |    0.03 |   17.33 KB |        0.07 |
|            |       |                      |            |              |             |            |       |         |            |             |
| **Ooples**     | **10000** | **TaLib(...)ammer [28]** | **8,351.9 μs** | **128,669.1 μs** | **7,052.79 μs** | **4,328.1 μs** |  **1.47** |    **1.39** | **3714.74 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)ammer [28] |   990.1 μs |     234.5 μs |    12.85 μs |   985.6 μs |  0.17 |    0.09 |  122.09 KB |        0.03 |
