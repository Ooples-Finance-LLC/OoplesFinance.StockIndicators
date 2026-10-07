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
| **Ooples**     | **1000**  | **Trady(...)ength [32]** |  **8.626 ms** | **12.342 ms** | **0.6765 ms** |  **1.00** |    **0.10** |         **-** |         **-** |         **-** |   **2.67 MB** |        **1.00** |
| Competitor | 1000  | Trady(...)ength [32] |  6.932 ms | 17.246 ms | 0.9453 ms |  0.81 |    0.11 |         - |         - |         - |    3.8 MB |        1.42 |
|            |       |                      |           |           |           |       |         |           |           |           |           |             |
| **Ooples**     | **10000** | **Trady(...)ength [32]** | **27.360 ms** | **97.297 ms** | **5.3332 ms** |  **1.02** |    **0.24** | **3000.0000** | **1000.0000** |         **-** |  **27.98 MB** |        **1.00** |
| Competitor | 10000 | Trady(...)ength [32] | 27.992 ms | 46.360 ms | 2.5411 ms |  1.05 |    0.19 | 2000.0000 | 1000.0000 | 1000.0000 |  26.83 MB |        0.96 |
