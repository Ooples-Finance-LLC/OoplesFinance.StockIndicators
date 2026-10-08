```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean         | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|-------------:|------------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Dema** |   **281.914 μs** |   **148.808 μs** |   **8.1567 μs** |  **1.00** |    **0.04** |  **32.2266** |   **8.7891** |        **-** |  **264.76 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Dema |     9.319 μs |     3.091 μs |   0.1694 μs |  0.03 |    0.00 |   3.8605 |   0.1526 |        - |   31.54 KB |        0.12 |
|            |       |                      |              |              |             |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib.Functions.Dema** | **3,381.309 μs** | **5,880.596 μs** | **322.3353 μs** |  **1.01** |    **0.12** | **679.6875** | **570.3125** | **496.0938** | **3712.31 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Dema |    87.472 μs |     5.503 μs |   0.3017 μs |  0.03 |    0.00 |  38.0859 |   9.5215 |        - |  312.79 KB |        0.08 |
