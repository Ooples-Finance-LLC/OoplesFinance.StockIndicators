```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean       | Error        | StdDev    | Ratio | RatioSD | Gen0      | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|-------------:|----------:|------:|--------:|----------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)Cover [32]** |   **148.1 μs** |     **20.52 μs** |   **1.12 μs** |  **1.00** |    **0.01** |   **32.2266** |   **8.0566** |        **-** |  **264.58 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)Cover [32] |   617.6 μs |    103.39 μs |   5.67 μs |  4.17 |    0.04 |   92.7734 |  33.2031 |        - |  762.28 KB |        2.88 |
|            |       |                      |            |              |           |       |         |           |          |          |            |             |
| **Ooples**     | **10000** | **Trady(...)Cover [32]** | **1,776.5 μs** |    **304.15 μs** |  **16.67 μs** |  **1.00** |    **0.01** |  **531.2500** | **421.8750** | **349.6094** | **3709.66 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)Cover [32] | 7,712.4 μs | 10,076.89 μs | 552.35 μs |  4.34 |    0.27 | 1023.4375 | 734.3750 | 460.9375 | 7494.33 KB |        2.02 |
