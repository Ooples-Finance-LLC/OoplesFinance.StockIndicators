```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |------------:|-------------:|------------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)lusDM [22]** | **17,698.1 μs** | **14,996.80 μs** |   **822.03 μs** | **1.001** |    **0.06** |         **-** |         **-** |  **7267.98 KB** |       **1.000** |
| Competitor | 1000  | TaLib(...)lusDM [22] |    134.8 μs |     48.07 μs |     2.64 μs | 0.008 |    0.00 |         - |         - |    37.59 KB |       0.005 |
|            |       |                      |             |              |             |       |         |           |           |             |             |
| **Ooples**     | **10000** | **TaLib(...)lusDM [22]** | **45,824.1 μs** | **59,417.53 μs** | **3,256.88 μs** | **1.003** |    **0.09** | **8000.0000** | **1000.0000** | **74215.98 KB** |       **1.000** |
| Competitor | 10000 | TaLib(...)lusDM [22] |    289.2 μs |    542.40 μs |    29.73 μs | 0.006 |    0.00 |         - |         - |   328.66 KB |       0.004 |
