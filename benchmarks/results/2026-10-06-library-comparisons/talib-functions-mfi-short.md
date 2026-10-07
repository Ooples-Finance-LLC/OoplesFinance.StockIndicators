```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId              | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |-------------------- |------------:|------------:|----------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Mfi** | **13,495.7 μs** | **13,335.0 μs** | **730.94 μs** |  **1.00** |    **0.07** |         **-** |         **-** |  **4880.66 KB** |       **1.000** |
| Competitor | 1000  | TaLib.Functions.Mfi |    154.6 μs |    114.7 μs |   6.29 μs |  0.01 |    0.00 |         - |         - |    38.31 KB |       0.008 |
|            |       |                     |             |             |           |       |         |           |           |             |             |
| **Ooples**     | **10000** | **TaLib.Functions.Mfi** | **40,692.7 μs** | **13,430.5 μs** | **736.17 μs** | **1.000** |    **0.02** | **5000.0000** | **1000.0000** | **50540.09 KB** |       **1.000** |
| Competitor | 10000 | TaLib.Functions.Mfi |    289.1 μs |    868.4 μs |  47.60 μs | 0.007 |    0.00 |         - |         - |   328.68 KB |       0.007 |
