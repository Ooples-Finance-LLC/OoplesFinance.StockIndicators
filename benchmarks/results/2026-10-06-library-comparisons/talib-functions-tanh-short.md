```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean       | Error       | StdDev   | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|------------:|---------:|-----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Tanh** | **2,720.0 μs** |  **9,840.9 μs** | **539.4 μs** | **2,511.4 μs** |  **1.02** |    **0.24** |  **305.16 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Tanh |   318.1 μs |  6,477.4 μs | 355.1 μs |   115.6 μs |  0.12 |    0.12 |   33.43 KB |        0.11 |
|            |       |                      |            |             |          |            |       |         |            |             |
| **Ooples**     | **10000** | **TaLib.Functions.Tanh** | **3,709.2 μs** |  **4,079.2 μs** | **223.6 μs** | **3,677.0 μs** |  **1.00** |    **0.07** | **4113.22 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Tanh |   992.8 μs | 14,926.4 μs | 818.2 μs |   541.5 μs |  0.27 |    0.19 |  328.34 KB |        0.08 |
