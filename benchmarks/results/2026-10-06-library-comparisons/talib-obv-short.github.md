```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId              | Mean         | Error         | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |-------------------- |-------------:|--------------:|-----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Obv** |   **188.035 μs** |   **129.2263 μs** |  **7.0833 μs** | **1.001** |    **0.05** |  **34.1797** |  **10.0098** |        **-** |  **280.69 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Obv |     1.772 μs |     0.7366 μs |  0.0404 μs | 0.009 |    0.00 |   0.9956 |   0.0267 |        - |    8.14 KB |        0.03 |
|            |       |                     |              |               |            |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib.Functions.Obv** | **2,205.011 μs** | **1,326.3088 μs** | **72.6995 μs** |  **1.00** |    **0.04** | **699.2188** | **562.5000** | **496.0938** | **3887.33 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Obv |    36.956 μs |    16.9292 μs |  0.9279 μs |  0.02 |    0.00 |   9.5215 |   1.5869 |        - |   78.45 KB |        0.02 |
