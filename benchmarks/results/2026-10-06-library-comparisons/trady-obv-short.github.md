```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0      | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|-----------:|----------:|------:|--------:|----------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)olume [31]** |   **188.6 μs** |   **102.2 μs** |   **5.60 μs** |  **1.00** |    **0.04** |   **34.1797** |  **10.0098** |        **-** |  **280.69 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)olume [31] |   534.7 μs |   121.5 μs |   6.66 μs |  2.84 |    0.08 |  102.5391 |  34.1797 |        - |  844.84 KB |        3.01 |
|            |       |                      |            |            |           |       |         |           |          |          |            |             |
| **Ooples**     | **10000** | **Trady(...)olume [31]** | **2,513.5 μs** | **2,565.3 μs** | **140.61 μs** |  **1.00** |    **0.07** |  **562.5000** | **429.6875** | **359.3750** | **3885.08 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)olume [31] | 9,443.6 μs | 7,787.1 μs | 426.84 μs |  3.76 |    0.23 | 1125.0000 | 750.0000 | 312.5000 | 8323.39 KB |        2.14 |
