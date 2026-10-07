```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------- |-----------:|-----------:|----------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Kama** |  **30.567 ms** | **16.7136 ms** | **0.9161 ms** |  **1.00** |    **0.04** |         **-** |         **-** |  **7755.97 KB** |       **1.000** |
| Competitor | 1000  | QuanTAlib.Kama |   1.053 ms |  0.5263 ms | 0.0288 ms |  0.03 |    0.00 |         - |         - |    70.53 KB |       0.009 |
|            |       |                |            |            |           |       |         |           |           |             |             |
| **Ooples**     | **10000** | **QuanTAlib.Kama** | **121.064 ms** |  **8.3059 ms** | **0.4553 ms** |  **1.00** |    **0.00** | **9000.0000** | **1000.0000** | **79851.09 KB** |       **1.000** |
| Competitor | 10000 | QuanTAlib.Kama |   2.316 ms |  1.4602 ms | 0.0800 ms |  0.02 |    0.00 |         - |         - |    641.8 KB |       0.008 |
