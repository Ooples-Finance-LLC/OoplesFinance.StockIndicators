```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId             | Mean         | Error       | StdDev      | Ratio | RatioSD | Gen0      | Gen1      | Allocated  | Alloc Ratio |
|----------- |------ |------------------- |-------------:|------------:|------------:|------:|--------:|----------:|----------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetDynamic** |  **42,571.3 μs** | **17,639.9 μs** |   **966.90 μs** |  **1.00** |    **0.03** |         **-** |         **-** | **7886.26 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetDynamic |     576.2 μs |    301.6 μs |    16.53 μs |  0.01 |    0.00 |         - |         - |  113.69 KB |        0.01 |
|            |       |                    |              |             |             |       |         |           |           |            |             |
| **Ooples**     | **10000** | **Skender.GetDynamic** | **260,319.7 μs** | **97,157.2 μs** | **5,325.51 μs** | **1.000** |    **0.02** | **9000.0000** | **1000.0000** | **79909.7 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetDynamic |   1,119.1 μs |  4,277.0 μs |   234.44 μs | 0.004 |    0.00 |         - |         - | 1071.65 KB |        0.01 |
