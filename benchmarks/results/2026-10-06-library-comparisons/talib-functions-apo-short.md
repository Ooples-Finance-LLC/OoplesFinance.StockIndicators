```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId              | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |-------------------- |------------:|------------:|----------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Apo** | **11,249.8 μs** | **10,053.6 μs** | **551.07 μs** |  **1.00** |    **0.06** |         **-** |         **-** |  **3020.87 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Apo |    362.6 μs |  1,622.7 μs |  88.94 μs |  0.03 |    0.01 |         - |         - |    46.59 KB |        0.02 |
|            |       |                     |             |             |           |       |         |           |           |             |             |
| **Ooples**     | **10000** | **TaLib.Functions.Apo** | **27,087.6 μs** | **11,754.7 μs** | **644.32 μs** | **1.000** |    **0.03** | **3000.0000** | **1000.0000** | **31571.33 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Apo |    221.4 μs |    498.4 μs |  27.32 μs | 0.008 |    0.00 |         - |         - |   407.22 KB |        0.01 |
