```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean     | Error     | StdDev   | Ratio | RatioSD | Gen0       | Gen1      | Gen2      | Allocated | Alloc Ratio |
|----------- |------ |--------------------- |---------:|----------:|---------:|------:|--------:|-----------:|----------:|----------:|----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)lator [40]** | **11.05 ms** |  **12.33 ms** | **0.676 ms** |  **1.00** |    **0.08** |          **-** |         **-** |         **-** |   **3.07 MB** |        **1.00** |
| Competitor | 1000  | Trady(...)lator [40] | 18.27 ms |  44.40 ms | 2.434 ms |  1.66 |    0.21 |  1000.0000 |         - |         - |  10.55 MB |        3.44 |
|            |       |                      |          |           |          |       |         |            |           |           |           |             |
| **Ooples**     | **10000** | **Trady(...)lator [40]** | **32.92 ms** |  **53.09 ms** | **2.910 ms** |  **1.01** |    **0.11** |  **3000.0000** | **1000.0000** |         **-** |  **32.11 MB** |        **1.00** |
| Competitor | 10000 | Trady(...)lator [40] | 61.40 ms | 165.79 ms | 9.087 ms |  1.87 |    0.28 | 10000.0000 | 3000.0000 | 1000.0000 |  92.85 MB |        2.89 |
