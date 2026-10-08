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
| **Ooples**     | **1000**  | **Skender.GetStochRsi** | **10,890.7 μs** | **14,896.7 μs** | **816.54 μs** |  **1.00** |    **0.09** |         **-** |         **-** |  **3207.72 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetStochRsi |    975.1 μs |    835.2 μs |  45.78 μs |  0.09 |    0.01 |         - |         - |   385.62 KB |        0.12 |
|            |       |                     |             |             |           |       |         |           |           |             |             |
| **Ooples**     | **10000** | **Skender.GetStochRsi** | **34,361.7 μs** | **14,691.6 μs** | **805.30 μs** |  **1.00** |    **0.03** | **3000.0000** | **1000.0000** | **33545.48 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetStochRsi |  3,131.8 μs |  7,781.8 μs | 426.55 μs |  0.09 |    0.01 |         - |         - |  3812.32 KB |        0.11 |
