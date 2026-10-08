```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean       | Error     | StdDev    | Median     | Ratio | RatioSD | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|----------:|----------:|-----------:|------:|--------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **Skend(...)ation [22]** |  **73.567 ms** | **31.378 ms** | **1.7199 ms** |  **73.786 ms** |  **1.00** |    **0.03** |  **2000.0000** |         **-** |  **22749.84 KB** |        **1.00** |
| Competitor | 1000  | Skend(...)ation [22] |   1.545 ms |  3.791 ms | 0.2078 ms |   1.436 ms |  0.02 |    0.00 |          - |         - |     687.7 KB |        0.03 |
|            |       |                      |            |           |           |            |       |         |            |           |              |             |
| **Ooples**     | **10000** | **Skend(...)ation [22]** | **350.477 ms** | **60.285 ms** | **3.3044 ms** | **349.667 ms** |  **1.00** |    **0.01** | **27000.0000** | **1000.0000** | **231440.66 KB** |        **1.00** |
| Competitor | 10000 | Skend(...)ation [22] |   4.495 ms | 58.380 ms | 3.2000 ms |   2.654 ms |  0.01 |    0.01 |          - |         - |   6848.22 KB |        0.03 |
