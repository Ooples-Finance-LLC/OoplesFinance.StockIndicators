```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId           | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0      | Allocated   | Alloc Ratio |
|----------- |------ |----------------- |----------:|----------:|----------:|------:|--------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Afirma** |  **7.949 ms** |  **8.784 ms** | **0.4815 ms** |  **1.00** |    **0.07** |         **-** |  **2889.78 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Afirma |  1.782 ms |  1.546 ms | 0.0847 ms |  0.22 |    0.01 |         - |    54.09 KB |        0.02 |
|            |       |                  |           |           |           |       |         |           |             |             |
| **Ooples**     | **10000** | **QuanTAlib.Afirma** | **42.750 ms** | **86.722 ms** | **4.7535 ms** |  **1.01** |    **0.14** | **3000.0000** | **29553.37 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Afirma |  3.507 ms |  1.219 ms | 0.0668 ms |  0.08 |    0.01 |         - |   475.96 KB |        0.02 |
