```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId              | Mean      | Error      | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |-------------------- |----------:|-----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetDonchian** |  **4.002 ms** |   **2.812 ms** | **0.1542 ms** |  **1.00** |    **0.05** |  **882.92 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetDonchian |  1.298 ms |   1.043 ms | 0.0572 ms |  0.32 |    0.02 |  265.19 KB |        0.30 |
|            |       |                     |           |            |           |       |         |            |             |
| **Ooples**     | **10000** | **Skender.GetDonchian** | **27.306 ms** | **158.137 ms** | **8.6680 ms** |  **1.09** |    **0.47** | **9908.83 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetDonchian |  5.650 ms |   1.170 ms | 0.0641 ms |  0.22 |    0.07 | 2585.88 KB |        0.26 |
