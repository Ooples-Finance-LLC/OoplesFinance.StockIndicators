```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error       | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |----------:|------------:|----------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skend(...)Trend [21]** | **19.011 ms** |  **13.5496 ms** | **0.7427 ms** |  **1.00** |    **0.05** |         **-** |         **-** |  **6023.97 KB** |        **1.00** |
| Competitor | 1000  | Skend(...)Trend [21] |  1.584 ms |   1.6918 ms | 0.0927 ms |  0.08 |    0.01 |         - |         - |   358.23 KB |        0.06 |
|            |       |                      |           |             |           |       |         |           |           |             |             |
| **Ooples**     | **10000** | **Skend(...)Trend [21]** | **90.317 ms** | **135.0020 ms** | **7.3999 ms** |  **1.00** |    **0.10** | **7000.0000** | **1000.0000** | **61993.91 KB** |        **1.00** |
| Competitor | 10000 | Skend(...)Trend [21] |  1.994 ms |   0.8313 ms | 0.0456 ms |  0.02 |    0.00 |         - |         - |  3507.65 KB |        0.06 |
