```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean       | Error      | StdDev     | Ratio | RatioSD | Gen0       | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------- |-----------:|-----------:|-----------:|------:|--------:|-----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Mama** | **154.827 ms** | **633.134 ms** | **34.7042 ms** |  **1.04** |    **0.30** |  **5000.0000** |         **-** | **45921.23 KB** |       **1.000** |
| Competitor | 1000  | QuanTAlib.Mama |   1.986 ms |   3.915 ms |  0.2146 ms |  0.01 |    0.00 |          - |         - |    96.38 KB |       0.002 |
|            |       |                |            |            |            |       |         |            |           |             |             |
| **Ooples**     | **10000** | **QuanTAlib.Mama** | **710.921 ms** |  **39.968 ms** |  **2.1908 ms** | **1.000** |    **0.00** | **56000.0000** | **1000.0000** | **463019.7 KB** |       **1.000** |
| Competitor | 10000 | QuanTAlib.Mama |   3.979 ms |   2.979 ms |  0.1633 ms | 0.006 |    0.00 |          - |         - |   887.44 KB |       0.002 |
