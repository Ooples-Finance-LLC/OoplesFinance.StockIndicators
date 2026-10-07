```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean         | Error        | StdDev      | Ratio | RatioSD | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|-------------:|------------:|------:|--------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)hasor [24]** | **159,915.8 μs** | **100,727.3 μs** | **5,521.20 μs** | **1.001** |    **0.04** |  **5000.0000** |         **-** |  **40858.21 KB** |       **1.000** |
| Competitor | 1000  | TaLib(...)hasor [24] |     524.0 μs |     437.1 μs |    23.96 μs | 0.003 |    0.00 |          - |         - |     71.85 KB |       0.002 |
|            |       |                      |              |              |             |       |         |            |           |              |             |
| **Ooples**     | **10000** | **TaLib(...)hasor [24]** | **633,299.5 μs** | **122,177.0 μs** | **6,696.93 μs** | **1.000** |    **0.01** | **50000.0000** | **1000.0000** | **414444.96 KB** |       **1.000** |
| Competitor | 10000 | TaLib(...)hasor [24] |   1,073.9 μs |   1,248.2 μs |    68.42 μs | 0.002 |    0.00 |          - |         - |    651.98 KB |       0.002 |
