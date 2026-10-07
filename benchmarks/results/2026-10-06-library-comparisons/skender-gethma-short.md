```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean     | Error      | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------- |---------:|-----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetHma** | **3.512 ms** | **11.0688 ms** | **0.6067 ms** |  **1.02** |    **0.21** |  **307.84 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetHma | 1.513 ms |  7.3829 ms | 0.4047 ms |  0.44 |    0.12 |  422.93 KB |        1.37 |
|            |       |                |          |            |           |       |         |            |             |
| **Ooples**     | **10000** | **Skender.GetHma** | **7.987 ms** |  **2.1924 ms** | **0.1202 ms** |  **1.00** |    **0.02** | **4114.58 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetHma | 2.918 ms |  0.9536 ms | 0.0523 ms |  0.37 |    0.01 | 4357.41 KB |        1.06 |
