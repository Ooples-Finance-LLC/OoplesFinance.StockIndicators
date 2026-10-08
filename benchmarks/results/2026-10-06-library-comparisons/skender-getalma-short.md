```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId          | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |---------------- |------------:|------------:|------------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetAlma** | **22,006.5 μs** | **22,093.5 μs** | **1,211.02 μs** |  **1.00** |    **0.07** |         **-** |         **-** |  **5285.76 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetAlma |    484.3 μs |    454.8 μs |    24.93 μs |  0.02 |    0.00 |         - |         - |   113.23 KB |        0.02 |
|            |       |                 |             |             |             |       |         |           |           |             |             |
| **Ooples**     | **10000** | **Skender.GetAlma** | **47,066.2 μs** | **14,729.9 μs** |   **807.40 μs** |  **1.00** |    **0.02** | **6000.0000** | **1000.0000** | **54366.89 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetAlma |  1,165.6 μs |  4,513.7 μs |   247.41 μs |  0.02 |    0.00 |         - |         - |  1071.89 KB |        0.02 |
