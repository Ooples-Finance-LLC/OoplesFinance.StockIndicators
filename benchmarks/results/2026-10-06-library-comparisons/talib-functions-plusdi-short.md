```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0       | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |------------:|------------:|------------:|------:|--------:|-----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)lusDI [22]** | **20,683.4 μs** | **14,952.4 μs** |   **819.59 μs** | **1.001** |    **0.05** |  **1000.0000** |         **-** |  **8285.14 KB** |       **1.000** |
| Competitor | 1000  | TaLib(...)lusDI [22] |    183.1 μs |    103.5 μs |     5.68 μs | 0.009 |    0.00 |          - |         - |    38.91 KB |       0.005 |
|            |       |                      |             |             |             |       |         |            |           |             |             |
| **Ooples**     | **10000** | **TaLib(...)lusDI [22]** | **57,016.5 μs** | **43,503.3 μs** | **2,384.56 μs** | **1.001** |    **0.05** | **10000.0000** | **1000.0000** | **84579.49 KB** |       **1.000** |
| Competitor | 10000 | TaLib(...)lusDI [22] |    321.5 μs |    152.4 μs |     8.35 μs | 0.006 |    0.00 |          - |         - |   328.63 KB |       0.004 |
