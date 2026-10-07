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
| **Ooples**     | **1000**  | **Trady(...)ating [45]** | **28.18 ms** | **20.192 ms** | **1.107 ms** |  **1.00** |    **0.05** |  **1000.0000** |         **-** |         **-** |   **9.79 MB** |        **1.00** |
| Competitor | 1000  | Trady(...)ating [45] | 19.06 ms |  4.879 ms | 0.267 ms |  0.68 |    0.02 |  1000.0000 |         - |         - |   9.23 MB |        0.94 |
|            |       |                      |          |           |          |       |         |            |           |           |           |             |
| **Ooples**     | **10000** | **Trady(...)ating [45]** | **72.60 ms** | **67.043 ms** | **3.675 ms** |  **1.00** |    **0.06** | **12000.0000** | **1000.0000** |         **-** | **100.06 MB** |        **1.00** |
| Competitor | 10000 | Trady(...)ating [45] | 65.28 ms | 73.527 ms | 4.030 ms |  0.90 |    0.06 |  5000.0000 | 2000.0000 | 1000.0000 |  64.73 MB |        0.65 |
