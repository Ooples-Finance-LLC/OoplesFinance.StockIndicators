```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId             | Mean      | Error      | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |------------------- |----------:|-----------:|----------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Variance** | **14.294 ms** | **12.4752 ms** | **0.6838 ms** |  **1.00** |    **0.06** |         **-** |         **-** |   **4501.7 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Variance |  2.469 ms |  0.0921 ms | 0.0050 ms |  0.17 |    0.01 |         - |         - |   515.36 KB |        0.11 |
|            |       |                    |           |            |           |       |         |           |           |             |             |
| **Ooples**     | **10000** | **QuanTAlib.Variance** | **70.026 ms** | **51.9806 ms** | **2.8492 ms** |  **1.00** |    **0.05** | **5000.0000** | **1000.0000** | **46269.53 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Variance |  7.410 ms |  1.8234 ms | 0.0999 ms |  0.11 |    0.00 |         - |         - |  4208.05 KB |        0.09 |
