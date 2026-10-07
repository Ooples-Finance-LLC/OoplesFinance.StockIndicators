```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId              | Mean       | Error      | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |-------------------- |-----------:|-----------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Exp** | **2,428.5 μs** | **8,438.8 μs** | **462.56 μs** | **2,203.0 μs** |  **1.02** |    **0.23** |  **305.16 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Exp |   289.3 μs | 5,964.5 μs | 326.94 μs |   103.2 μs |  0.12 |    0.12 |   37.32 KB |        0.12 |
|            |       |                     |            |            |           |            |       |         |            |             |
| **Ooples**     | **10000** | **TaLib.Functions.Exp** | **3,244.6 μs** | **7,577.3 μs** | **415.34 μs** | **3,234.8 μs** |  **1.01** |    **0.16** | **4110.31 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Exp |   257.2 μs |   402.2 μs |  22.05 μs |   247.0 μs |  0.08 |    0.01 |  328.02 KB |        0.08 |
