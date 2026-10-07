```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error      | StdDev    | Ratio | RatioSD | Gen0       | Gen1      | Gen2      | Allocated | Alloc Ratio |
|----------- |------ |--------------------- |----------:|-----------:|----------:|------:|--------:|-----------:|----------:|----------:|----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)cator [41]** | **24.049 ms** |  **17.229 ms** | **0.9444 ms** |  **1.00** |    **0.05** |  **1000.0000** |         **-** |         **-** |   **8.34 MB** |        **1.00** |
| Competitor | 1000  | Trady(...)cator [41] |  7.996 ms |  12.062 ms | 0.6611 ms |  0.33 |    0.03 |          - |         - |         - |   3.87 MB |        0.46 |
|            |       |                      |           |            |           |       |         |            |           |           |           |             |
| **Ooples**     | **10000** | **Trady(...)cator [41]** | **60.607 ms** |   **7.702 ms** | **0.4222 ms** |  **1.00** |    **0.01** | **10000.0000** | **1000.0000** |         **-** |  **85.37 MB** |        **1.00** |
| Competitor | 10000 | Trady(...)cator [41] | 31.565 ms | 174.246 ms | 9.5510 ms |  0.52 |    0.14 |  3000.0000 | 2000.0000 | 1000.0000 |  29.03 MB |        0.34 |
