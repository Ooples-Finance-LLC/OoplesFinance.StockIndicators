```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error      | StdDev     | Ratio | RatioSD | Gen0      | Gen1      | Gen2      | Allocated | Alloc Ratio |
|----------- |------ |--------------------- |----------:|-----------:|-----------:|------:|--------:|----------:|----------:|----------:|----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)gence [50]** | **17.372 ms** |  **14.512 ms** |  **0.7955 ms** |  **1.00** |    **0.06** |         **-** |         **-** |         **-** |   **5.35 MB** |        **1.00** |
| Competitor | 1000  | Trady(...)gence [50] |  7.136 ms |   2.010 ms |  0.1102 ms |  0.41 |    0.02 |         - |         - |         - |   4.47 MB |        0.84 |
|            |       |                      |           |            |            |       |         |           |           |           |           |             |
| **Ooples**     | **10000** | **Trady(...)gence [50]** | **55.351 ms** | **211.245 ms** | **11.5791 ms** |  **1.03** |    **0.29** | **6000.0000** | **1000.0000** |         **-** |  **54.54 MB** |        **1.00** |
| Competitor | 10000 | Trady(...)gence [50] | 25.927 ms |  75.024 ms |  4.1123 ms |  0.48 |    0.12 | 3000.0000 | 2000.0000 | 1000.0000 |  33.88 MB |        0.62 |
