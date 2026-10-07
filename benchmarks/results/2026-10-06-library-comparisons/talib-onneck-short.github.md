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
| **Ooples**     | **1000**  | **TaLib.Candles.OnNeck** |   **223.16 μs** | **103.96 μs** |  **5.699 μs** |  **1.00** |    **0.03** |  **32.4707** |  **10.7422** |        **-** |  **266.93 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Candles.OnNeck |    51.58 μs |  13.47 μs |  0.738 μs |  0.23 |    0.01 |   1.4648 |        - |        - |   12.08 KB |        0.05 |
|            |       |                      |             |           |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib.Candles.OnNeck** | **2,409.53 μs** | **326.59 μs** | **17.902 μs** |  **1.00** |    **0.01** | **679.6875** | **562.5000** | **496.0938** | **3714.34 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Candles.OnNeck |   547.87 μs |  63.71 μs |  3.492 μs |  0.23 |    0.00 |  13.6719 |        - |        - |  117.55 KB |        0.03 |
