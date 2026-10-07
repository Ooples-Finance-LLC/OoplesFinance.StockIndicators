```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|-------------:|-------------:|------:|--------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)eriod [26]** | **120,462.3 μs** | **852,021.6 μs** | **46,702.18 μs** | **1.141** |    **0.63** |  **4000.0000** |         **-** |  **40378.18 KB** |       **1.000** |
| Competitor | 1000  | TaLib(...)eriod [26] |     452.9 μs |     118.5 μs |      6.49 μs | 0.004 |    0.00 |          - |         - |     39.42 KB |       0.001 |
|            |       |                      |              |              |              |       |         |            |           |              |             |
| **Ooples**     | **10000** | **TaLib(...)eriod [26]** | **640,554.1 μs** | **217,131.3 μs** | **11,901.70 μs** | **1.000** |    **0.02** | **49000.0000** | **1000.0000** | **409526.78 KB** |       **1.000** |
| Competitor | 10000 | TaLib(...)eriod [26] |   1,136.2 μs |   1,751.4 μs |     96.00 μs | 0.002 |    0.00 |          - |         - |    329.46 KB |       0.001 |
