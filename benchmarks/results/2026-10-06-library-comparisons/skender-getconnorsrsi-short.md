```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean         | Error         | StdDev     | Ratio | RatioSD | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|--------------:|-----------:|------:|--------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **Skend(...)rsRsi [21]** |   **268.540 ms** |   **230.3555 ms** | **12.6266 ms** | **1.001** |    **0.06** |  **6000.0000** |         **-** |  **55360.97 KB** |       **1.000** |
| Competitor | 1000  | Skend(...)rsRsi [21] |     1.126 ms |     0.0987 ms |  0.0054 ms | 0.004 |    0.00 |          - |         - |    405.31 KB |       0.007 |
|            |       |                      |              |               |            |       |         |            |           |              |             |
| **Ooples**     | **10000** | **Skend(...)rsRsi [21]** | **2,883.145 ms** | **1,243.8324 ms** | **68.1787 ms** | **1.000** |    **0.03** | **74000.0000** | **1000.0000** | **605805.09 KB** |       **1.000** |
| Competitor | 10000 | Skend(...)rsRsi [21] |     6.317 ms |    19.7890 ms |  1.0847 ms | 0.002 |    0.00 |          - |         - |   3992.94 KB |       0.007 |
