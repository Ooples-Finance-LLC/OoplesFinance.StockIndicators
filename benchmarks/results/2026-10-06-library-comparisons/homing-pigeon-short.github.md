```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean        | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated | Alloc Ratio |
|----------- |------ |--------------------- |------------:|-------------:|-----------:|------:|--------:|---------:|---------:|---------:|----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)igeon [26]** |   **258.55 μs** |    **46.375 μs** |   **2.542 μs** |  **1.00** |    **0.01** |  **32.4707** |   **9.0332** |        **-** | **266.18 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)igeon [26] |    43.51 μs |     9.640 μs |   0.528 μs |  0.17 |    0.00 |   1.4648 |        - |        - |  12.08 KB |        0.05 |
|            |       |                      |             |              |            |       |         |          |          |          |           |             |
| **Ooples**     | **10000** | **TaLib(...)igeon [26]** | **3,018.90 μs** | **7,722.290 μs** | **423.285 μs** |  **1.01** |    **0.17** | **679.6875** | **562.5000** | **496.0938** | **3713.1 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)igeon [26] |   492.01 μs |   143.762 μs |   7.880 μs |  0.16 |    0.02 |  14.1602 |        - |        - | 117.55 KB |        0.03 |
