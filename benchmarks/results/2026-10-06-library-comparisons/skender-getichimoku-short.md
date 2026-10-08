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
| **Ooples**     | **1000**  | **Skender.GetIchimoku** |  **1.706 ms** |  **2.6421 ms** | **0.1448 ms** |  **1.00** |    **0.10** |   **601.3 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetIchimoku |  8.239 ms |  0.8771 ms | 0.0481 ms |  4.85 |    0.34 |  321.47 KB |        0.53 |
|            |       |                     |           |            |           |       |         |            |             |
| **Ooples**     | **10000** | **Skender.GetIchimoku** |  **6.024 ms** | **24.5066 ms** | **1.3433 ms** |  **1.04** |    **0.30** | **5994.58 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetIchimoku | 14.665 ms | 37.8807 ms | 2.0764 ms |  2.53 |    0.62 |  3142.8 KB |        0.52 |
