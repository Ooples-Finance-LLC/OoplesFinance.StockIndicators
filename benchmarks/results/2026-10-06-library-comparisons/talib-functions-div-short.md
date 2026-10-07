```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId              | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|----------- |------ |-------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Div** |  **2,473.0 μs** | **11,194.3 μs** | **613.60 μs** |  **1.04** |    **0.30** | **305.42 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Div |    101.6 μs |    173.0 μs |   9.48 μs |  0.04 |    0.01 |  37.98 KB |        0.12 |
|            |       |                     |             |             |           |       |         |           |             |
| **Ooples**     | **10000** | **TaLib.Functions.Div** | **10,470.9 μs** |  **6,377.2 μs** | **349.55 μs** |  **1.00** |    **0.04** | **4113.2 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Div |    212.3 μs |    343.7 μs |  18.84 μs |  0.02 |    0.00 | 328.72 KB |        0.08 |
