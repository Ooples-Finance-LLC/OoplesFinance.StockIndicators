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
| **Ooples**     | **1000**  | **TaLib.Candles.InNeck** |   **223.47 μs** |  **14.46 μs** |  **0.793 μs** |  **1.00** |    **0.00** |  **32.4707** |  **10.7422** |        **-** |  **266.93 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Candles.InNeck |    50.68 μs |  12.62 μs |  0.691 μs |  0.23 |    0.00 |   1.4648 |        - |        - |   12.08 KB |        0.05 |
|            |       |                      |             |           |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib.Candles.InNeck** | **2,475.11 μs** | **954.66 μs** | **52.328 μs** |  **1.00** |    **0.03** | **679.6875** | **562.5000** | **496.0938** | **3714.51 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Candles.InNeck |   585.13 μs | 651.81 μs | 35.728 μs |  0.24 |    0.01 |  13.6719 |        - |        - |  117.55 KB |        0.03 |
