```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean         | Error        | StdDev     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|-------------:|-----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Ceil** |  **2,647.90 μs** | **10,410.27 μs** | **570.622 μs** |  **1.03** |    **0.26** |  **305.16 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Ceil |     99.10 μs |     91.22 μs |   5.000 μs |  0.04 |    0.01 |   38.68 KB |        0.13 |
|            |       |                      |              |              |            |       |         |            |             |
| **Ooples**     | **10000** | **TaLib.Functions.Ceil** | **10,100.17 μs** |  **6,061.10 μs** | **332.229 μs** |  **1.00** |    **0.04** | **4112.88 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Ceil |    203.53 μs |    329.65 μs |  18.069 μs |  0.02 |    0.00 |  328.06 KB |        0.08 |
