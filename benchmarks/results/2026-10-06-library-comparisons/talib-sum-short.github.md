```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId              | Mean         | Error         | StdDev      | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |-------------------- |-------------:|--------------:|------------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Sum** |   **182.660 μs** |    **64.1951 μs** |   **3.5188 μs** |  **1.00** |    **0.02** |  **32.2266** |   **8.0566** |        **-** |  **265.06 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Sum |     2.897 μs |     4.0132 μs |   0.2200 μs |  0.02 |    0.00 |   1.9569 |   0.0420 |        - |   16.02 KB |        0.06 |
|            |       |                     |              |               |             |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib.Functions.Sum** | **2,071.184 μs** | **2,073.9510 μs** | **113.6803 μs** |  **1.00** |    **0.07** | **679.6875** | **574.2188** | **496.0938** | **3712.26 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Sum |    25.151 μs |     0.7527 μs |   0.0413 μs |  0.01 |    0.00 |  19.0430 |   3.1738 |        - |  156.64 KB |        0.04 |
