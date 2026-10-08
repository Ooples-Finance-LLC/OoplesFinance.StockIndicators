```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean         | Error        | StdDev      | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|-------------:|------------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Kama** |  **30,412.5 μs** | **15,108.25 μs** |   **828.13 μs** | **1.001** |    **0.03** |         **-** |         **-** |  **7720.66 KB** |       **1.000** |
| Competitor | 1000  | TaLib.Functions.Kama |     166.0 μs |     60.54 μs |     3.32 μs | 0.005 |    0.00 |         - |         - |    38.56 KB |       0.005 |
|            |       |                      |              |              |             |       |         |           |           |             |             |
| **Ooples**     | **10000** | **TaLib.Functions.Kama** | **123,549.3 μs** | **31,405.86 μs** | **1,721.46 μs** | **1.000** |    **0.02** | **9000.0000** | **1000.0000** | **79536.79 KB** |       **1.000** |
| Competitor | 10000 | TaLib.Functions.Kama |     237.1 μs |    797.73 μs |    43.73 μs | 0.002 |    0.00 |         - |         - |   328.88 KB |       0.004 |
