```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error      | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |----------:|-----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)tHigh [27]** |  **1.954 ms** |  **2.1846 ms** | **0.1197 ms** |  **1.00** |    **0.08** |  **369.38 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)tHigh [27] |  2.802 ms |  0.3744 ms | 0.0205 ms |  1.44 |    0.08 |   833.8 KB |        2.26 |
|            |       |                      |           |            |           |       |         |            |             |
| **Ooples**     | **10000** | **Trady(...)tHigh [27]** | **10.446 ms** |  **7.2252 ms** | **0.3960 ms** |  **1.00** |    **0.05** | **4732.79 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)tHigh [27] |  8.781 ms | 27.0053 ms | 1.4803 ms |  0.84 |    0.13 | 7541.76 KB |        1.59 |
