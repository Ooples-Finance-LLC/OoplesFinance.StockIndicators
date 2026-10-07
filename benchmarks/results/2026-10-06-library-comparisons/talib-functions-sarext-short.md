```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |------------:|-------------:|------------:|------:|--------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)arExt [22]** | **12,142.5 μs** |  **11,845.0 μs** |   **649.27 μs** |  **1.00** |    **0.07** |         **-** |   **3579.7 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)arExt [22] |    149.2 μs |     179.5 μs |     9.84 μs |  0.01 |    0.00 |         - |    38.63 KB |        0.01 |
|            |       |                      |             |              |             |       |         |           |             |             |
| **Ooples**     | **10000** | **TaLib(...)arExt [22]** | **57,973.3 μs** | **101,573.0 μs** | **5,567.56 μs** | **1.006** |    **0.12** | **4000.0000** | **35860.11 KB** |       **1.000** |
| Competitor | 10000 | TaLib(...)arExt [22] |    332.8 μs |     190.0 μs |    10.41 μs | 0.006 |    0.00 |         - |   328.66 KB |       0.009 |
