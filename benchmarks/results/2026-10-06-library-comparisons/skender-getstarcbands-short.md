```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error      | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |----------:|-----------:|----------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skend(...)Bands [21]** |  **6.372 ms** |  **9.0244 ms** | **0.4947 ms** |  **1.00** |    **0.09** |         **-** |         **-** |  **1996.44 KB** |        **1.00** |
| Competitor | 1000  | Skend(...)Bands [21] |  1.151 ms |  0.9124 ms | 0.0500 ms |  0.18 |    0.01 |         - |         - |   417.66 KB |        0.21 |
|            |       |                      |           |            |           |       |         |           |           |             |             |
| **Ooples**     | **10000** | **Skend(...)Bands [21]** | **15.042 ms** |  **5.9507 ms** | **0.3262 ms** |  **1.00** |    **0.03** | **2000.0000** | **1000.0000** | **21010.55 KB** |        **1.00** |
| Competitor | 10000 | Skend(...)Bands [21] |  2.526 ms | 13.3369 ms | 0.7310 ms |  0.17 |    0.04 |         - |         - |  4099.95 KB |        0.20 |
