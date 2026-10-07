```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------- |------------:|------------:|----------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetMfi** | **12,915.8 μs** | **14,600.2 μs** | **800.29 μs** |  **1.00** |    **0.08** |         **-** |         **-** |  **4597.38 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetMfi |    492.6 μs |    324.9 μs |  17.81 μs |  0.04 |    0.00 |         - |         - |   186.95 KB |        0.04 |
|            |       |                |             |             |           |       |         |           |           |             |             |
| **Ooples**     | **10000** | **Skender.GetMfi** | **44,869.6 μs** | **15,620.0 μs** | **856.19 μs** |  **1.00** |    **0.02** | **5000.0000** | **1000.0000** | **47654.91 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetMfi |  1,625.0 μs |  3,522.5 μs | 193.08 μs |  0.04 |    0.00 |         - |         - |  1813.54 KB |        0.04 |
