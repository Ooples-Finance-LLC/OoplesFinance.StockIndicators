```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |--------------------- |------------:|-------------:|------------:|------:|--------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Adxr** | **33,446.6 μs** |  **13,724.4 μs** |   **752.28 μs** | **1.000** |    **0.03** |  **1000.0000** |         **-** |  **13519.18 KB** |       **1.000** |
| Competitor | 1000  | TaLib.Functions.Adxr |    225.7 μs |     134.4 μs |     7.37 μs | 0.007 |    0.00 |          - |         - |     45.88 KB |       0.003 |
|            |       |                      |             |              |             |       |         |            |           |              |             |
| **Ooples**     | **10000** | **TaLib.Functions.Adxr** | **96,892.8 μs** | **169,048.4 μs** | **9,266.12 μs** | **1.006** |    **0.12** | **16000.0000** | **1000.0000** | **138299.88 KB** |       **1.000** |
| Competitor | 10000 | TaLib.Functions.Adxr |    510.7 μs |     792.2 μs |    43.42 μs | 0.005 |    0.00 |          - |         - |    402.01 KB |       0.003 |
