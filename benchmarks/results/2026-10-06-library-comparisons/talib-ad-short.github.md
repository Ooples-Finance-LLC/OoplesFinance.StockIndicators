```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId             | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0      | Allocated  | Alloc Ratio |
|----------- |------ |------------------- |-------------:|-------------:|-------------:|------:|--------:|----------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Ad** | **11,519.67 μs** |  **5,281.29 μs** |   **289.485 μs** | **1.000** |    **0.03** |         **-** |  **1247.8 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Ad |     36.43 μs |     42.01 μs |     2.303 μs | 0.003 |    0.00 |         - |   13.02 KB |        0.01 |
|            |       |                    |              |              |              |       |         |           |            |             |
| **Ooples**     | **10000** | **TaLib.Functions.Ad** | **28,996.87 μs** | **85,552.37 μs** | **4,689.414 μs** | **1.018** |    **0.21** | **1000.0000** | **15405.3 KB** |       **1.000** |
| Competitor | 10000 | TaLib.Functions.Ad |     56.90 μs |     76.03 μs |     4.168 μs | 0.002 |    0.00 |         - |   79.49 KB |       0.005 |
