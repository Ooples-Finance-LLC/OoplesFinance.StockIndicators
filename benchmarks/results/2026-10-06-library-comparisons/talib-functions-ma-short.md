```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId             | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0      | Allocated   | Alloc Ratio |
|----------- |------ |------------------- |-------------:|-------------:|-------------:|------:|--------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Ma** |  **4,603.43 μs** |  **9,536.86 μs** |   **522.748 μs** |  **1.01** |    **0.14** |         **-** |  **1053.89 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Ma |     90.77 μs |     68.85 μs |     3.774 μs |  0.02 |    0.00 |         - |    38.28 KB |        0.04 |
|            |       |                    |              |              |              |       |         |           |             |             |
| **Ooples**     | **10000** | **TaLib.Functions.Ma** | **10,868.67 μs** | **54,703.52 μs** | **2,998.485 μs** |  **1.05** |    **0.33** | **1000.0000** | **11611.85 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Ma |    170.90 μs |    299.46 μs |    16.415 μs |  0.02 |    0.00 |         - |   328.65 KB |        0.03 |
