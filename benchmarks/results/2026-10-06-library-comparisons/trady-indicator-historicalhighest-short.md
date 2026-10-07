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
| **Ooples**     | **1000**  | **Trady(...)ghest [33]** |  **2.255 ms** |  **1.828 ms** | **0.1002 ms** |  **1.00** |    **0.05** |  **369.42 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)ghest [33] |  2.668 ms |  1.123 ms | 0.0616 ms |  1.18 |    0.05 |  900.65 KB |        2.44 |
|            |       |                      |           |           |           |       |         |            |             |
| **Ooples**     | **10000** | **Trady(...)ghest [33]** | **35.429 ms** | **11.172 ms** | **0.6124 ms** |  **1.00** |    **0.02** | **4733.16 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)ghest [33] |  8.066 ms | 11.767 ms | 0.6450 ms |  0.23 |    0.02 | 8171.66 KB |        1.73 |
