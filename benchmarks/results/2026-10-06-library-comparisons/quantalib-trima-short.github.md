```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId          | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated | Alloc Ratio |
|----------- |------ |---------------- |-----------:|------------:|----------:|------:|--------:|---------:|---------:|---------:|----------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Trima** |   **208.9 μs** |    **79.34 μs** |   **4.35 μs** |  **1.00** |    **0.03** |  **32.4707** |   **8.0566** |        **-** | **265.89 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Trima |   103.1 μs |    47.08 μs |   2.58 μs |  0.49 |    0.01 |  31.1279 |   0.9766 |        - | 254.73 KB |        0.96 |
|            |       |                 |            |             |           |       |         |          |          |          |           |             |
| **Ooples**     | **10000** | **QuanTAlib.Trima** | **2,457.3 μs** | **2,448.66 μs** | **134.22 μs** |  **1.00** |    **0.07** | **679.6875** | **558.5938** | **496.0938** |   **3713 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Trima | 1,005.0 μs |   388.29 μs |  21.28 μs |  0.41 |    0.02 | 313.4766 |  62.5000 |        - | 2564.5 KB |        0.69 |
