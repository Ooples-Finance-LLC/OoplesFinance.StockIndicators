```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error      | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Gen2      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |----------:|-----------:|----------:|------:|--------:|----------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)+Full [42]** |  **4.469 ms** |  **9.2376 ms** | **0.5063 ms** |  **1.01** |    **0.14** |         **-** |         **-** |         **-** |   **704.38 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)+Full [42] |  9.902 ms |  0.5092 ms | 0.0279 ms |  2.23 |    0.21 |         - |         - |         - |  4895.02 KB |        6.95 |
|            |       |                      |           |            |           |       |         |           |           |           |             |             |
| **Ooples**     | **10000** | **Trady(...)+Full [42]** | **11.517 ms** | **45.3186 ms** | **2.4841 ms** |  **1.04** |    **0.29** |         **-** |         **-** |         **-** |  **8141.58 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)+Full [42] | 44.829 ms | 79.2942 ms | 4.3464 ms |  4.03 |    0.90 | 4000.0000 | 2000.0000 | 1000.0000 | 37945.38 KB |        4.66 |
