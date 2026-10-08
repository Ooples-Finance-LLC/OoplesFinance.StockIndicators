```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId          | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0       | Gen1      | Allocated | Alloc Ratio |
|----------- |------ |---------------- |-----------:|-----------:|----------:|------:|--------:|-----------:|----------:|----------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Vidya** |  **33.978 ms** |  **14.392 ms** | **0.7889 ms** |  **1.00** |    **0.03** |  **1000.0000** |         **-** |   **14.5 MB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Vidya |   8.815 ms |   1.680 ms | 0.0921 ms |  0.26 |    0.01 |          - |         - |   1.02 MB |        0.07 |
|            |       |                 |            |            |           |       |         |            |           |           |             |
| **Ooples**     | **10000** | **QuanTAlib.Vidya** | **184.359 ms** | **107.093 ms** | **5.8701 ms** |  **1.00** |    **0.04** | **19000.0000** | **1000.0000** |  **154.9 MB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Vidya |  31.807 ms |  21.493 ms | 1.1781 ms |  0.17 |    0.01 |  1000.0000 |         - |   9.55 MB |        0.06 |
