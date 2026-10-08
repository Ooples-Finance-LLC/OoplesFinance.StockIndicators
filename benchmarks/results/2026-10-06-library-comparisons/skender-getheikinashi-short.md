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
| **Ooples**     | **1000**  | **Skend(...)nAshi [21]** |  **2.331 ms** |  **2.711 ms** | **0.1486 ms** |  **1.00** |    **0.08** |  **333.53 KB** |        **1.00** |
| Competitor | 1000  | Skend(...)nAshi [21] |  1.097 ms |  1.274 ms | 0.0698 ms |  0.47 |    0.04 |  336.65 KB |        1.01 |
|            |       |                      |           |           |           |       |         |            |             |
| **Ooples**     | **10000** | **Skend(...)nAshi [21]** | **19.546 ms** | **22.418 ms** | **1.2288 ms** |  **1.00** |    **0.08** | **4388.99 KB** |        **1.00** |
| Competitor | 10000 | Skend(...)nAshi [21] |  3.136 ms |  2.948 ms | 0.1616 ms |  0.16 |    0.01 | 3298.56 KB |        0.75 |
