```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean       | Error     | StdDev   | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|----------:|---------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)cking [21]** |   **223.6 μs** |  **53.76 μs** |  **2.95 μs** |  **1.00** |    **0.02** |  **32.7148** |   **8.7891** |        **-** |   **267.7 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)cking [21] |   100.4 μs |  11.91 μs |  0.65 μs |  0.45 |    0.01 |   1.4648 |        - |        - |   12.08 KB |        0.05 |
|            |       |                      |            |           |          |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)cking [21]** | **2,458.7 μs** | **234.17 μs** | **12.84 μs** |  **1.00** |    **0.01** | **679.6875** | **566.4063** | **496.0938** | **3715.06 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)cking [21] | 1,016.0 μs | 502.43 μs | 27.54 μs |  0.41 |    0.01 |  13.6719 |        - |        - |  117.55 KB |        0.03 |
