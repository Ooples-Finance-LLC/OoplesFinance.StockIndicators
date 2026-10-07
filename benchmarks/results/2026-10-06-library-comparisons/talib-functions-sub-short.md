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
| **Ooples**     | **1000**  | **TaLib.Functions.Sub** |  **2,635.07 μs** | **10,761.30 μs** | **589.863 μs** |  **1.03** |    **0.27** |  **305.42 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Sub |     99.90 μs |     83.74 μs |   4.590 μs |  0.04 |    0.01 |   38.02 KB |        0.12 |
|            |       |                     |              |              |            |       |         |            |             |
| **Ooples**     | **10000** | **TaLib.Functions.Sub** | **10,662.23 μs** |  **6,407.10 μs** | **351.195 μs** |  **1.00** |    **0.04** | **4114.46 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Sub |    202.07 μs |    349.29 μs |  19.146 μs |  0.02 |    0.00 |  328.39 KB |        0.08 |
