```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0      | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |------------:|------------:|----------:|------:|--------:|----------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)entum [28]** |    **166.0 μs** |    **45.56 μs** |   **2.50 μs** |  **1.00** |    **0.02** |   **32.2266** |   **8.0566** |        **-** |  **265.04 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)entum [28] |    547.2 μs |   126.72 μs |   6.95 μs |  3.30 |    0.06 |  110.3516 |  39.0625 |        - |  904.99 KB |        3.41 |
|            |       |                      |             |             |           |       |         |           |          |          |            |             |
| **Ooples**     | **10000** | **Trady(...)entum [28]** |  **2,123.2 μs** | **4,324.69 μs** | **237.05 μs** |  **1.01** |    **0.13** |  **527.3438** | **414.0625** | **347.6563** | **3710.51 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)entum [28] | 10,027.1 μs | 5,989.39 μs | 328.30 μs |  4.76 |    0.45 | 1281.2500 | 968.7500 | 593.7500 | 8830.69 KB |        2.38 |
