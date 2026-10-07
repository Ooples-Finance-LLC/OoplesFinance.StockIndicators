```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------- |------------:|------------:|------------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetTsi** | **20,405.5 μs** | **21,512.6 μs** | **1,179.18 μs** |  **1.00** |    **0.07** |         **-** |         **-** |   **7160.4 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetTsi |    734.3 μs |    351.7 μs |    19.28 μs |  0.04 |    0.00 |         - |         - |   208.48 KB |        0.03 |
|            |       |                |             |             |             |       |         |           |           |             |             |
| **Ooples**     | **10000** | **Skender.GetTsi** | **56,276.2 μs** | **63,380.2 μs** | **3,474.08 μs** |  **1.00** |    **0.08** | **8000.0000** | **1000.0000** | **74241.88 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetTsi |    888.3 μs |  1,456.0 μs |    79.81 μs |  0.02 |    0.00 |         - |         - |   2019.3 KB |        0.03 |
