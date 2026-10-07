```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean        | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated | Alloc Ratio |
|----------- |------ |--------------------- |------------:|------------:|-----------:|------:|--------:|---------:|---------:|---------:|----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)sting [23]** |   **236.47 μs** |    **94.08 μs** |   **5.157 μs** |  **1.00** |    **0.03** |  **32.4707** |  **10.7422** |        **-** | **266.93 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)sting [23] |    50.44 μs |    12.27 μs |   0.673 μs |  0.21 |    0.00 |   1.4648 |        - |        - |  12.08 KB |        0.05 |
|            |       |                      |             |             |            |       |         |          |          |          |           |             |
| **Ooples**     | **10000** | **TaLib(...)sting [23]** | **2,814.17 μs** | **4,943.48 μs** | **270.969 μs** |  **1.01** |    **0.12** | **679.6875** | **558.5938** | **496.0938** | **3714.2 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)sting [23] |   545.82 μs |   122.99 μs |   6.742 μs |  0.20 |    0.02 |  13.6719 |        - |        - | 117.55 KB |        0.03 |
