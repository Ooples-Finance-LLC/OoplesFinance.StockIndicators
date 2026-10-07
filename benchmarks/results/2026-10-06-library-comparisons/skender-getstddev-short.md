```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId            | Mean         | Error        | StdDev      | Ratio | RatioSD | Gen0       | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |------------------ |-------------:|-------------:|------------:|------:|--------:|-----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetStdDev** |  **65,015.4 μs** |  **32,092.7 μs** | **1,759.11 μs** |  **1.00** |    **0.03** |  **2000.0000** |         **-** | **16685.18 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetStdDev |     991.3 μs |   1,557.6 μs |    85.38 μs |  0.02 |    0.00 |          - |         - |   418.88 KB |        0.03 |
|            |       |                   |              |              |             |       |         |            |           |             |             |
| **Ooples**     | **10000** | **Skender.GetStdDev** | **234,521.1 μs** | **106,893.5 μs** | **5,859.19 μs** | **1.000** |    **0.03** | **20000.0000** | **1000.0000** | **169375.8 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetStdDev |   2,162.5 μs |   9,765.0 μs |   535.25 μs | 0.009 |    0.00 |          - |         - |  4145.11 KB |        0.02 |
