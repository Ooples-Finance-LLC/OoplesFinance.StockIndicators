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
| **Ooples**     | **1000**  | **Trady(...)cator [40]** | **24.186 ms** |  **20.008 ms** | **1.0967 ms** |  **1.00** |    **0.06** |  **1000.0000** |         **-** |         **-** |   **8.34 MB** |        **1.00** |
| Competitor | 1000  | Trady(...)cator [40] |  8.149 ms |   8.305 ms | 0.4552 ms |  0.34 |    0.02 |          - |         - |         - |   3.87 MB |        0.46 |
|            |       |                      |           |            |           |       |         |            |           |           |           |             |
| **Ooples**     | **10000** | **Trady(...)cator [40]** | **62.214 ms** |  **25.991 ms** | **1.4247 ms** |  **1.00** |    **0.03** | **10000.0000** | **1000.0000** |         **-** |  **85.37 MB** |        **1.00** |
| Competitor | 10000 | Trady(...)cator [40] | 31.013 ms | 158.036 ms | 8.6625 ms |  0.50 |    0.12 |  3000.0000 | 2000.0000 | 1000.0000 |   29.1 MB |        0.34 |
