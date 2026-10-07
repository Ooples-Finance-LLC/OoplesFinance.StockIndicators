```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean         | Error         | StdDev       | Ratio | RatioSD | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|--------------:|-------------:|------:|--------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)orrel [22]** |  **37,711.7 μs** |   **7,443.09 μs** |    **407.98 μs** | **1.000** |    **0.01** |  **2000.0000** |         **-** |  **17426.53 KB** |       **1.000** |
| Competitor | 1000  | TaLib(...)orrel [22] |     139.8 μs |      40.03 μs |      2.19 μs | 0.004 |    0.00 |          - |         - |     38.52 KB |       0.002 |
|            |       |                      |              |               |              |       |         |            |           |              |             |
| **Ooples**     | **10000** | **TaLib(...)orrel [22]** | **260,893.2 μs** | **412,076.37 μs** | **22,587.30 μs** | **1.005** |    **0.11** | **21000.0000** | **1000.0000** | **177351.65 KB** |       **1.000** |
| Competitor | 10000 | TaLib(...)orrel [22] |     257.9 μs |   1,050.67 μs |     57.59 μs | 0.001 |    0.00 |          - |         - |    329.21 KB |       0.002 |
