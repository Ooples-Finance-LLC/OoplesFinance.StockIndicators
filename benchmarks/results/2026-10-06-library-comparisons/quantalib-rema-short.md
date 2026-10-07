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
| **Ooples**     | **1000**  | **QuanTAlib.Rema** | **5,311.3 μs** |  **4,685.5 μs** |   **256.83 μs** |  **1.00** |    **0.06** |  **558.48 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Rema |   220.5 μs |    200.1 μs |    10.97 μs |  0.04 |    0.00 |   52.83 KB |        0.09 |
|            |       |                |            |             |             |       |         |            |             |
| **Ooples**     | **10000** | **QuanTAlib.Rema** | **9,634.5 μs** | **50,213.9 μs** | **2,752.40 μs** |  **1.05** |    **0.35** | **6662.65 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Rema |   850.1 μs |  1,744.1 μs |    95.60 μs |  0.09 |    0.02 |  475.05 KB |        0.07 |
