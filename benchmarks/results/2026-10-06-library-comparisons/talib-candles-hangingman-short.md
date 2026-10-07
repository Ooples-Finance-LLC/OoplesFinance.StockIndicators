```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean     | Error     | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |---------:|----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)ngMan [24]** | **2.619 ms** | **1.7446 ms** | **0.0956 ms** |  **1.00** |    **0.04** |  **266.99 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)ngMan [24] | 1.411 ms | 0.1993 ms | 0.0109 ms |  0.54 |    0.02 |   17.33 KB |        0.06 |
|            |       |                      |          |           |           |       |         |            |             |
| **Ooples**     | **10000** | **TaLib(...)ngMan [24]** | **4.691 ms** | **7.2378 ms** | **0.3967 ms** |  **1.01** |    **0.11** | **3715.35 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)ngMan [24] | 1.217 ms | 0.9080 ms | 0.0498 ms |  0.26 |    0.02 |  120.78 KB |        0.03 |
