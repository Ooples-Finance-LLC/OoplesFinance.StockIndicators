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
| **Ooples**     | **1000**  | **TaLib.Functions.Mama** | **127,635.2 μs** | **967,402.3 μs** | **53,026.58 μs** | **1.143** |    **0.64** |  **5000.0000** |         **-** |  **45631.66 KB** |       **1.000** |
| Competitor | 1000  | TaLib.Functions.Mama |     610.7 μs |     203.9 μs |     11.18 μs | 0.005 |    0.00 |          - |         - |     72.18 KB |       0.002 |
|            |       |                      |              |              |              |       |         |            |           |              |             |
| **Ooples**     | **10000** | **TaLib.Functions.Mama** | **736,597.2 μs** | **490,337.9 μs** | **26,877.08 μs** | **1.001** |    **0.04** | **56000.0000** | **1000.0000** | **462730.13 KB** |       **1.000** |
| Competitor | 10000 | TaLib.Functions.Mama |   1,332.9 μs |   3,056.6 μs |    167.54 μs | 0.002 |    0.00 |          - |         - |    651.98 KB |       0.001 |
