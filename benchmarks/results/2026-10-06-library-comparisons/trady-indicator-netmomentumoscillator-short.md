```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Gen2      | Allocated | Alloc Ratio |
|----------- |------ |--------------------- |----------:|----------:|----------:|------:|--------:|----------:|----------:|----------:|----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)lator [37]** |  **8.754 ms** | **11.784 ms** | **0.6459 ms** |  **1.00** |    **0.09** |         **-** |         **-** |         **-** |   **2.76 MB** |        **1.00** |
| Competitor | 1000  | Trady(...)lator [37] |  7.179 ms |  3.624 ms | 0.1987 ms |  0.82 |    0.06 |         - |         - |         - |   4.48 MB |        1.62 |
|            |       |                      |           |           |           |       |         |           |           |           |           |             |
| **Ooples**     | **10000** | **Trady(...)lator [37]** | **32.417 ms** | **33.641 ms** | **1.8440 ms** |  **1.00** |    **0.07** | **3000.0000** | **1000.0000** |         **-** |  **28.91 MB** |        **1.00** |
| Competitor | 10000 | Trady(...)lator [37] | 36.129 ms | 93.112 ms | 5.1038 ms |  1.12 |    0.15 | 3000.0000 | 2000.0000 | 1000.0000 |  30.95 MB |        1.07 |
