```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId              | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0       | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |-------------------- |------------:|------------:|------------:|------:|--------:|-----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Cci** | **17,734.5 μs** |  **9,920.4 μs** |   **543.77 μs** |  **1.00** |    **0.04** |  **1000.0000** |         **-** |  **8993.09 KB** |       **1.000** |
| Competitor | 1000  | TaLib.Functions.Cci |    412.0 μs |    174.8 μs |     9.58 μs |  0.02 |    0.00 |          - |         - |    38.81 KB |       0.004 |
|            |       |                     |             |             |             |       |         |            |           |             |             |
| **Ooples**     | **10000** | **TaLib.Functions.Cci** | **54,271.8 μs** | **96,944.4 μs** | **5,313.85 μs** | **1.006** |    **0.12** | **11000.0000** | **1000.0000** | **92429.09 KB** |       **1.000** |
| Competitor | 10000 | TaLib.Functions.Cci |    489.3 μs |  1,899.2 μs |   104.10 μs | 0.009 |    0.00 |          - |         - |   328.85 KB |       0.004 |
