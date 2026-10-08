```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId          | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0       | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |---------------- |------------:|-------------:|------------:|------:|--------:|-----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Frama** | **19,870.4 μs** | **10,742.97 μs** |   **588.86 μs** |  **1.00** |    **0.04** |  **1000.0000** |         **-** |  **8264.94 KB** |       **1.000** |
| Competitor | 1000  | QuanTAlib.Frama |    873.6 μs |     88.35 μs |     4.84 μs |  0.04 |    0.00 |          - |         - |    53.31 KB |       0.006 |
|            |       |                 |             |              |             |       |         |            |           |             |             |
| **Ooples**     | **10000** | **QuanTAlib.Frama** | **95,787.6 μs** | **87,940.31 μs** | **4,820.30 μs** |  **1.00** |    **0.06** | **10000.0000** | **1000.0000** | **85009.53 KB** |       **1.000** |
| Competitor | 10000 | QuanTAlib.Frama |  2,732.1 μs |  2,189.91 μs |   120.04 μs |  0.03 |    0.00 |          - |         - |   474.25 KB |       0.006 |
