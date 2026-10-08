```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId              | Mean         | Error        | StdDev     | Ratio | RatioSD | Allocated | Alloc Ratio |
|----------- |------ |-------------------- |-------------:|-------------:|-----------:|------:|--------:|----------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Add** |  **2,490.47 μs** | **11,049.71 μs** | **605.672 μs** |  **1.04** |    **0.29** | **305.42 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Add |     99.40 μs |    124.85 μs |   6.843 μs |  0.04 |    0.01 |   37.7 KB |        0.12 |
|            |       |                     |              |              |            |       |         |           |             |
| **Ooples**     | **10000** | **TaLib.Functions.Add** | **15,205.50 μs** |  **2,436.04 μs** | **133.528 μs** |  **1.00** |    **0.01** | **4113.8 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Add |    200.30 μs |    317.26 μs |  17.390 μs |  0.01 |    0.00 | 328.72 KB |        0.08 |
