```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId          | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0       | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |---------------- |-------------:|-------------:|-------------:|------:|--------:|-----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetMama** | **136,809.0 μs** | **955,709.4 μs** | **52,385.66 μs** | **1.120** |    **0.57** |  **5000.0000** |         **-** | **46848.32 KB** |       **1.000** |
| Competitor | 1000  | Skender.GetMama |     866.7 μs |     691.1 μs |     37.88 μs | 0.007 |    0.00 |          - |         - |   248.25 KB |       0.005 |
|            |       |                 |              |              |              |       |         |            |           |             |             |
| **Ooples**     | **10000** | **Skender.GetMama** | **751,896.8 μs** | **390,921.0 μs** | **21,427.70 μs** | **1.001** |    **0.03** | **57000.0000** | **1000.0000** | **472264.3 KB** |       **1.000** |
| Competitor | 10000 | Skender.GetMama |   2,145.1 μs |  10,157.5 μs |    556.77 μs | 0.003 |    0.00 |          - |         - |  2410.36 KB |       0.005 |
