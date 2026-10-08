```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId          | Mean      | Error    | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |---------------- |----------:|---------:|----------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetDoji** |  **6.517 ms** | **9.413 ms** | **0.5160 ms** |  **1.00** |    **0.10** |         **-** |         **-** |  **1830.23 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetDoji |  2.163 ms | 1.830 ms | 0.1003 ms |  0.33 |    0.03 |         - |         - |   396.28 KB |        0.22 |
|            |       |                 |           |          |           |       |         |           |           |             |             |
| **Ooples**     | **10000** | **Skender.GetDoji** | **24.482 ms** | **6.842 ms** | **0.3750 ms** |  **1.00** |    **0.02** | **2000.0000** | **1000.0000** | **19328.09 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetDoji |  7.371 ms | 2.655 ms | 0.1456 ms |  0.30 |    0.01 |         - |         - |  3879.96 KB |        0.20 |
