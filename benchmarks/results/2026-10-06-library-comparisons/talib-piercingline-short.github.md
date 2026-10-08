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
| **Ooples**     | **1000**  | **TaLib(...)gLine [26]** |   **208.61 μs** | **238.69 μs** | **13.083 μs** |  **1.00** |    **0.08** |  **32.4707** |   **8.0566** |        **-** |  **265.99 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)gLine [26] |    51.54 μs |  15.59 μs |  0.854 μs |  0.25 |    0.01 |   1.4648 |        - |        - |   12.08 KB |        0.05 |
|            |       |                      |             |           |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)gLine [26]** | **2,356.40 μs** | **819.12 μs** | **44.898 μs** |  **1.00** |    **0.02** | **679.6875** | **574.2188** | **496.0938** | **3713.32 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)gLine [26] |   599.67 μs | 149.62 μs |  8.201 μs |  0.25 |    0.01 |  13.6719 |        - |        - |  117.55 KB |        0.03 |
