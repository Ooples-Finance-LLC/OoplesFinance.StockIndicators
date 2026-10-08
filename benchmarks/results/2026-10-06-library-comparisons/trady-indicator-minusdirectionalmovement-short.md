```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |----------:|----------:|----------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)ement [40]** |  **5.978 ms** |  **9.305 ms** | **0.5100 ms** |  **1.01** |    **0.11** |         **-** |         **-** |  **1835.17 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)ement [40] |  2.500 ms | 11.792 ms | 0.6463 ms |  0.42 |    0.10 |         - |         - |   745.65 KB |        0.41 |
|            |       |                      |           |           |           |       |         |           |           |             |             |
| **Ooples**     | **10000** | **Trady(...)ement [40]** | **17.131 ms** |  **7.365 ms** | **0.4037 ms** |  **1.00** |    **0.03** | **2000.0000** | **1000.0000** | **19421.15 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)ement [40] |  7.689 ms |  5.193 ms | 0.2846 ms |  0.45 |    0.02 |         - |         - |  6621.92 KB |        0.34 |
