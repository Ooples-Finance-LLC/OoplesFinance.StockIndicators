```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId           | Mean      | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |----------------- |----------:|------------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetGator** |  **3.919 ms** |   **2.4394 ms** | **0.1337 ms** |  **1.00** |    **0.04** |  **477.84 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetGator |  1.628 ms |   0.9265 ms | 0.0508 ms |  0.42 |    0.02 |   355.8 KB |        0.74 |
|            |       |                  |           |             |           |       |         |            |             |
| **Ooples**     | **10000** | **Skender.GetGator** | **21.325 ms** | **171.0970 ms** | **9.3784 ms** |  **1.12** |    **0.57** |  **5786.3 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetGator |  2.853 ms |   0.9080 ms | 0.0498 ms |  0.15 |    0.05 | 3482.78 KB |        0.60 |
