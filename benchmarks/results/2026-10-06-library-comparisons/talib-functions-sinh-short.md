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
| **Ooples**     | **1000**  | **TaLib.Functions.Sinh** | **2,642.2 μs** | **11,752.8 μs** | **644.2 μs** | **2,286.7 μs** |  **1.04** |    **0.29** |  **305.16 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Sinh |   345.6 μs |  7,075.6 μs | 387.8 μs |   121.9 μs |  0.14 |    0.14 |   38.35 KB |        0.13 |
|            |       |                      |            |             |          |            |       |         |            |             |
| **Ooples**     | **10000** | **TaLib.Functions.Sinh** | **3,467.0 μs** |  **3,420.9 μs** | **187.5 μs** | **3,422.2 μs** |  **1.00** |    **0.07** | **4114.53 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Sinh |   869.2 μs | 13,779.0 μs | 755.3 μs |   449.9 μs |  0.25 |    0.19 |  328.39 KB |        0.08 |
