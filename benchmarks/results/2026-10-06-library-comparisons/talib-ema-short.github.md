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
| **Ooples**     | **1000**  | **TaLib.Functions.Ema** |   **185.627 μs** |  **59.211 μs** |  **3.2456 μs** |  **1.00** |    **0.02** |  **35.4004** |   **9.5215** |        **-** |  **290.98 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Ema |     3.810 μs |   4.286 μs |  0.2349 μs |  0.02 |    0.00 |   1.9531 |   0.0381 |        - |   16.02 KB |        0.06 |
|            |       |                     |              |            |            |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib.Functions.Ema** | **2,121.372 μs** | **577.603 μs** | **31.6604 μs** |  **1.00** |    **0.02** | **707.0313** | **574.2188** | **496.0938** | **3949.83 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Ema |    37.335 μs |  44.597 μs |  2.4445 μs |  0.02 |    0.00 |  19.0430 |   3.1738 |        - |  156.64 KB |        0.04 |
