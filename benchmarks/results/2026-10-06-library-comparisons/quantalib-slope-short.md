```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId          | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |---------------- |-----------:|----------:|----------:|------:|--------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Slope** |  **28.439 ms** | **17.161 ms** | **0.9406 ms** |  **1.00** |    **0.04** |  **1000.0000** |         **-** |  **12906.46 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Slope |   1.031 ms |  1.775 ms | 0.0973 ms |  0.04 |    0.00 |          - |         - |    514.53 KB |        0.04 |
|            |       |                 |            |           |           |       |         |            |           |              |             |
| **Ooples**     | **10000** | **QuanTAlib.Slope** | **122.154 ms** | **66.232 ms** | **3.6304 ms** |  **1.00** |    **0.04** | **15000.0000** | **1000.0000** | **130494.71 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Slope |   1.606 ms |  4.004 ms | 0.2195 ms |  0.01 |    0.00 |          - |         - |   5118.59 KB |        0.04 |
