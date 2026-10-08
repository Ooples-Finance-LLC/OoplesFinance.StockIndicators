```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId              | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |-------------------- |------------:|------------:|------------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Cmo** |  **8,651.8 μs** | **11,883.1 μs** |   **651.35 μs** |  **1.00** |    **0.09** |         **-** |         **-** |  **2823.75 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Cmo |    130.9 μs |    117.2 μs |     6.43 μs |  0.02 |    0.00 |         - |         - |    38.91 KB |        0.01 |
|            |       |                     |             |             |             |       |         |           |           |             |             |
| **Ooples**     | **10000** | **TaLib.Functions.Cmo** | **25,001.5 μs** | **40,203.4 μs** | **2,203.68 μs** |  **1.01** |    **0.11** | **3000.0000** | **1000.0000** | **29599.13 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Cmo |    299.9 μs |    532.2 μs |    29.17 μs |  0.01 |    0.00 |         - |         - |   324.73 KB |        0.01 |
