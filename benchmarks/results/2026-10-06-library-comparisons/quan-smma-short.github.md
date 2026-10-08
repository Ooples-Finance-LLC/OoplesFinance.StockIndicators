```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean       | Error       | StdDev      | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------- |-----------:|------------:|------------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Smma** | **2,363.2 μs** |  **2,109.4 μs** |   **115.63 μs** |  **1.00** |    **0.06** |   **264.5 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Smma |   224.9 μs |    204.3 μs |    11.20 μs |  0.10 |    0.01 |   53.35 KB |        0.20 |
|            |       |                |            |             |             |       |         |            |             |
| **Ooples**     | **10000** | **QuanTAlib.Smma** | **8,458.0 μs** | **76,443.1 μs** | **4,190.10 μs** |  **1.24** |    **0.87** | **3713.19 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Smma |   616.6 μs |  1,187.6 μs |    65.10 μs |  0.09 |    0.05 |  475.25 KB |        0.13 |
