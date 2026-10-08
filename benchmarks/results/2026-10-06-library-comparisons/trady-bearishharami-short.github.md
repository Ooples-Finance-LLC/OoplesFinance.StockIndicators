```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0      | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|------------:|----------:|------:|--------:|----------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)arami [31]** |   **152.7 μs** |    **68.03 μs** |   **3.73 μs** |  **1.00** |    **0.03** |   **32.2266** |   **8.0566** |        **-** |  **264.45 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)arami [31] |   689.9 μs |   222.62 μs |  12.20 μs |  4.52 |    0.12 |  121.0938 |  49.8047 |        - |  995.57 KB |        3.76 |
|            |       |                      |            |             |           |       |         |           |          |          |            |             |
| **Ooples**     | **10000** | **Trady(...)arami [31]** | **1,896.5 μs** | **1,241.19 μs** |  **68.03 μs** |  **1.00** |    **0.04** |  **548.8281** | **437.5000** | **367.1875** | **3709.76 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)arami [31] | 9,297.0 μs | 5,075.38 μs | 278.20 μs |  4.91 |    0.20 | 1046.8750 | 765.6250 | 296.8750 | 9746.01 KB |        2.63 |
