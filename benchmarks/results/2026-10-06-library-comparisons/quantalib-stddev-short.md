```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId           | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0       | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |----------------- |-----------:|----------:|----------:|------:|--------:|-----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Stddev** |  **22.059 ms** | **27.594 ms** | **1.5125 ms** |  **1.00** |    **0.08** |  **1000.0000** |         **-** |  **8666.41 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Stddev |   2.515 ms |  1.035 ms | 0.0567 ms |  0.11 |    0.01 |          - |         - |   516.67 KB |        0.06 |
|            |       |                  |            |           |           |       |         |            |           |             |             |
| **Ooples**     | **10000** | **QuanTAlib.Stddev** | **134.670 ms** |  **5.194 ms** | **0.2847 ms** |  **1.00** |    **0.00** | **10000.0000** | **1000.0000** | **87955.05 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Stddev |   6.856 ms | 20.161 ms | 1.1051 ms |  0.05 |    0.01 |          - |         - |  4209.09 KB |        0.05 |
