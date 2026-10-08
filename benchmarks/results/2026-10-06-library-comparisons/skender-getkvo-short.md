```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean      | Error      | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------- |----------:|-----------:|----------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetKvo** | **26.382 ms** | **15.1560 ms** | **0.8308 ms** |  **1.00** |    **0.04** |         **-** |         **-** |  **6861.15 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetKvo |  1.073 ms |  0.8734 ms | 0.0479 ms |  0.04 |    0.00 |         - |         - |    271.2 KB |        0.04 |
|            |       |                |           |            |           |       |         |           |           |             |             |
| **Ooples**     | **10000** | **Skender.GetKvo** | **71.290 ms** | **84.1354 ms** | **4.6117 ms** |  **1.00** |    **0.08** | **8000.0000** | **1000.0000** | **70437.96 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetKvo |  1.585 ms |  2.6295 ms | 0.1441 ms |  0.02 |    0.00 |         - |         - |  2644.25 KB |        0.04 |
