```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId        | Mean       | Error      | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |-------------- |-----------:|-----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetTr** | **1,893.8 μs** | **1,173.9 μs** |  **64.35 μs** |  **1.00** |    **0.04** |  **299.04 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetTr |   466.7 μs |   166.7 μs |   9.14 μs |  0.25 |    0.01 |  150.23 KB |        0.50 |
|            |       |               |            |            |           |       |         |            |             |
| **Ooples**     | **10000** | **Skender.GetTr** | **9,747.2 μs** | **3,570.7 μs** | **195.72 μs** |  **1.00** |    **0.02** | **4028.04 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetTr |   983.6 μs | 4,035.8 μs | 221.22 μs |  0.10 |    0.02 | 1451.05 KB |        0.36 |
