```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0      | Allocated   | Alloc Ratio |
|----------- |------ |--------------- |----------:|----------:|----------:|------:|--------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetCmf** |  **8.244 ms** |  **3.459 ms** | **0.1896 ms** |  **1.00** |    **0.03** |         **-** |  **1271.16 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetCmf |  1.231 ms |  1.264 ms | 0.0693 ms |  0.15 |    0.01 |         - |   385.78 KB |        0.30 |
|            |       |                |           |           |           |       |         |           |             |             |
| **Ooples**     | **10000** | **Skender.GetCmf** | **23.092 ms** | **73.588 ms** | **4.0336 ms** |  **1.02** |    **0.23** | **1000.0000** | **13877.69 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetCmf |  2.293 ms |  9.426 ms | 0.5167 ms |  0.10 |    0.03 |         - |  3787.52 KB |        0.27 |
