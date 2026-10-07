```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error      | StdDev   | Ratio | RatioSD | Gen0       | Gen1      | Allocated | Alloc Ratio |
|----------- |------ |--------------------- |----------:|-----------:|---------:|------:|--------:|-----------:|----------:|----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)Width [34]** |  **69.83 ms** |  **45.773 ms** | **2.509 ms** |  **1.00** |    **0.04** |  **2000.0000** |         **-** |  **21.88 MB** |        **1.00** |
| Competitor | 1000  | Trady(...)Width [34] |  12.77 ms |   2.888 ms | 0.158 ms |  0.18 |    0.01 |          - |         - |    2.4 MB |        0.11 |
|            |       |                      |           |            |          |       |         |            |           |           |             |
| **Ooples**     | **10000** | **Trady(...)Width [34]** | **310.62 ms** | **153.973 ms** | **8.440 ms** |  **1.00** |    **0.03** | **27000.0000** | **1000.0000** | **222.23 MB** |        **1.00** |
| Competitor | 10000 | Trady(...)Width [34] |  28.61 ms |  59.363 ms | 3.254 ms |  0.09 |    0.01 |  1000.0000 |         - |  19.17 MB |        0.09 |
