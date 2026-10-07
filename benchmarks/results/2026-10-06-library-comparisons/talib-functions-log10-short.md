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
| **Ooples**     | **1000**  | **TaLib(...)Log10 [21]** | **2,573.4 μs** |  **9,165.2 μs** | **502.4 μs** | **2,305.0 μs** |  **1.02** |    **0.23** |  **305.16 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)Log10 [21] |   328.4 μs |  7,135.6 μs | 391.1 μs |   104.0 μs |  0.13 |    0.14 |   36.71 KB |        0.12 |
|            |       |                      |            |             |          |            |       |         |            |             |
| **Ooples**     | **10000** | **TaLib(...)Log10 [21]** | **3,440.9 μs** |  **3,591.5 μs** | **196.9 μs** | **3,538.2 μs** |  **1.00** |    **0.07** | **4111.91 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)Log10 [21] |   859.9 μs | 15,740.6 μs | 862.8 μs |   384.3 μs |  0.25 |    0.22 |  326.75 KB |        0.08 |
