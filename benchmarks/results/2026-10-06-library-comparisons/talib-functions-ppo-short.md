```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId              | Mean        | Error        | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |-------------------- |------------:|-------------:|----------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Ppo** | **14,226.7 μs** | **16,430.85 μs** | **900.63 μs** |  **1.00** |    **0.08** |         **-** |         **-** |  **4009.59 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Ppo |    635.6 μs |     99.64 μs |   5.46 μs |  0.04 |    0.00 |         - |         - |     47.2 KB |        0.01 |
|            |       |                     |             |              |           |       |         |           |           |             |             |
| **Ooples**     | **10000** | **TaLib.Functions.Ppo** | **33,894.5 μs** |  **9,420.61 μs** | **516.38 μs** | **1.000** |    **0.02** | **4000.0000** | **1000.0000** | **41676.71 KB** |       **1.000** |
| Competitor | 10000 | TaLib.Functions.Ppo |    289.2 μs |    552.83 μs |  30.30 μs | 0.009 |    0.00 |         - |         - |   403.33 KB |       0.010 |
