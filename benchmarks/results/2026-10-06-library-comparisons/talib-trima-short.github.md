```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean         | Error      | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|-----------:|-----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)Trima [21]** |   **203.330 μs** |  **38.338 μs** |  **2.1014 μs** |  **1.00** |    **0.01** |  **32.4707** |   **8.0566** |        **-** |  **265.81 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)Trima [21] |     3.662 μs |   2.011 μs |  0.1102 μs |  0.02 |    0.00 |   1.9531 |   0.0381 |        - |   16.02 KB |        0.06 |
|            |       |                      |              |            |            |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)Trima [21]** | **2,288.082 μs** | **586.858 μs** | **32.1677 μs** |  **1.00** |    **0.02** | **679.6875** | **562.5000** | **496.0938** | **3713.02 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)Trima [21] |    35.200 μs |   4.658 μs |  0.2553 μs |  0.02 |    0.00 |  19.0430 |   3.1738 |        - |  156.64 KB |        0.04 |
