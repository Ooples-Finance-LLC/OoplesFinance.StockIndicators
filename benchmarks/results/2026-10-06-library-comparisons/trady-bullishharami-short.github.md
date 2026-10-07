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
| **Ooples**     | **1000**  | **Trady(...)arami [31]** |   **149.1 μs** |    **58.86 μs** |   **3.23 μs** |  **1.00** |    **0.03** |   **32.2266** |   **8.0566** |        **-** |  **264.45 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)arami [31] |   692.9 μs |   252.55 μs |  13.84 μs |  4.65 |    0.12 |  121.0938 |  49.8047 |        - |  995.91 KB |        3.77 |
|            |       |                      |            |             |           |       |         |           |          |          |            |             |
| **Ooples**     | **10000** | **Trady(...)arami [31]** | **1,868.9 μs** | **3,637.66 μs** | **199.39 μs** |  **1.01** |    **0.13** |  **539.0625** | **431.6406** | **357.4219** | **3709.61 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)arami [31] | 9,836.9 μs | 4,338.32 μs | 237.80 μs |  5.30 |    0.47 | 1062.5000 | 765.6250 | 312.5000 | 9746.15 KB |        2.63 |
