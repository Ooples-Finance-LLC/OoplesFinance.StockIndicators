```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0      | Allocated   | Alloc Ratio |
|----------- |------ |--------------- |----------:|----------:|----------:|------:|--------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetStc** | **12.126 ms** | **13.347 ms** | **0.7316 ms** |  **1.00** |    **0.08** |         **-** |  **3810.39 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetStc |  1.197 ms |  1.334 ms | 0.0731 ms |  0.10 |    0.01 |         - |   556.88 KB |        0.15 |
|            |       |                |           |           |           |       |         |           |             |             |
| **Ooples**     | **10000** | **Skender.GetStc** | **41.520 ms** | **58.388 ms** | **3.2004 ms** |  **1.00** |    **0.09** | **4000.0000** | **39709.69 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetStc |  3.058 ms |  2.166 ms | 0.1187 ms |  0.07 |    0.01 |         - |  5792.79 KB |        0.15 |
