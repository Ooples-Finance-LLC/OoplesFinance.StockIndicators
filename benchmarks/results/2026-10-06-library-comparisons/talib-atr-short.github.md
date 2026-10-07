```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId              | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0      | Gen1      | Allocated  | Alloc Ratio |
|----------- |------ |-------------------- |-------------:|-------------:|-----------:|------:|--------:|----------:|----------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Atr** |  **4,820.37 μs** | **10,955.99 μs** | **600.535 μs** | **1.010** |    **0.15** |         **-** |         **-** | **1825.58 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Atr |     46.77 μs |     84.05 μs |   4.607 μs | 0.010 |    0.00 |         - |         - |   28.48 KB |        0.02 |
|            |       |                     |              |              |            |       |         |           |           |            |             |
| **Ooples**     | **10000** | **TaLib.Functions.Atr** | **13,258.37 μs** | **13,451.12 μs** | **737.301 μs** |  **1.00** |    **0.07** | **2000.0000** | **1000.0000** | **19335.5 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Atr |    175.17 μs |    452.84 μs |  24.822 μs |  0.01 |    0.00 |         - |         - |  236.84 KB |        0.01 |
