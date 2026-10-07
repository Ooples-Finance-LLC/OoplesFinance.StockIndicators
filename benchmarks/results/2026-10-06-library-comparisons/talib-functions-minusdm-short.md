```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean        | Error         | StdDev      | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |------------:|--------------:|------------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)nusDM [23]** | **17,333.7 μs** |  **14,829.01 μs** |   **812.83 μs** | **1.001** |    **0.06** |         **-** |         **-** |  **7267.98 KB** |       **1.000** |
| Competitor | 1000  | TaLib(...)nusDM [23] |    136.5 μs |      77.92 μs |     4.27 μs | 0.008 |    0.00 |         - |         - |    37.97 KB |       0.005 |
|            |       |                      |             |               |             |       |         |           |           |             |             |
| **Ooples**     | **10000** | **TaLib(...)nusDM [23]** | **48,306.8 μs** | **105,481.40 μs** | **5,781.79 μs** | **1.009** |    **0.14** | **8000.0000** | **1000.0000** | **74210.77 KB** |       **1.000** |
| Competitor | 10000 | TaLib(...)nusDM [23] |    229.6 μs |     300.33 μs |    16.46 μs | 0.005 |    0.00 |         - |         - |   327.68 KB |       0.004 |
