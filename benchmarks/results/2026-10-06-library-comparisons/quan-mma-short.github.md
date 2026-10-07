```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId        | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |-------------- |------------:|------------:|----------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Mma** |  **6,158.1 μs** |  **9,511.8 μs** | **521.37 μs** |  **1.00** |    **0.10** |         **-** |         **-** |   **2868.2 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Mma |    899.4 μs |    843.2 μs |  46.22 μs |  0.15 |    0.01 |         - |         - |   219.98 KB |        0.08 |
|            |       |               |             |             |           |       |         |           |           |             |             |
| **Ooples**     | **10000** | **QuanTAlib.Mma** | **17,445.6 μs** | **14,589.5 μs** | **799.70 μs** |  **1.00** |    **0.06** | **3000.0000** | **1000.0000** | **29938.79 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Mma |  1,615.6 μs |  1,069.2 μs |  58.60 μs |  0.09 |    0.00 |         - |         - |   2178.8 KB |        0.07 |
