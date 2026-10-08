```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId          | Mean       | Error       | StdDev      | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |---------------- |-----------:|------------:|------------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetSmma** | **2,369.1 μs** |  **2,475.2 μs** |   **135.67 μs** |  **1.00** |    **0.07** |   **264.5 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetSmma |   475.2 μs |    289.3 μs |    15.86 μs |  0.20 |    0.01 |   95.97 KB |        0.36 |
|            |       |                 |            |             |             |       |         |            |             |
| **Ooples**     | **10000** | **Skender.GetSmma** | **5,171.7 μs** | **39,868.6 μs** | **2,185.33 μs** |  **1.11** |    **0.54** | **3710.94 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetSmma |   588.7 μs |  2,173.8 μs |   119.15 μs |  0.13 |    0.04 |  904.89 KB |        0.24 |
