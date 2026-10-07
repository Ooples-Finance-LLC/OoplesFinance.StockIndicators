```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean       | Error        | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|-------------:|----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)arami [24]** |   **150.3 μs** |     **31.73 μs** |   **1.74 μs** |  **1.00** |    **0.01** |  **32.2266** |   **8.0566** |        **-** |  **264.38 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)arami [24] |   532.0 μs |    120.79 μs |   6.62 μs |  3.54 |    0.05 |  89.8438 |  29.2969 |        - |  735.36 KB |        2.78 |
|            |       |                      |            |              |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **Trady(...)arami [24]** | **1,797.0 μs** |    **603.34 μs** |  **33.07 μs** |  **1.00** |    **0.02** | **533.2031** | **423.8281** | **351.5625** | **3709.79 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)arami [24] | 9,486.5 μs | 13,891.19 μs | 761.42 μs |  5.28 |    0.38 | 953.1250 | 609.3750 | 312.5000 | 7216.56 KB |        1.95 |
