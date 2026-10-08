```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId           | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0      | Gen1     | Gen2     | Allocated   | Alloc Ratio |
|----------- |------ |----------------- |-------------:|------------:|-----------:|------:|--------:|----------:|---------:|---------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetRocWb** |  **2,434.58 μs** | **3,058.73 μs** | **167.659 μs** |  **1.00** |    **0.08** |  **253.9063** |  **82.0313** |        **-** |  **2094.43 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetRocWb |     91.78 μs |    31.43 μs |   1.723 μs |  0.04 |    0.00 |   32.7148 |   8.9111 |        - |    268.2 KB |        0.13 |
|            |       |                  |              |             |            |       |         |           |          |          |             |             |
| **Ooples**     | **10000** | **Skender.GetRocWb** | **23,976.75 μs** | **9,695.24 μs** | **531.429 μs** |  **1.00** |    **0.03** | **2906.2500** | **656.2500** | **468.7500** | **22322.23 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetRocWb |  1,781.99 μs | 2,207.54 μs | 121.003 μs |  0.07 |    0.00 |  373.0469 | 318.3594 |  87.8906 |  2659.36 KB |        0.12 |
