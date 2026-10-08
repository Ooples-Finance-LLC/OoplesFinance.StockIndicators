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
| **Ooples**     | **1000**  | **Skend(...)lysis [22]** |  **96,611.6 μs** | **532,930.8 μs** | **29,211.74 μs** | **1.080** |    **0.45** |  **3000.0000** |         **-** |  **28597.27 KB** |        **1.00** |
| Competitor | 1000  | Skend(...)lysis [22] |     667.8 μs |   1,376.4 μs |     75.44 μs | 0.007 |    0.00 |          - |         - |    289.55 KB |        0.01 |
|            |       |                      |              |              |              |       |         |            |           |              |             |
| **Ooples**     | **10000** | **Skend(...)lysis [22]** | **417,103.3 μs** |  **63,394.0 μs** |  **3,474.84 μs** | **1.000** |    **0.01** | **35000.0000** | **1000.0000** | **292514.27 KB** |       **1.000** |
| Competitor | 10000 | Skend(...)lysis [22] |   1,648.5 μs |   2,630.1 μs |    144.16 μs | 0.004 |    0.00 |          - |         - |   2819.82 KB |       0.010 |
