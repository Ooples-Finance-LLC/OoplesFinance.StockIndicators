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
| **Ooples**     | **1000**  | **TaLib.Functions.Acos** | **2,776.4 μs** |  **9,457.9 μs** | **518.4 μs** | **2,484.2 μs** |  **1.02** |    **0.22** |  **305.16 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Acos |   322.9 μs |  6,680.1 μs | 366.2 μs |   112.7 μs |  0.12 |    0.12 |   38.68 KB |        0.13 |
|            |       |                      |            |             |          |            |       |         |            |             |
| **Ooples**     | **10000** | **TaLib.Functions.Acos** | **3,434.3 μs** |  **3,672.5 μs** | **201.3 μs** | **3,388.4 μs** |  **1.00** |    **0.07** | **4110.97 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Acos |   772.7 μs | 12,327.6 μs | 675.7 μs |   399.9 μs |  0.23 |    0.17 |  328.34 KB |        0.08 |
