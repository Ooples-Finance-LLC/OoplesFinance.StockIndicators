```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error      | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Gen2      | Allocated | Alloc Ratio |
|----------- |------ |--------------------- |----------:|-----------:|----------:|------:|--------:|----------:|----------:|----------:|----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)ogram [59]** | **15.390 ms** |  **16.331 ms** | **0.8952 ms** |  **1.00** |    **0.07** |         **-** |         **-** |         **-** |   **4.91 MB** |        **1.00** |
| Competitor | 1000  | Trady(...)ogram [59] |  9.334 ms |   3.065 ms | 0.1680 ms |  0.61 |    0.03 |         - |         - |         - |   4.71 MB |        0.96 |
|            |       |                      |           |            |           |       |         |           |           |           |           |             |
| **Ooples**     | **10000** | **Trady(...)ogram [59]** | **44.141 ms** | **113.054 ms** | **6.1969 ms** |  **1.01** |    **0.18** | **5000.0000** | **1000.0000** |         **-** |   **50.1 MB** |        **1.00** |
| Competitor | 10000 | Trady(...)ogram [59] | 35.813 ms | 163.774 ms | 8.9770 ms |  0.82 |    0.21 | 3000.0000 | 2000.0000 | 1000.0000 |   35.5 MB |        0.71 |
