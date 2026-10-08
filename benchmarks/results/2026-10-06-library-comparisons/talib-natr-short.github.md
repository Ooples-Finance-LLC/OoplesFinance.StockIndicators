```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|-------------:|-----------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Natr** |  **5,833.90 μs** |  **6,477.46 μs** | **355.052 μs** | **1.002** |    **0.07** |         **-** |         **-** |  **2124.61 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Natr |     57.20 μs |     94.37 μs |   5.173 μs | 0.010 |    0.00 |         - |         - |    28.85 KB |        0.01 |
|            |       |                      |              |              |            |       |         |           |           |             |             |
| **Ooples**     | **10000** | **TaLib.Functions.Natr** | **16,293.63 μs** | **16,582.57 μs** | **908.947 μs** |  **1.00** |    **0.07** | **2000.0000** | **1000.0000** | **22374.52 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Natr |    189.30 μs |    745.43 μs |  40.859 μs |  0.01 |    0.00 |         - |         - |   234.87 KB |        0.01 |
