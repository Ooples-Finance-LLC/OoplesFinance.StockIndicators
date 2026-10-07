```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId        | Mean        | Error        | StdDev       | Ratio | RatioSD | Gen0      | Allocated  | Alloc Ratio |
|----------- |------ |-------------- |------------:|-------------:|-------------:|------:|--------:|----------:|-----------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Rma** |  **2,780.9 μs** |   **1,356.4 μs** |     **74.35 μs** |  **1.00** |    **0.03** |         **-** |  **460.78 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Rma |    208.6 μs |     185.7 μs |     10.18 μs |  0.08 |    0.00 |         - |   53.46 KB |        0.12 |
|            |       |               |             |              |              |       |         |           |            |             |
| **Ooples**     | **10000** | **QuanTAlib.Rma** | **21,138.5 μs** | **236,934.4 μs** | **12,987.18 μs** |  **1.28** |    **0.99** | **1000.0000** | **11223.1 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Rma |    608.8 μs |     899.2 μs |     49.29 μs |  0.04 |    0.02 |         - |  475.08 KB |        0.04 |
