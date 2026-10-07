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
| **Ooples**     | **1000**  | **TaLib.Functions.Cosh** | **2,774.4 μs** | **10,317.4 μs** | **565.5 μs** | **2,538.2 μs** |  **1.03** |    **0.25** |  **305.16 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Cosh |   303.2 μs |  6,026.1 μs | 330.3 μs |   115.1 μs |  0.11 |    0.11 |   35.12 KB |        0.12 |
|            |       |                      |            |             |          |            |       |         |            |             |
| **Ooples**     | **10000** | **TaLib.Functions.Cosh** | **3,438.0 μs** |  **4,460.0 μs** | **244.5 μs** | **3,301.7 μs** |  **1.00** |    **0.09** | **4108.67 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Cosh |   804.3 μs | 11,893.6 μs | 651.9 μs |   439.5 μs |  0.23 |    0.17 |  327.36 KB |        0.08 |
