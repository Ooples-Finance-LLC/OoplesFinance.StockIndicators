```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId           | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0      | Gen1      | Allocated  | Alloc Ratio |
|----------- |------ |----------------- |------------:|------------:|------------:|------:|--------:|----------:|----------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetStoch** | **11,687.9 μs** | **11,306.8 μs** |   **619.76 μs** |  **1.00** |    **0.06** |         **-** |         **-** | **2642.51 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetStoch |    825.4 μs |  1,117.5 μs |    61.25 μs |  0.07 |    0.01 |         - |         - |  272.27 KB |        0.10 |
|            |       |                  |             |             |             |       |         |           |           |            |             |
| **Ooples**     | **10000** | **Skender.GetStoch** | **45,639.7 μs** | **18,377.5 μs** | **1,007.33 μs** |  **1.00** |    **0.03** | **3000.0000** | **1000.0000** | **27774.2 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetStoch |  2,002.8 μs |  4,902.2 μs |   268.70 μs |  0.04 |    0.01 |         - |         - | 2654.15 KB |        0.10 |
