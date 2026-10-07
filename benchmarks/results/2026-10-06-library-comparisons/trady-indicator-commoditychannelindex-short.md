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
| **Ooples**     | **1000**  | **Trady(...)Index [37]** |  **10.25 ms** |  **8.864 ms** | **0.486 ms** |  **1.00** |    **0.06** |         **-** |         **-** |   **3.07 MB** |        **1.00** |
| Competitor | 1000  | Trady(...)Index [37] |  63.61 ms | 40.349 ms | 2.212 ms |  6.22 |    0.32 |         - |         - |   4.65 MB |        1.52 |
|            |       |                      |           |           |          |       |         |           |           |           |             |
| **Ooples**     | **10000** | **Trady(...)Index [37]** |  **23.46 ms** |  **7.441 ms** | **0.408 ms** |  **1.00** |    **0.02** | **3000.0000** | **1000.0000** |  **32.35 MB** |        **1.00** |
| Competitor | 10000 | Trady(...)Index [37] | 138.80 ms | 20.147 ms | 1.104 ms |  5.92 |    0.10 | 5000.0000 | 1000.0000 |  47.13 MB |        1.46 |
