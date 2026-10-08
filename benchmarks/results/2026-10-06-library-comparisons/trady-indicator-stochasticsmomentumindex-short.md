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
| **Ooples**     | **1000**  | **Trady(...)Index [40]** |  **5.209 ms** |  **12.243 ms** | **0.6711 ms** |  **1.01** |    **0.15** |         **-** |         **-** |         **-** |   **731.75 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)Index [40] | 13.737 ms |   7.103 ms | 0.3894 ms |  2.66 |    0.29 |         - |         - |         - |  6279.11 KB |        8.58 |
|            |       |                      |           |            |           |       |         |           |           |           |             |             |
| **Ooples**     | **10000** | **Trady(...)Index [40]** | **14.395 ms** |  **31.615 ms** | **1.7329 ms** |  **1.01** |    **0.15** |         **-** |         **-** |         **-** |  **8411.17 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)Index [40] | 57.319 ms | 139.165 ms | 7.6281 ms |  4.02 |    0.65 | 4000.0000 | 2000.0000 | 1000.0000 | 45040.05 KB |        5.35 |
