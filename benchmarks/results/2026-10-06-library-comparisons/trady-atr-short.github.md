```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated | Alloc Ratio |
|----------- |------ |--------------------- |----------:|----------:|----------:|------:|--------:|----------:|----------:|----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)Range [32]** |  **4.960 ms** |  **7.254 ms** | **0.3976 ms** |  **1.00** |    **0.10** |         **-** |         **-** |   **1.78 MB** |        **1.00** |
| Competitor | 1000  | Trady(...)Range [32] |  4.150 ms |  1.700 ms | 0.0932 ms |  0.84 |    0.06 |         - |         - |   1.96 MB |        1.10 |
|            |       |                      |           |           |           |       |         |           |           |           |             |
| **Ooples**     | **10000** | **Trady(...)Range [32]** | **18.051 ms** |  **6.772 ms** | **0.3712 ms** |  **1.00** |    **0.03** | **2000.0000** | **1000.0000** |  **18.88 MB** |        **1.00** |
| Competitor | 10000 | Trady(...)Range [32] | 12.525 ms | 18.698 ms | 1.0249 ms |  0.69 |    0.05 | 1000.0000 |         - |  17.96 MB |        0.95 |
