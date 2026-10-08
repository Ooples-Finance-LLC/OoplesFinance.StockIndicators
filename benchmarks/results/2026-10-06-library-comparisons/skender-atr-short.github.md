```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean      | Error       | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------- |----------:|------------:|----------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetAtr** |  **6.473 ms** |   **7.8404 ms** | **0.4298 ms** |  **1.00** |    **0.08** |         **-** |         **-** |  **2231.51 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetAtr |  1.022 ms |   0.6204 ms | 0.0340 ms |  0.16 |    0.01 |         - |         - |      280 KB |        0.13 |
|            |       |                |           |             |           |       |         |           |           |             |             |
| **Ooples**     | **10000** | **Skender.GetAtr** | **21.535 ms** | **101.4685 ms** | **5.5618 ms** |  **1.04** |    **0.31** | **2000.0000** | **1000.0000** | **23422.83 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetAtr |  1.475 ms |   3.1084 ms | 0.1704 ms |  0.07 |    0.02 |         - |         - |  2733.09 KB |        0.12 |
