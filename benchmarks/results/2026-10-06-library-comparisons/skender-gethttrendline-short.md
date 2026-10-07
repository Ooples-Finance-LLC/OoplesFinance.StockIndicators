```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|-------------:|-------------:|------:|--------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **Skend(...)dline [22]** |  **94,503.4 μs** | **112,307.4 μs** |  **6,155.95 μs** | **1.003** |    **0.08** |  **5000.0000** | **1000.0000** |  **47401.99 KB** |       **1.000** |
| Competitor | 1000  | Skend(...)dline [22] |     791.0 μs |     670.1 μs |     36.73 μs | 0.008 |    0.00 |          - |         - |     287.9 KB |       0.006 |
|            |       |                      |              |              |              |       |         |            |           |              |             |
| **Ooples**     | **10000** | **Skend(...)dline [22]** | **694,756.7 μs** | **260,105.1 μs** | **14,257.24 μs** | **1.000** |    **0.03** | **58000.0000** | **1000.0000** | **478965.98 KB** |       **1.000** |
| Competitor | 10000 | Skend(...)dline [22] |   2,180.0 μs |     818.9 μs |     44.89 μs | 0.003 |    0.00 |          - |         - |   2808.72 KB |       0.006 |
