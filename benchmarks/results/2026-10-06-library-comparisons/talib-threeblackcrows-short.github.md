```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated | Alloc Ratio |
|----------- |------ |--------------------- |------------:|----------:|----------:|------:|--------:|---------:|---------:|---------:|----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)Crows [29]** |   **196.34 μs** |  **80.55 μs** |  **4.415 μs** |  **1.00** |    **0.03** |  **32.4707** |   **9.5215** |        **-** | **267.15 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)Crows [29] |    68.95 μs |  17.47 μs |  0.957 μs |  0.35 |    0.01 |   1.4648 |        - |        - |  12.08 KB |        0.05 |
|            |       |                      |             |           |           |       |         |          |          |          |           |             |
| **Ooples**     | **10000** | **TaLib(...)Crows [29]** | **2,224.82 μs** | **956.62 μs** | **52.436 μs** |  **1.00** |    **0.03** | **679.6875** | **570.3125** | **496.0938** | **3714.4 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)Crows [29] |   682.39 μs | 138.88 μs |  7.613 μs |  0.31 |    0.01 |  13.6719 |        - |        - | 117.55 KB |        0.03 |
