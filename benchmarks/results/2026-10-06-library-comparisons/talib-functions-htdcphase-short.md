```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean         | Error       | StdDev    | Ratio | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|------------:|----------:|------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)Phase [25]** |   **126.881 ms** |  **10.7770 ms** | **0.5907 ms** |  **1.00** |  **9000.0000** | **1000.0000** |  **76195.47 KB** |       **1.000** |
| Competitor | 1000  | TaLib(...)Phase [25] |     2.332 ms |   0.7424 ms | 0.0407 ms |  0.02 |          - |         - |     37.82 KB |       0.000 |
|            |       |                      |              |             |           |       |            |           |              |             |
| **Ooples**     | **10000** | **TaLib(...)Phase [25]** | **1,314.150 ms** | **163.6412 ms** | **8.9697 ms** | **1.000** | **97000.0000** | **1000.0000** | **801876.85 KB** |       **1.000** |
| Competitor | 10000 | TaLib(...)Phase [25] |     5.667 ms |   0.4682 ms | 0.0257 ms | 0.004 |          - |         - |    326.88 KB |       0.000 |
