```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|------------:|----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)rence [26]** |   **187.4 μs** |    **59.32 μs** |   **3.25 μs** |  **1.00** |    **0.02** |  **32.2266** |   **8.0566** |        **-** |  **264.96 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)rence [26] |   462.4 μs | 1,056.84 μs |  57.93 μs |  2.47 |    0.27 |  78.1250 |  25.8789 |        - |  639.93 KB |        2.42 |
|            |       |                      |            |             |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **Trady(...)rence [26]** | **2,116.6 μs** |   **568.29 μs** |  **31.15 μs** |  **1.00** |    **0.02** | **527.3438** | **414.0625** | **347.6563** | **3710.11 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)rence [26] | 9,512.4 μs | 9,171.41 μs | 502.72 μs |  4.49 |    0.21 | 890.6250 | 593.7500 | 312.5000 | 6290.21 KB |        1.70 |
