```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId             | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |------------------- |------------:|------------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetAwesome** |  **5,121.8 μs** |  **4,265.0 μs** | **233.78 μs** |  **1.00** |    **0.06** |  **657.13 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetAwesome |    605.2 μs |    375.6 μs |  20.59 μs |  0.12 |    0.01 |  169.59 KB |        0.26 |
|            |       |                    |             |             |           |       |         |            |             |
| **Ooples**     | **10000** | **Skender.GetAwesome** | **10,278.0 μs** |  **9,136.4 μs** | **500.80 μs** |  **1.00** |    **0.06** | **7700.63 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetAwesome |  1,773.7 μs | 11,267.8 μs | 617.63 μs |  0.17 |    0.05 | 1628.52 KB |        0.21 |
