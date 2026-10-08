```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId              | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |-------------------- |------------:|------------:|------------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Rsi** |  **8,302.6 μs** | **12,026.2 μs** |   **659.20 μs** |  **1.00** |    **0.10** |         **-** |         **-** |  **2795.06 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Rsi |    130.5 μs |    122.4 μs |     6.71 μs |  0.02 |    0.00 |         - |         - |     38.3 KB |        0.01 |
|            |       |                     |             |             |             |       |         |           |           |             |             |
| **Ooples**     | **10000** | **TaLib.Functions.Rsi** | **22,704.3 μs** | **59,030.0 μs** | **3,235.63 μs** |  **1.01** |    **0.17** | **3000.0000** | **1000.0000** | **29314.14 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Rsi |    235.8 μs |    167.1 μs |     9.16 μs |  0.01 |    0.00 |         - |         - |   328.29 KB |        0.01 |
