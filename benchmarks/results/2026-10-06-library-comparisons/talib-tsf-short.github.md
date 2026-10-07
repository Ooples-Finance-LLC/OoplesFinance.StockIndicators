```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId              | Mean         | Error        | StdDev       | Median       | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |-------------------- |-------------:|-------------:|-------------:|-------------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Tsf** |  **5,875.73 μs** |  **9,918.50 μs** |   **543.667 μs** |  **5,564.10 μs** | **1.005** |    **0.11** |         **-** |         **-** |  **3035.16 KB** |       **1.000** |
| Competitor | 1000  | TaLib.Functions.Tsf |     50.40 μs |     23.93 μs |     1.311 μs |     50.60 μs | 0.009 |    0.00 |         - |         - |    20.89 KB |       0.007 |
|            |       |                     |              |              |              |              |       |         |           |           |             |             |
| **Ooples**     | **10000** | **TaLib.Functions.Tsf** | **23,631.83 μs** | **18,897.39 μs** | **1,035.830 μs** | **23,550.80 μs** |  **1.00** |    **0.05** | **3000.0000** | **1000.0000** | **31525.13 KB** |       **1.000** |
| Competitor | 10000 | TaLib.Functions.Tsf |    508.23 μs |  7,734.34 μs |   423.945 μs |    277.50 μs |  0.02 |    0.02 |         - |         - |   161.56 KB |       0.005 |
