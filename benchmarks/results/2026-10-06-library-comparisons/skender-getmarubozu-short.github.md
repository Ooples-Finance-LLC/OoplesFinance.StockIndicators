```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId              | Mean      | Error      | StdDev    | Ratio | RatioSD | Gen0      | Allocated   | Alloc Ratio |
|----------- |------ |-------------------- |----------:|-----------:|----------:|------:|--------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetMarubozu** |  **5.868 ms** |  **7.8840 ms** | **0.4321 ms** |  **1.00** |    **0.09** |         **-** |  **1353.67 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetMarubozu |  2.292 ms |  0.9838 ms | 0.0539 ms |  0.39 |    0.03 |         - |    395.3 KB |        0.29 |
|            |       |                     |           |            |           |       |         |           |             |             |
| **Ooples**     | **10000** | **Skender.GetMarubozu** | **20.529 ms** | **18.5758 ms** | **1.0182 ms** |  **1.00** |    **0.06** | **1000.0000** | **14559.88 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetMarubozu |  8.140 ms |  4.4440 ms | 0.2436 ms |  0.40 |    0.02 |         - |  3879.96 KB |        0.27 |
