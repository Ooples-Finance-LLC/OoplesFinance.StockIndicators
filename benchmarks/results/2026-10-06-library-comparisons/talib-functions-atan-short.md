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
| **Ooples**     | **1000**  | **TaLib.Functions.Atan** | **2,681.5 μs** |  **7,858.8 μs** | **430.8 μs** | **2,453.7 μs** |  **1.02** |    **0.19** |  **305.16 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Atan |   331.0 μs |  7,039.2 μs | 385.8 μs |   108.5 μs |  0.13 |    0.13 |   37.98 KB |        0.12 |
|            |       |                      |            |             |          |            |       |         |            |             |
| **Ooples**     | **10000** | **TaLib.Functions.Atan** | **3,229.0 μs** |  **5,158.1 μs** | **282.7 μs** | **3,369.5 μs** |  **1.01** |    **0.11** | **4113.59 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Atan |   887.6 μs | 17,868.4 μs | 979.4 μs |   385.4 μs |  0.28 |    0.27 |  328.72 KB |        0.08 |
