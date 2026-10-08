```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean        | Error        | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |------------:|-------------:|----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Candles.Harami** |   **266.43 μs** |    **86.991 μs** |  **4.768 μs** |  **1.00** |    **0.02** |  **32.2266** |   **8.7891** |        **-** |   **266.1 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Candles.Harami |    56.32 μs |     9.697 μs |  0.532 μs |  0.21 |    0.00 |   1.4648 |        - |        - |   12.08 KB |        0.05 |
|            |       |                      |             |              |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib.Candles.Harami** | **2,933.19 μs** | **1,661.769 μs** | **91.087 μs** |  **1.00** |    **0.04** | **679.6875** | **570.3125** | **496.0938** | **3713.22 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Candles.Harami |   662.17 μs |   416.372 μs | 22.823 μs |  0.23 |    0.01 |  13.6719 |        - |        - |  117.55 KB |        0.03 |
