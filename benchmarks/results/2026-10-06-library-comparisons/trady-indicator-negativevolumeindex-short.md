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
| **Ooples**     | **1000**  | **Trady(...)Index [35]** |  **3.184 ms** |  **8.5168 ms** | **0.4668 ms** |  **1.01** |    **0.18** |  **523.36 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)Index [35] |  2.769 ms |  0.5823 ms | 0.0319 ms |  0.88 |    0.10 |  933.65 KB |        1.78 |
|            |       |                      |           |            |           |       |         |            |             |
| **Ooples**     | **10000** | **Trady(...)Index [35]** | **15.183 ms** | **87.3441 ms** | **4.7876 ms** |  **1.08** |    **0.46** | **6286.68 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)Index [35] | 11.514 ms | 15.0605 ms | 0.8255 ms |  0.82 |    0.27 |  8492.5 KB |        1.35 |
