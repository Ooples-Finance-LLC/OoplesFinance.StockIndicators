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
| **Ooples**     | **1000**  | **Trady(...)nnels [31]** |  **6.531 ms** |  **9.567 ms** | **0.5244 ms** |  **1.00** |    **0.10** |         **-** |         **-** |         **-** |   **1.95 MB** |        **1.00** |
| Competitor | 1000  | Trady(...)nnels [31] |  6.124 ms |  1.425 ms | 0.0781 ms |  0.94 |    0.06 |         - |         - |         - |   3.86 MB |        1.98 |
|            |       |                      |           |           |           |       |         |           |           |           |           |             |
| **Ooples**     | **10000** | **Trady(...)nnels [31]** | **22.050 ms** | **19.181 ms** | **1.0514 ms** |  **1.00** |    **0.06** | **2000.0000** | **1000.0000** |         **-** |  **20.52 MB** |        **1.00** |
| Competitor | 10000 | Trady(...)nnels [31] | 39.194 ms | 68.984 ms | 3.7813 ms |  1.78 |    0.17 | 3000.0000 | 2000.0000 | 1000.0000 |   32.4 MB |        1.58 |
