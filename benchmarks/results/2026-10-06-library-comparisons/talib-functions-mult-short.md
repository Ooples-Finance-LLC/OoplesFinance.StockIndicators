```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |------------:|-----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Mult** |  **2,437.4 μs** | **9,865.8 μs** | **540.78 μs** |  **1.03** |    **0.27** |  **305.42 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Mult |    100.9 μs |   114.1 μs |   6.26 μs |  0.04 |    0.01 |   39.29 KB |        0.13 |
|            |       |                      |             |            |           |       |         |            |             |
| **Ooples**     | **10000** | **TaLib.Functions.Mult** | **10,386.7 μs** | **6,326.2 μs** | **346.76 μs** |  **1.00** |    **0.04** | **4113.85 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Mult |    212.5 μs |   345.0 μs |  18.91 μs |  0.02 |    0.00 |     329 KB |        0.08 |
