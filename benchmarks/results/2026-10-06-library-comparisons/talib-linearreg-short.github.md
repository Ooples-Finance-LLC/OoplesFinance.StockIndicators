```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean         | Error        | StdDev       | Median       | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|-------------:|-------------:|-------------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)arReg [25]** |  **6,144.13 μs** | **17,829.93 μs** |   **977.318 μs** |  **5,600.30 μs** | **1.016** |    **0.19** |         **-** |         **-** |  **3034.84 KB** |       **1.000** |
| Competitor | 1000  | TaLib(...)arReg [25] |     54.33 μs |     40.64 μs |     2.228 μs |     53.20 μs | 0.009 |    0.00 |         - |         - |    16.02 KB |       0.005 |
|            |       |                      |              |              |              |              |       |         |           |           |             |             |
| **Ooples**     | **10000** | **TaLib(...)arReg [25]** | **23,055.00 μs** | **83,726.00 μs** | **4,589.304 μs** | **25,125.80 μs** |  **1.03** |    **0.27** | **3000.0000** | **1000.0000** | **31521.27 KB** |       **1.000** |
| Competitor | 10000 | TaLib(...)arReg [25] |    471.17 μs |  6,520.23 μs |   357.396 μs |    274.60 μs |  0.02 |    0.01 |         - |         - |   161.52 KB |       0.005 |
