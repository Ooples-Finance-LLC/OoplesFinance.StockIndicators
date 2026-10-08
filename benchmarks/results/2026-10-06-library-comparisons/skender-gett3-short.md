```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId        | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |-------------- |------------:|-------------:|------------:|------:|--------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetT3** | **22,319.7 μs** |  **16,020.2 μs** |   **878.12 μs** |  **1.00** |    **0.05** |  **1000.0000** |         **-** |  **10634.22 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetT3 |    891.7 μs |     482.0 μs |    26.42 μs |  0.04 |    0.00 |          - |         - |     112.7 KB |        0.01 |
|            |       |               |             |              |             |       |         |            |           |              |             |
| **Ooples**     | **10000** | **Skender.GetT3** | **70,069.5 μs** | **178,466.5 μs** | **9,782.35 μs** |  **1.01** |    **0.17** | **12000.0000** | **1000.0000** | **107461.29 KB** |       **1.000** |
| Competitor | 10000 | Skender.GetT3 |  1,249.6 μs |   4,476.8 μs |   245.39 μs |  0.02 |    0.00 |          - |         - |   1071.41 KB |       0.010 |
