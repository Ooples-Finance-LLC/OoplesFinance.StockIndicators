```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error      | StdDev    | Median    | Ratio | RatioSD | Gen0      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |----------:|-----------:|----------:|----------:|------:|--------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)entum [35]** |  **3.167 ms** |   **9.033 ms** | **0.4951 ms** |  **2.938 ms** |  **1.02** |    **0.19** |         **-** |    **415.2 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)entum [35] |  5.630 ms |  14.712 ms | 0.8064 ms |  5.182 ms |  1.80 |    0.32 |         - |  1732.18 KB |        4.17 |
|            |       |                      |           |            |           |           |       |         |           |             |             |
| **Ooples**     | **10000** | **Trady(...)entum [35]** | **14.112 ms** | **170.531 ms** | **9.3474 ms** | **11.283 ms** |  **1.33** |    **1.10** |         **-** |  **5206.64 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)entum [35] | 19.144 ms |  77.288 ms | 4.2364 ms | 18.494 ms |  1.81 |    1.02 | 1000.0000 | 13802.58 KB |        2.65 |
