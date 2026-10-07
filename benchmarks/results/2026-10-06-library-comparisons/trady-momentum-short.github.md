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
| **Ooples**     | **1000**  | **Trady(...)entum [24]** |   **197.0 μs** |    **42.83 μs** |   **2.35 μs** |  **1.00** |    **0.01** |  **32.2266** |   **8.0566** |        **-** |  **265.04 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)entum [24] |   419.1 μs |   136.24 μs |   7.47 μs |  2.13 |    0.04 |  78.1250 |  25.8789 |        - |  639.93 KB |        2.41 |
|            |       |                      |            |             |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **Trady(...)entum [24]** | **2,195.5 μs** |   **333.09 μs** |  **18.26 μs** |  **1.00** |    **0.01** | **527.3438** | **414.0625** | **347.6563** | **3710.22 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)entum [24] | 9,203.5 μs | 2,622.77 μs | 143.76 μs |  4.19 |    0.06 | 890.6250 | 578.1250 | 312.5000 | 6290.26 KB |        1.70 |
