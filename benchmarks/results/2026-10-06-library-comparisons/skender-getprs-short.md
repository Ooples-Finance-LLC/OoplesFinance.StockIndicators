```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean      | Error       | StdDev     | Median    | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------- |----------:|------------:|-----------:|----------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetPrs** | **21.143 ms** |  **12.8877 ms** |  **0.7064 ms** | **21.120 ms** |  **1.00** |    **0.04** |         **-** |         **-** |  **6084.75 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetPrs |  1.226 ms |   0.7984 ms |  0.0438 ms |  1.220 ms |  0.06 |    0.00 |         - |         - |   253.31 KB |        0.04 |
|            |       |                |           |             |            |           |       |         |           |           |             |             |
| **Ooples**     | **10000** | **Skender.GetPrs** | **83.610 ms** | **206.6845 ms** | **11.3291 ms** | **79.860 ms** |  **1.01** |    **0.16** | **7000.0000** | **1000.0000** | **62413.09 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetPrs |  3.173 ms |  56.4289 ms |  3.0931 ms |  1.442 ms |  0.04 |    0.03 |         - |         - |  2459.04 KB |        0.04 |
