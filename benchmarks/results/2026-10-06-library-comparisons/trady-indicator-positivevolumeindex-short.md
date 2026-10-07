```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |----------:|----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)Index [35]** |  **3.299 ms** |  **9.857 ms** | **0.5403 ms** |  **1.02** |    **0.20** |   **517.2 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)Index [35] |  2.827 ms |  2.226 ms | 0.1220 ms |  0.87 |    0.12 |  933.04 KB |        1.80 |
|            |       |                      |           |           |           |       |         |            |             |
| **Ooples**     | **10000** | **Trady(...)Index [35]** | **19.611 ms** | **32.162 ms** | **1.7629 ms** |  **1.01** |    **0.11** | **6266.63 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)Index [35] | 11.088 ms | 11.116 ms | 0.6093 ms |  0.57 |    0.05 | 8495.08 KB |        1.36 |
