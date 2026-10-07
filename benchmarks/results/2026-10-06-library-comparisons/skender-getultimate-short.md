```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId              | Mean         | Error       | StdDev      | Ratio | RatioSD | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |-------------------- |-------------:|------------:|------------:|------:|--------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetUltimate** |  **22,601.3 μs** | **12,642.5 μs** |   **692.98 μs** |  **1.00** |    **0.04** |  **1000.0000** |         **-** |   **9517.96 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetUltimate |     549.6 μs |    540.9 μs |    29.65 μs |  0.02 |    0.00 |          - |         - |    183.72 KB |        0.02 |
|            |       |                     |              |             |             |       |         |            |           |              |             |
| **Ooples**     | **10000** | **Skender.GetUltimate** | **153,179.6 μs** | **22,368.2 μs** | **1,226.08 μs** |  **1.00** |    **0.01** | **12000.0000** | **1000.0000** | **101275.98 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetUltimate |   2,295.5 μs |  3,045.5 μs |   166.94 μs |  0.01 |    0.00 |          - |         - |   1774.49 KB |        0.02 |
