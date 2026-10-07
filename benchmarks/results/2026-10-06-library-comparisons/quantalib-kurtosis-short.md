```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId             | Mean         | Error        | StdDev      | Ratio | RatioSD | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |------------------- |-------------:|-------------:|------------:|------:|--------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Kurtosis** |  **49,518.5 μs** |  **10,392.0 μs** |   **569.62 μs** |  **1.00** |    **0.01** |  **2000.0000** |         **-** |  **19945.98 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Kurtosis |     798.8 μs |   1,509.2 μs |    82.73 μs |  0.02 |    0.00 |          - |         - |    399.11 KB |        0.02 |
|            |       |                    |              |              |             |       |         |            |           |              |             |
| **Ooples**     | **10000** | **QuanTAlib.Kurtosis** | **420,089.2 μs** | **167,330.8 μs** | **9,171.97 μs** | **1.000** |    **0.03** | **24000.0000** | **1000.0000** | **201650.88 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Kurtosis |   1,599.1 μs |   3,205.2 μs |   175.69 μs | 0.004 |    0.00 |          - |         - |   3973.82 KB |        0.02 |
