```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |------------:|------------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)ubozu [29]** |  **2,695.6 μs** |  **1,867.8 μs** | **102.38 μs** |  **1.00** |    **0.05** |  **266.16 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)ubozu [29] |    871.4 μs |    246.6 μs |  13.52 μs |  0.32 |    0.01 |   16.63 KB |        0.06 |
|            |       |                      |             |             |           |       |         |            |             |
| **Ooples**     | **10000** | **TaLib(...)ubozu [29]** | **22,283.2 μs** | **14,910.0 μs** | **817.27 μs** |  **1.00** |    **0.05** | **3714.24 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)ubozu [29] |    768.7 μs |    526.4 μs |  28.86 μs |  0.03 |    0.00 |   122.8 KB |        0.03 |
