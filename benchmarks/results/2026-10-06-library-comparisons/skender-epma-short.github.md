```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId          | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |---------------- |------------:|------------:|------------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetEpma** |  **6,061.2 μs** | **15,356.0 μs** |   **841.71 μs** |  **1.01** |    **0.17** |         **-** |         **-** |  **3034.84 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetEpma |    613.4 μs |    518.1 μs |    28.40 μs |  0.10 |    0.01 |         - |         - |   221.38 KB |        0.07 |
|            |       |                 |             |             |             |       |         |           |           |             |             |
| **Ooples**     | **10000** | **Skender.GetEpma** | **28,566.3 μs** | **93,093.5 μs** | **5,102.77 μs** |  **1.02** |    **0.24** | **3000.0000** | **1000.0000** | **31521.97 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetEpma |  1,329.9 μs |  1,038.2 μs |    56.91 μs |  0.05 |    0.01 |         - |         - |  2153.99 KB |        0.07 |
