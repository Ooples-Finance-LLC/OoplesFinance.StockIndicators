```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId           | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0       | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |----------------- |-----------:|-----------:|----------:|------:|--------:|-----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Zscore** |  **20.159 ms** |   **7.908 ms** | **0.4335 ms** |  **1.00** |    **0.03** |  **1000.0000** |         **-** |  **9399.42 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Zscore |   2.498 ms |   1.542 ms | 0.0845 ms |  0.12 |    0.00 |          - |         - |   516.28 KB |        0.05 |
|            |       |                  |            |            |           |       |         |            |           |             |             |
| **Ooples**     | **10000** | **QuanTAlib.Zscore** | **120.774 ms** | **118.744 ms** | **6.5088 ms** |  **1.00** |    **0.07** | **11000.0000** | **1000.0000** | **95218.65 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Zscore |   7.470 ms |   1.035 ms | 0.0568 ms |  0.06 |    0.00 |          - |         - |  4203.16 KB |        0.04 |
