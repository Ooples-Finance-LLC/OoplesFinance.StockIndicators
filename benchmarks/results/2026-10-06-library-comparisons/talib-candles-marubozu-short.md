```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|------------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)ubozu [22]** | **2,737.5 μs** |  **1,240.6 μs** |  **68.00 μs** |  **1.00** |    **0.03** |  **266.16 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)ubozu [22] |   896.2 μs |    165.5 μs |   9.07 μs |  0.33 |    0.01 |   16.63 KB |        0.06 |
|            |       |                      |            |             |           |       |         |            |             |
| **Ooples**     | **10000** | **TaLib(...)ubozu [22]** | **5,316.3 μs** | **15,948.4 μs** | **874.18 μs** |  **1.02** |    **0.20** | **3713.59 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)ubozu [22] |   772.0 μs |    396.0 μs |  21.71 μs |  0.15 |    0.02 |  120.83 KB |        0.03 |
