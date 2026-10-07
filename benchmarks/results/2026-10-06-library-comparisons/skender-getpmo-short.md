```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------- |------------:|------------:|----------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetPmo** | **17,569.9 μs** | **12,759.9 μs** | **699.41 μs** |  **1.00** |    **0.05** |         **-** |         **-** |  **5727.41 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetPmo |    978.3 μs |    731.7 μs |  40.11 μs |  0.06 |    0.00 |         - |         - |   271.43 KB |        0.05 |
|            |       |                |             |             |           |       |         |           |           |             |             |
| **Ooples**     | **10000** | **Skender.GetPmo** | **45,690.8 μs** |  **1,061.9 μs** |  **58.21 μs** |  **1.00** |    **0.00** | **6000.0000** | **1000.0000** | **59500.15 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetPmo |  2,053.1 μs |  9,818.2 μs | 538.17 μs |  0.04 |    0.01 |         - |         - |   2744.6 KB |        0.05 |
