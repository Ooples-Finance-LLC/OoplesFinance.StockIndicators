```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------- |------------:|-------------:|------------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Dema** |  **9,680.7 μs** | **14,155.85 μs** |   **775.93 μs** |  **1.00** |    **0.10** |         **-** |         **-** |   **2061.7 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Dema |    198.9 μs |     56.17 μs |     3.08 μs |  0.02 |    0.00 |         - |         - |    52.52 KB |        0.03 |
|            |       |                |             |              |             |       |         |           |           |             |             |
| **Ooples**     | **10000** | **QuanTAlib.Dema** | **26,897.4 μs** | **61,807.65 μs** | **3,387.89 μs** |  **1.01** |    **0.16** | **2000.0000** | **1000.0000** | **19922.73 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Dema |    690.6 μs |  3,289.91 μs |   180.33 μs |  0.03 |    0.01 |         - |         - |   475.05 KB |        0.02 |
