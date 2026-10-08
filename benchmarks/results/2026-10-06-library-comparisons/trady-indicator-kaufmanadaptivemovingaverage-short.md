```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error     | StdDev   | Ratio | RatioSD | Gen0      | Gen1      | Allocated | Alloc Ratio |
|----------- |------ |--------------------- |----------:|----------:|---------:|------:|--------:|----------:|----------:|----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)erage [44]** |  **31.87 ms** |  **3.856 ms** | **0.211 ms** |  **1.00** |    **0.01** |         **-** |         **-** |   **7.94 MB** |        **1.00** |
| Competitor | 1000  | Trady(...)erage [44] |  31.31 ms | 33.236 ms | 1.822 ms |  0.98 |    0.05 |         - |         - |   2.61 MB |        0.33 |
|            |       |                      |           |           |          |       |         |           |           |           |             |
| **Ooples**     | **10000** | **Trady(...)erage [44]** | **123.36 ms** | **46.176 ms** | **2.531 ms** |  **1.00** |    **0.03** | **9000.0000** | **1000.0000** |  **81.68 MB** |        **1.00** |
| Competitor | 10000 | Trady(...)erage [44] | 259.88 ms | 30.993 ms | 1.699 ms |  2.11 |    0.04 | 2000.0000 | 1000.0000 |  24.67 MB |        0.30 |
