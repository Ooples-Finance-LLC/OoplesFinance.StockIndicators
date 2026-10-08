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
| **Ooples**     | **1000**  | **TaLib(...)eDoji [28]** |  **2,282.4 μs** |  **1,557.7 μs** |  **85.38 μs** |  **1.00** |    **0.05** |  **265.12 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)eDoji [28] |    735.5 μs |    216.9 μs |  11.89 μs |  0.32 |    0.01 |    16.3 KB |        0.06 |
|            |       |                      |             |             |           |       |         |            |             |
| **Ooples**     | **10000** | **TaLib(...)eDoji [28]** | **14,358.0 μs** | **13,547.0 μs** | **742.56 μs** |  **1.00** |    **0.06** | **3712.21 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)eDoji [28] |    594.1 μs |    368.9 μs |  20.22 μs |  0.04 |    0.00 |  122.75 KB |        0.03 |
