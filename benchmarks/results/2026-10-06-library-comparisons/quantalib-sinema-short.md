```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId           | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |----------------- |------------:|------------:|------------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Sinema** | **16,931.0 μs** |  **9,696.3 μs** |   **531.49 μs** |  **1.00** |    **0.04** |         **-** |         **-** |   **3052.2 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Sinema |    497.5 μs |    645.2 μs |    35.36 μs |  0.03 |    0.00 |         - |         - |   258.95 KB |        0.08 |
|            |       |                  |             |             |             |       |         |           |           |             |             |
| **Ooples**     | **10000** | **QuanTAlib.Sinema** | **46,687.7 μs** | **40,046.1 μs** | **2,195.06 μs** |  **1.00** |    **0.06** | **3000.0000** | **1000.0000** | **33258.98 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Sinema |  1,280.9 μs |  3,938.2 μs |   215.87 μs |  0.03 |    0.00 |         - |         - |  2570.02 KB |        0.08 |
