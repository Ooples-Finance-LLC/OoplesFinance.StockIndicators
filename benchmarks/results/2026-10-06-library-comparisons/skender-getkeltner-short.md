```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId             | Mean      | Error    | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |------------------- |----------:|---------:|----------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetKeltner** |  **7.635 ms** | **6.904 ms** | **0.3785 ms** |  **1.00** |    **0.06** |         **-** |         **-** |  **2335.12 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetKeltner |  1.424 ms | 1.190 ms | 0.0652 ms |  0.19 |    0.01 |         - |         - |    473.9 KB |        0.20 |
|            |       |                    |           |          |           |       |         |           |           |             |             |
| **Ooples**     | **10000** | **Skender.GetKeltner** | **18.240 ms** | **6.875 ms** | **0.3768 ms** |  **1.00** |    **0.03** | **2000.0000** | **1000.0000** | **24449.85 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetKeltner |  2.484 ms | 7.057 ms | 0.3868 ms |  0.14 |    0.02 |         - |         - |  4657.49 KB |        0.19 |
