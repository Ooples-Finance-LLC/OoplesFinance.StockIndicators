```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId             | Mean       | Error      | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |------------------- |-----------:|-----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetFractal** |   **560.9 μs** | **1,092.0 μs** |  **59.86 μs** |  **1.01** |    **0.13** |  **259.16 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetFractal | 1,181.1 μs |   533.9 μs |  29.27 μs |  2.12 |    0.19 |  169.04 KB |        0.65 |
|            |       |                    |            |            |           |       |         |            |             |
| **Ooples**     | **10000** | **Skender.GetFractal** | **1,201.5 μs** | **6,701.4 μs** | **367.33 μs** |  **1.06** |    **0.37** | **2599.29 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetFractal | 6,409.9 μs | 2,952.7 μs | 161.85 μs |  5.64 |    1.28 | 1628.07 KB |        0.63 |
