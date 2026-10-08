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
| **Ooples**     | **1000**  | **QuanTAlib.Hwma** | **18,098.5 μs** | **18,310.3 μs** | **1,003.65 μs** |  **1.00** |    **0.07** |         **-** |         **-** |  **4753.77 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Hwma |    221.8 μs |    172.9 μs |     9.48 μs |  0.01 |    0.00 |         - |         - |    52.85 KB |        0.01 |
|            |       |                |             |             |             |       |         |           |           |             |             |
| **Ooples**     | **10000** | **QuanTAlib.Hwma** | **54,525.3 μs** | **83,942.7 μs** | **4,601.18 μs** |  **1.00** |    **0.10** | **5000.0000** | **1000.0000** | **49476.23 KB** |       **1.000** |
| Competitor | 10000 | QuanTAlib.Hwma |    702.4 μs |  3,968.4 μs |   217.52 μs |  0.01 |    0.00 |         - |         - |   473.48 KB |       0.010 |
