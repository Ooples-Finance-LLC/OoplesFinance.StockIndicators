```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId              | Mean       | Error       | StdDev   | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |-------------------- |-----------:|------------:|---------:|-----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Sin** | **2,506.5 μs** |  **9,866.3 μs** | **540.8 μs** | **2,205.5 μs** |  **1.03** |    **0.26** |  **305.16 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Sin |   280.2 μs |  5,228.3 μs | 286.6 μs |   117.5 μs |  0.11 |    0.10 |   38.96 KB |        0.13 |
|            |       |                     |            |             |          |            |       |         |            |             |
| **Ooples**     | **10000** | **TaLib.Functions.Sin** | **3,046.9 μs** | **13,516.1 μs** | **740.9 μs** | **3,208.9 μs** |  **1.05** |    **0.33** | **4113.88 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Sin |   902.9 μs | 15,518.9 μs | 850.6 μs |   424.1 μs |  0.31 |    0.27 |     329 KB |        0.08 |
