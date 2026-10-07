```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean        | Error         | StdDev      | Ratio | RatioSD | Gen0       | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |------------:|--------------:|------------:|------:|--------:|-----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)nusDI [23]** | **21,311.0 μs** |  **18,383.33 μs** | **1,007.65 μs** | **1.001** |    **0.06** |  **1000.0000** |         **-** |  **8284.64 KB** |       **1.000** |
| Competitor | 1000  | TaLib(...)nusDI [23] |    195.1 μs |      45.88 μs |     2.51 μs | 0.009 |    0.00 |          - |         - |    38.63 KB |       0.005 |
|            |       |                      |             |               |             |       |         |            |           |             |             |
| **Ooples**     | **10000** | **TaLib(...)nusDI [23]** | **59,148.0 μs** | **105,715.65 μs** | **5,794.63 μs** | **1.006** |    **0.12** | **10000.0000** | **1000.0000** | **84577.12 KB** |       **1.000** |
| Competitor | 10000 | TaLib(...)nusDI [23] |    268.2 μs |     548.83 μs |    30.08 μs | 0.005 |    0.00 |          - |         - |   324.08 KB |       0.004 |
