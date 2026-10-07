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
| **Ooples**     | **1000**  | **TaLib(...)yDoji [27]** |  **2,273.6 μs** |  **1,824.3 μs** | **100.00 μs** |  **1.00** |    **0.05** |  **265.12 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)yDoji [27] |    740.0 μs |    163.0 μs |   8.94 μs |  0.33 |    0.01 |   17.61 KB |        0.07 |
|            |       |                      |             |             |           |       |         |            |             |
| **Ooples**     | **10000** | **TaLib(...)yDoji [27]** | **13,630.6 μs** | **17,968.1 μs** | **984.89 μs** |  **1.00** |    **0.09** | **3713.48 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)yDoji [27] |    601.7 μs |    308.5 μs |  16.91 μs |  0.04 |    0.00 |  122.14 KB |        0.03 |
