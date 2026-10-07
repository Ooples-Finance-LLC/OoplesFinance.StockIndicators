```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean        | Error        | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |------------:|-------------:|----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Tema** |   **323.27 μs** |    **18.790 μs** |  **1.030 μs** |  **1.00** |    **0.00** |  **32.2266** |   **7.8125** |        **-** |  **264.86 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Tema |    11.12 μs |     6.336 μs |  0.347 μs |  0.03 |    0.00 |   3.8605 |   0.1526 |        - |   31.54 KB |        0.12 |
|            |       |                      |             |              |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib.Functions.Tema** | **3,648.65 μs** | **1,444.333 μs** | **79.169 μs** |  **1.00** |    **0.03** | **679.6875** | **562.5000** | **496.0938** | **3711.84 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Tema |   110.17 μs |    46.233 μs |  2.534 μs |  0.03 |    0.00 |  38.0859 |   9.5215 |        - |  312.79 KB |        0.08 |
