```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean        | Error       | StdDev      | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |------------:|------------:|------------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)tHold [22]** |  **2,670.8 μs** |  **2,223.2 μs** |   **121.86 μs** |  **1.00** |    **0.06** |  **266.16 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)tHold [22] |    896.4 μs |    163.1 μs |     8.94 μs |  0.34 |    0.01 |   17.61 KB |        0.07 |
|            |       |                      |             |             |             |       |         |            |             |
| **Ooples**     | **10000** | **TaLib(...)tHold [22]** | **25,425.2 μs** | **73,941.9 μs** | **4,053.00 μs** |  **1.02** |    **0.19** | **3714.57 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)tHold [22] |    751.2 μs |    434.0 μs |    23.79 μs |  0.03 |    0.00 |   122.8 KB |        0.03 |
