```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean         | Error        | StdDev     | Median       | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|-------------:|-----------:|-------------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)rcept [34]** |  **5,831.43 μs** | **12,770.78 μs** | **700.009 μs** |  **5,566.20 μs** | **1.009** |    **0.14** |         **-** |         **-** |  **3034.84 KB** |       **1.000** |
| Competitor | 1000  | TaLib(...)rcept [34] |     50.13 μs |     83.09 μs |   4.554 μs |     48.40 μs | 0.009 |    0.00 |         - |         - |    20.89 KB |       0.007 |
|            |       |                      |              |              |            |              |       |         |           |           |             |             |
| **Ooples**     | **10000** | **TaLib(...)rcept [34]** | **24,948.57 μs** |  **5,991.13 μs** | **328.394 μs** | **24,959.80 μs** |  **1.00** |    **0.02** | **3000.0000** | **1000.0000** | **31520.94 KB** |       **1.000** |
| Competitor | 10000 | TaLib(...)rcept [34] |    441.38 μs |  6,125.08 μs | 335.736 μs |    258.65 μs |  0.02 |    0.01 |         - |         - |   161.23 KB |       0.005 |
