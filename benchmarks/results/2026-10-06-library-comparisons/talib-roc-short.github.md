```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId              | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0      | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |-------------------- |-------------:|-------------:|-----------:|------:|--------:|----------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Roc** |   **471.605 μs** |    **59.279 μs** |  **3.2493 μs** | **1.000** |    **0.01** |   **67.3828** |  **20.0195** |        **-** |  **552.28 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Roc |     2.523 μs |     1.118 μs |  0.0613 μs | 0.005 |    0.00 |    1.9569 |   0.0420 |        - |   16.02 KB |        0.03 |
|            |       |                     |              |              |            |       |         |           |          |          |            |             |
| **Ooples**     | **10000** | **TaLib.Functions.Roc** | **5,248.119 μs** | **1,300.763 μs** | **71.2992 μs** | **1.000** |    **0.02** | **1031.2500** | **890.6250** | **492.1875** | **6619.64 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Roc |    23.085 μs |     5.060 μs |  0.2774 μs | 0.004 |    0.00 |   19.0430 |   3.1738 |        - |  156.64 KB |        0.02 |
