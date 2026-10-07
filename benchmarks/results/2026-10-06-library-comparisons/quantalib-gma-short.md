```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId        | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |-------------- |------------:|------------:|------------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Gma** | **17,319.0 μs** | **10,802.6 μs** |   **592.13 μs** |  **1.00** |    **0.04** |         **-** |         **-** |   **3082.5 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Gma |    471.9 μs |    573.7 μs |    31.45 μs |  0.03 |    0.00 |         - |         - |    260.3 KB |        0.08 |
|            |       |               |             |             |             |       |         |           |           |             |             |
| **Ooples**     | **10000** | **QuanTAlib.Gma** | **39,859.0 μs** | **18,795.8 μs** | **1,030.26 μs** |  **1.00** |    **0.03** | **3000.0000** | **1000.0000** | **33859.89 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Gma |  1,351.6 μs |  4,174.9 μs |   228.84 μs |  0.03 |    0.01 |         - |         - |  2570.02 KB |        0.08 |
