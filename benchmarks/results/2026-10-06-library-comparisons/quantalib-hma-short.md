```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId        | Mean     | Error     | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |-------------- |---------:|----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Hma** | **3.592 ms** |  **9.428 ms** | **0.5168 ms** |  **1.01** |    **0.17** |  **307.84 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Hma | 1.315 ms | 10.420 ms | 0.5711 ms |  0.37 |    0.15 |  487.23 KB |        1.58 |
|            |       |               |          |           |           |       |         |            |             |
| **Ooples**     | **10000** | **QuanTAlib.Hma** | **5.758 ms** |  **9.192 ms** | **0.5039 ms** |  **1.00** |    **0.11** | **4117.53 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Hma | 2.245 ms |  2.988 ms | 0.1638 ms |  0.39 |    0.04 | 4842.31 KB |        1.18 |
