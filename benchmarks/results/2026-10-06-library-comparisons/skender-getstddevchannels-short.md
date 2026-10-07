```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean       | Error       | StdDev     | Ratio | RatioSD | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|------------:|-----------:|------:|--------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **Skend(...)nnels [25]** |  **29.527 ms** |  **23.0718 ms** |  **1.2646 ms** |  **1.00** |    **0.05** |  **1000.0000** |         **-** |  **12141.75 KB** |        **1.00** |
| Competitor | 1000  | Skend(...)nnels [25] |   1.101 ms |   2.2552 ms |  0.1236 ms |  0.04 |    0.00 |          - |         - |    358.91 KB |        0.03 |
|            |       |                      |            |             |            |       |         |            |           |              |             |
| **Ooples**     | **10000** | **Skend(...)nnels [25]** | **135.891 ms** | **285.0724 ms** | **15.6258 ms** |  **1.01** |    **0.15** | **14000.0000** | **4000.0000** | **121465.75 KB** |        **1.00** |
| Competitor | 10000 | Skend(...)nnels [25] |   2.530 ms |   0.5113 ms |  0.0280 ms |  0.02 |    0.00 |          - |         - |   3520.95 KB |        0.03 |
