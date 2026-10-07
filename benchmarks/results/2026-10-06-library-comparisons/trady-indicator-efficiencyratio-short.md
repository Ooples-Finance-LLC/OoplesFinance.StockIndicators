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
| **Ooples**     | **1000**  | **Trady(...)Ratio [31]** |  **5.622 ms** |  **9.746 ms** | **0.5342 ms** |  **1.01** |    **0.11** |  **597.42 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)Ratio [31] |  5.399 ms |  2.619 ms | 0.1436 ms |  0.97 |    0.08 |  912.76 KB |        1.53 |
|            |       |                      |           |           |           |       |         |            |             |
| **Ooples**     | **10000** | **Trady(...)Ratio [31]** | **10.429 ms** | **26.634 ms** | **1.4599 ms** |  **1.01** |    **0.17** | **7069.02 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)Ratio [31] | 12.077 ms | 15.618 ms | 0.8561 ms |  1.17 |    0.15 |  8335.2 KB |        1.18 |
