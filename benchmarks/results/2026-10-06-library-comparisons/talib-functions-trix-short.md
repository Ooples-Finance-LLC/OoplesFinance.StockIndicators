```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean        | Error        | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |------------:|-------------:|----------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Trix** | **14,984.9 μs** | **14,787.89 μs** | **810.57 μs** | **1.002** |    **0.07** |         **-** |         **-** |  **5049.66 KB** |       **1.000** |
| Competitor | 1000  | TaLib.Functions.Trix |    140.5 μs |     52.00 μs |   2.85 μs | 0.009 |    0.00 |         - |         - |    46.49 KB |       0.009 |
|            |       |                      |             |              |           |       |         |           |           |             |             |
| **Ooples**     | **10000** | **TaLib.Functions.Trix** | **36,334.9 μs** |  **8,827.10 μs** | **483.84 μs** | **1.000** |    **0.02** | **6000.0000** | **1000.0000** | **53360.66 KB** |       **1.000** |
| Competitor | 10000 | TaLib.Functions.Trix |    324.3 μs |    726.47 μs |  39.82 μs | 0.009 |    0.00 |         - |         - |   406.52 KB |       0.008 |
