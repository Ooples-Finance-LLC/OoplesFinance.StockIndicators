```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId              | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |-------------------- |-------------:|------------:|-----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Mom** |   **193.782 μs** |  **62.0238 μs** |  **3.3997 μs** |  **1.00** |    **0.02** |  **32.2266** |   **8.0566** |        **-** |  **264.96 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Mom |     2.029 μs |   0.3432 μs |  0.0188 μs |  0.01 |    0.00 |   1.9569 |   0.0420 |        - |   16.02 KB |        0.06 |
|            |       |                     |              |             |            |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib.Functions.Mom** | **2,209.985 μs** | **542.9733 μs** | **29.7622 μs** | **1.000** |    **0.02** | **679.6875** | **570.3125** | **496.0938** | **3712.43 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Mom |    21.809 μs |  23.8069 μs |  1.3049 μs | 0.010 |    0.00 |  19.0430 |   3.1738 |        - |  156.64 KB |        0.04 |
