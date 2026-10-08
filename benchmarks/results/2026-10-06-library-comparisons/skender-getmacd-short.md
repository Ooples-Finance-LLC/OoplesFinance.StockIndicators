```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId          | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |---------------- |----------:|----------:|----------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetMacd** | **19.277 ms** | **14.518 ms** | **0.7958 ms** |  **1.00** |    **0.05** |         **-** |         **-** |  **5815.18 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetMacd |  1.175 ms |  1.418 ms | 0.0777 ms |  0.06 |    0.00 |         - |         - |   454.11 KB |        0.08 |
|            |       |                 |           |           |           |       |         |           |           |             |             |
| **Ooples**     | **10000** | **Skender.GetMacd** | **46.885 ms** | **28.062 ms** | **1.5381 ms** |  **1.00** |    **0.04** | **7000.0000** | **1000.0000** | **60283.41 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetMacd |  1.262 ms |  1.676 ms | 0.0919 ms |  0.03 |    0.00 |         - |         - |  4670.16 KB |        0.08 |
