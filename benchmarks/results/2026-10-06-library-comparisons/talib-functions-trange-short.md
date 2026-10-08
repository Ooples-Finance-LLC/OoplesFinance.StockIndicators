```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean         | Error       | StdDev     | Median      | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|------------:|-----------:|------------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)Range [22]** |  **1,812.40 μs** | **3,068.41 μs** | **168.190 μs** | **1,860.50 μs** |  **1.01** |    **0.12** |  **299.04 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)Range [22] |     32.00 μs |   112.89 μs |   6.188 μs |    30.80 μs |  0.02 |    0.00 |    21.6 KB |        0.07 |
|            |       |                      |              |             |            |             |       |         |            |             |
| **Ooples**     | **10000** | **TaLib(...)Range [22]** | **10,083.03 μs** | **6,483.50 μs** | **355.382 μs** | **9,958.60 μs** |  **1.00** |    **0.04** | **4029.02 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)Range [22] |    296.27 μs | 5,867.04 μs | 321.592 μs |   125.90 μs |  0.03 |    0.03 |  161.85 KB |        0.04 |
