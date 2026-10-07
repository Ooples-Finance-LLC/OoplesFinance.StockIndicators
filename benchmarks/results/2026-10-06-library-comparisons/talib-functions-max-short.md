```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId              | Mean         | Error        | StdDev     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |-------------------- |-------------:|-------------:|-----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Max** |  **2,065.77 μs** |  **1,405.54 μs** |  **77.042 μs** |  **1.00** |    **0.05** |  **369.87 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Max |     71.53 μs |     72.46 μs |   3.972 μs |  0.03 |    0.00 |   22.02 KB |        0.06 |
|            |       |                     |              |              |            |       |         |            |             |
| **Ooples**     | **10000** | **TaLib.Functions.Max** | **10,153.57 μs** | **16,598.34 μs** | **909.811 μs** |  **1.01** |    **0.11** | **4731.63 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Max |    108.57 μs |    310.50 μs |  17.019 μs |  0.01 |    0.00 |  162.37 KB |        0.03 |
