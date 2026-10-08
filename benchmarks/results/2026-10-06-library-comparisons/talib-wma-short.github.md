```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId              | Mean         | Error      | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |-------------------- |-------------:|-----------:|-----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Wma** |   **209.093 μs** |  **33.758 μs** |  **1.8504 μs** |  **1.00** |    **0.01** |  **36.3770** |  **11.9629** |        **-** |  **298.84 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Wma |     3.163 μs |   2.904 μs |  0.1592 μs |  0.02 |    0.00 |   1.9569 |   0.0420 |        - |   16.02 KB |        0.05 |
|            |       |                     |              |            |            |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib.Functions.Wma** | **2,316.439 μs** | **565.851 μs** | **31.0162 μs** |  **1.00** |    **0.02** | **714.8438** | **535.1563** | **496.0938** | **4027.71 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Wma |    29.149 μs |   3.417 μs |  0.1873 μs |  0.01 |    0.00 |  19.0430 |   3.1738 |        - |  156.64 KB |        0.04 |
