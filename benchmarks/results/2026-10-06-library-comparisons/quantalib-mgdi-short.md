```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean         | Error       | StdDev      | Ratio | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------- |-------------:|------------:|------------:|------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Mgdi** |  **42,208.8 μs** |  **7,640.8 μs** |   **418.82 μs** | **1.000** |         **-** |         **-** |  **7880.54 KB** |       **1.000** |
| Competitor | 1000  | QuanTAlib.Mgdi |     300.7 μs |    252.9 μs |    13.86 μs | 0.007 |         - |         - |     69.5 KB |       0.009 |
|            |       |                |              |             |             |       |           |           |             |             |
| **Ooples**     | **10000** | **QuanTAlib.Mgdi** | **256,794.9 μs** | **28,436.9 μs** | **1,558.72 μs** | **1.000** | **9000.0000** | **1000.0000** | **79904.26 KB** |       **1.000** |
| Competitor | 10000 | QuanTAlib.Mgdi |   1,176.0 μs |  3,482.3 μs |   190.87 μs | 0.005 |         - |         - |   641.07 KB |       0.008 |
