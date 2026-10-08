```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId          | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |---------------- |------------:|-------------:|------------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetChop** | **10,057.1 μs** |  **13,090.7 μs** |   **717.55 μs** |  **1.00** |    **0.09** |         **-** |         **-** |   **4119.2 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetChop |    564.3 μs |   1,309.2 μs |    71.76 μs |  0.06 |    0.01 |         - |         - |   191.47 KB |        0.05 |
|            |       |                 |             |              |             |       |         |           |           |             |             |
| **Ooples**     | **10000** | **Skender.GetChop** | **34,677.4 μs** | **108,694.1 μs** | **5,957.89 μs** |  **1.02** |    **0.22** | **4000.0000** | **1000.0000** | **43019.21 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetChop |  1,465.6 μs |   3,596.3 μs |   197.13 μs |  0.04 |    0.01 |         - |         - |  1852.27 KB |        0.04 |
