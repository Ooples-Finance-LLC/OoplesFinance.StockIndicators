```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId           | Mean       | Error      | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |----------------- |-----------:|-----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetAroon** | **2,789.3 μs** | **1,389.3 μs** |  **76.15 μs** |  **1.00** |    **0.03** |  **527.18 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetAroon |   795.0 μs | 1,020.6 μs |  55.95 μs |  0.29 |    0.02 |  255.84 KB |        0.49 |
|            |       |                  |            |            |           |       |         |            |             |
| **Ooples**     | **10000** | **Skender.GetAroon** | **6,471.7 μs** | **8,899.8 μs** | **487.83 μs** |  **1.00** |    **0.09** |  **6320.3 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetAroon | 2,445.3 μs | 8,338.2 μs | 457.05 μs |  0.38 |    0.07 | 2493.16 KB |        0.39 |
