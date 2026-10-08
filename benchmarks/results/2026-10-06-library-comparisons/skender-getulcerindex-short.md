```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean         | Error       | StdDev      | Ratio | RatioSD | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|------------:|------------:|------:|--------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **Skend(...)Index [21]** |  **80,609.9 μs** | **68,318.4 μs** | **3,744.76 μs** |  **1.00** |    **0.06** |  **3000.0000** |         **-** |  **31336.82 KB** |       **1.000** |
| Competitor | 1000  | Skend(...)Index [21] |     894.2 μs |    462.7 μs |    25.36 μs |  0.01 |    0.00 |          - |         - |    113.07 KB |       0.004 |
|            |       |                      |              |             |             |       |         |            |           |              |             |
| **Ooples**     | **10000** | **Skend(...)Index [21]** | **321,705.5 μs** | **28,230.4 μs** | **1,547.41 μs** |  **1.00** |    **0.01** | **39000.0000** | **1000.0000** | **322606.18 KB** |       **1.000** |
| Competitor | 10000 | Skend(...)Index [21] |   5,438.1 μs |  2,524.3 μs |   138.36 μs |  0.02 |    0.00 |          - |         - |   1071.36 KB |       0.003 |
