```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId        | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0       | Gen1      | Allocated | Alloc Ratio |
|----------- |------ |-------------- |-----------:|----------:|----------:|------:|--------:|-----------:|----------:|----------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Rvi** |  **24.547 ms** | **13.545 ms** | **0.7425 ms** |  **1.00** |    **0.04** |  **1000.0000** |         **-** |   **12.6 MB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Rvi |   6.126 ms | 18.569 ms | 1.0178 ms |  0.25 |    0.04 |          - |         - |   1.43 MB |        0.11 |
|            |       |               |            |           |           |       |         |            |           |           |             |
| **Ooples**     | **10000** | **QuanTAlib.Rvi** | **140.059 ms** | **57.202 ms** | **3.1354 ms** |  **1.00** |    **0.03** | **15000.0000** | **1000.0000** | **126.36 MB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Rvi |  16.625 ms | 57.600 ms | 3.1572 ms |  0.12 |    0.02 |  1000.0000 |         - |  12.61 MB |        0.10 |
