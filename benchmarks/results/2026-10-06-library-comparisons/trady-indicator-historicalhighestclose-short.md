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
| **Ooples**     | **1000**  | **Trady(...)Close [38]** |  **2.208 ms** |  **3.3073 ms** | **0.1813 ms** |  **1.00** |    **0.10** |  **369.42 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)Close [38] |  2.345 ms |  0.4026 ms | 0.0221 ms |  1.07 |    0.08 |   900.6 KB |        2.44 |
|            |       |                      |           |            |           |       |         |            |             |
| **Ooples**     | **10000** | **Trady(...)Close [38]** | **31.475 ms** | **11.8594 ms** | **0.6501 ms** |  **1.00** |    **0.03** | **4732.83 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)Close [38] |  8.804 ms |  1.2681 ms | 0.0695 ms |  0.28 |    0.01 | 8171.66 KB |        1.73 |
