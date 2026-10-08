```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId          | Mean         | Error       | StdDev      | Ratio | RatioSD | Gen0      | Gen1      | Allocated  | Alloc Ratio |
|----------- |------ |---------------- |-------------:|------------:|------------:|------:|--------:|----------:|----------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetKama** |  **29,906.9 μs** | **13,086.0 μs** |   **717.29 μs** |  **1.00** |    **0.03** |         **-** |         **-** | **7761.35 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetKama |     567.7 μs |    408.1 μs |    22.37 μs |  0.02 |    0.00 |         - |         - |  160.52 KB |        0.02 |
|            |       |                 |              |             |             |       |         |           |           |            |             |
| **Ooples**     | **10000** | **Skender.GetKama** | **124,467.7 μs** | **96,538.4 μs** | **5,291.60 μs** |  **1.00** |    **0.05** | **9000.0000** | **1000.0000** | **79938.2 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetKama |   1,340.0 μs |  1,173.7 μs |    64.33 μs |  0.01 |    0.00 |         - |         - | 1545.59 KB |        0.02 |
