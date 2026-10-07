```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean         | Error        | StdDev      | Ratio | RatioSD | Gen0      | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|-------------:|------------:|------:|--------:|----------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.RocR** |   **435.038 μs** |   **125.581 μs** |   **6.8835 μs** | **1.000** |    **0.02** |   **64.9414** |  **20.0195** |        **-** |  **531.24 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.RocR |     2.799 μs |     6.075 μs |   0.3330 μs | 0.006 |    0.00 |    1.9569 |   0.0420 |        - |   16.02 KB |        0.03 |
|            |       |                      |              |              |             |       |         |           |          |          |            |             |
| **Ooples**     | **10000** | **TaLib.Functions.RocR** | **5,404.367 μs** | **5,376.457 μs** | **294.7017 μs** | **1.002** |    **0.07** | **1000.0000** | **859.3750** | **492.1875** | **6421.95 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.RocR |    23.275 μs |    25.112 μs |   1.3765 μs | 0.004 |    0.00 |   19.0430 |   3.1738 |        - |  156.64 KB |        0.02 |
