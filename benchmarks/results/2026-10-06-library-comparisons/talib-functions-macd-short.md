```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |------------:|------------:|----------:|------:|--------:|----------:|----------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Macd** | **16,812.0 μs** | **17,030.4 μs** | **933.49 μs** |  **1.00** |    **0.07** |         **-** |         **-** |  **5257.5 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Macd |    248.2 μs |    142.8 μs |   7.83 μs |  0.01 |    0.00 |         - |         - |  118.73 KB |        0.02 |
|            |       |                      |             |             |           |       |         |           |           |            |             |
| **Ooples**     | **10000** | **TaLib.Functions.Macd** | **41,593.1 μs** |  **8,786.4 μs** | **481.61 μs** | **1.000** |    **0.01** | **6000.0000** | **1000.0000** | **54787.3 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Macd |    411.8 μs |  1,877.0 μs | 102.88 μs | 0.010 |    0.00 |         - |         - |  1129.8 KB |        0.02 |
