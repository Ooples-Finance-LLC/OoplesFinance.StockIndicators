```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId              | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |-------------------- |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Cos** | **2,615.2 μs** |  **9,871.7 μs** | **541.10 μs** | **2,357.7 μs** |  **1.03** |    **0.25** |  **305.16 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Cos |   335.5 μs |  6,793.3 μs | 372.36 μs |   121.0 μs |  0.13 |    0.13 |   37.98 KB |        0.12 |
|            |       |                     |            |             |           |            |       |         |            |             |
| **Ooples**     | **10000** | **TaLib.Functions.Cos** | **3,052.3 μs** | **16,174.9 μs** | **886.60 μs** | **2,546.8 μs** |  **1.05** |    **0.35** | **4114.58 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Cos |   408.4 μs |    930.8 μs |  51.02 μs |   386.2 μs |  0.14 |    0.03 |  327.36 KB |        0.08 |
