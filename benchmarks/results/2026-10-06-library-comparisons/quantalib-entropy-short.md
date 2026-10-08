```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId            | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0      | Allocated   | Alloc Ratio |
|----------- |------ |------------------ |----------:|----------:|----------:|------:|--------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Entropy** |  **3.383 ms** |  **8.998 ms** | **0.4932 ms** |  **1.01** |    **0.18** |         **-** |   **291.84 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Entropy | 10.727 ms | 12.632 ms | 0.6924 ms |  3.21 |    0.42 |         - |  5168.69 KB |       17.71 |
|            |       |                   |           |           |           |       |         |           |             |             |
| **Ooples**     | **10000** | **QuanTAlib.Entropy** |  **5.395 ms** | **17.726 ms** | **0.9716 ms** |  **1.02** |    **0.23** |         **-** |  **3957.96 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Entropy | 27.553 ms | 46.996 ms | 2.5760 ms |  5.22 |    0.92 | 6000.0000 | 52631.23 KB |       13.30 |
