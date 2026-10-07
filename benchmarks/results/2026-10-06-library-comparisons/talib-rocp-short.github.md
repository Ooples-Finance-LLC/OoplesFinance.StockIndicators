```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean         | Error         | StdDev      | Ratio | RatioSD | Gen0      | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|--------------:|------------:|------:|--------:|----------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.RocP** |   **484.307 μs** |    **56.4225 μs** |   **3.0927 μs** | **1.000** |    **0.01** |   **67.8711** |  **20.9961** |        **-** |  **556.18 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.RocP |     2.514 μs |     0.4277 μs |   0.0234 μs | 0.005 |    0.00 |    1.9569 |   0.0420 |        - |   16.02 KB |        0.03 |
|            |       |                      |              |               |             |       |         |           |          |          |            |             |
| **Ooples**     | **10000** | **TaLib.Functions.RocP** | **5,797.577 μs** | **9,286.9351 μs** | **509.0483 μs** | **1.005** |    **0.11** | **1039.0625** | **898.4375** | **492.1875** | **6691.76 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.RocP |    22.792 μs |     4.0269 μs |   0.2207 μs | 0.004 |    0.00 |   19.0430 |   3.1738 |        - |  156.64 KB |        0.02 |
