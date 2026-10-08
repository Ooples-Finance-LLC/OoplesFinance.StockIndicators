```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |------------:|----------:|----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)Lines [37]** |   **241.55 μs** |  **88.52 μs** |  **4.852 μs** |  **1.00** |    **0.02** |  **32.4707** |   **8.0566** |        **-** |   **266.5 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)Lines [37] |    45.24 μs |  12.00 μs |  0.657 μs |  0.19 |    0.00 |   1.4648 |        - |        - |   12.08 KB |        0.05 |
|            |       |                      |             |           |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)Lines [37]** | **2,599.53 μs** | **606.65 μs** | **33.253 μs** |  **1.00** |    **0.02** | **679.6875** | **574.2188** | **496.0938** | **3714.12 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)Lines [37] |   531.71 μs | 892.15 μs | 48.902 μs |  0.20 |    0.02 |  13.6719 |        - |        - |  117.55 KB |        0.03 |
