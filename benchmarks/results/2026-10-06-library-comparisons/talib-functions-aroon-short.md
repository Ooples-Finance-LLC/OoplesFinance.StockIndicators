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
| **Ooples**     | **1000**  | **TaLib(...)Aroon [21]** | **2,692.2 μs** |  **2,869.2 μs** |   **157.27 μs** |  **1.00** |    **0.07** |  **518.25 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)Aroon [21] |   277.1 μs |    345.8 μs |    18.95 μs |  0.10 |    0.01 |   86.37 KB |        0.17 |
|            |       |                      |            |             |             |       |         |            |             |
| **Ooples**     | **10000** | **TaLib(...)Aroon [21]** | **5,775.2 μs** | **25,952.1 μs** | **1,422.52 μs** |  **1.05** |    **0.34** | **6233.58 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)Aroon [21] |   402.6 μs |    740.9 μs |    40.61 μs |  0.07 |    0.02 |  803.84 KB |        0.13 |
