```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |--------------- |-------------:|-------------:|-------------:|------:|--------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Dsma** |  **34,782.7 μs** |   **4,207.9 μs** |    **230.65 μs** |  **1.00** |    **0.01** |  **1000.0000** |         **-** |  **12687.45 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Dsma |     513.2 μs |     901.3 μs |     49.41 μs |  0.01 |    0.00 |          - |         - |    220.38 KB |        0.02 |
|            |       |                |              |              |              |       |         |            |           |              |             |
| **Ooples**     | **10000** | **QuanTAlib.Dsma** | **165,971.2 μs** | **527,848.9 μs** | **28,933.18 μs** | **1.019** |    **0.21** | **15000.0000** | **1000.0000** | **128105.16 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Dsma |   1,487.8 μs |     387.6 μs |     21.24 μs | 0.009 |    0.00 |          - |         - |   2179.24 KB |        0.02 |
