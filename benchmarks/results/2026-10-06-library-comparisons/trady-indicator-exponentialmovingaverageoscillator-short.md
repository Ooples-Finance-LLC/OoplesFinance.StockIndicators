```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Gen2      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |----------:|----------:|----------:|------:|--------:|----------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)lator [50]** |  **2.839 ms** | **10.512 ms** | **0.5762 ms** |  **1.03** |    **0.24** |         **-** |         **-** |         **-** |   **305.41 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)lator [50] |  7.161 ms |  1.017 ms | 0.0558 ms |  2.59 |    0.41 |         - |         - |         - |  3071.41 KB |       10.06 |
|            |       |                      |           |           |           |       |         |           |           |           |             |             |
| **Ooples**     | **10000** | **Trady(...)lator [50]** | **19.454 ms** | **33.069 ms** | **1.8126 ms** |  **1.01** |    **0.11** |         **-** |         **-** |         **-** |  **4113.46 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)lator [50] | 40.218 ms |  6.885 ms | 0.3774 ms |  2.08 |    0.16 | 2000.0000 | 1000.0000 | 1000.0000 | 24966.48 KB |        6.07 |
