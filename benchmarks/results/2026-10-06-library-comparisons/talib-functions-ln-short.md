```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId             | Mean       | Error       | StdDev   | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |------------------- |-----------:|------------:|---------:|-----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Ln** | **2,454.1 μs** |  **9,110.4 μs** | **499.4 μs** | **2,235.9 μs** |  **1.03** |    **0.25** |  **305.16 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Ln |   319.2 μs |  6,568.3 μs | 360.0 μs |   120.4 μs |  0.13 |    0.13 |    38.3 KB |        0.13 |
|            |       |                    |            |             |          |            |       |         |            |             |
| **Ooples**     | **10000** | **TaLib.Functions.Ln** | **2,526.8 μs** |  **8,651.5 μs** | **474.2 μs** | **2,490.9 μs** |  **1.02** |    **0.24** | **4113.27 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Ln |   751.2 μs | 12,501.6 μs | 685.3 μs |   371.1 μs |  0.30 |    0.25 |  328.67 KB |        0.08 |
