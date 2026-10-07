```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0       | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------- |------------:|-------------:|------------:|------:|--------:|-----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetCci** | **17,368.9 μs** |  **20,067.9 μs** | **1,099.99 μs** |  **1.00** |    **0.08** |  **1000.0000** |         **-** |  **8993.09 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetCci |    419.2 μs |     263.0 μs |    14.41 μs |  0.02 |    0.00 |          - |         - |    175.8 KB |        0.02 |
|            |       |                |             |              |             |       |         |            |           |             |             |
| **Ooples**     | **10000** | **Skender.GetCci** | **49,659.9 μs** | **116,244.7 μs** | **6,371.77 μs** |  **1.01** |    **0.15** | **11000.0000** | **1000.0000** | **92429.41 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetCci |  1,220.8 μs |   4,446.0 μs |   243.70 μs |  0.02 |    0.01 |          - |         - |  1696.35 KB |        0.02 |
