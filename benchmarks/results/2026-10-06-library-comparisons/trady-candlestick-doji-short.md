```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean     | Error     | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |---------:|----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...).Doji [22]** | **2.379 ms** |  **1.472 ms** | **0.0807 ms** |  **1.00** |    **0.04** |  **264.54 KB** |        **1.00** |
| Competitor | 1000  | Trady(...).Doji [22] | 1.545 ms |  1.737 ms | 0.0952 ms |  0.65 |    0.04 |  564.43 KB |        2.13 |
|            |       |                      |          |           |           |       |         |            |             |
| **Ooples**     | **10000** | **Trady(...).Doji [22]** | **7.536 ms** | **87.496 ms** | **4.7959 ms** |  **1.56** |    **1.59** | **3712.24 KB** |        **1.00** |
| Competitor | 10000 | Trady(...).Doji [22] | 5.253 ms | 15.609 ms | 0.8556 ms |  1.09 |    0.85 | 4885.95 KB |        1.32 |
