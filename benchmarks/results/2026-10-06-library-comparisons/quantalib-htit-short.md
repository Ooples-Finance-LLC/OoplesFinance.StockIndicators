```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean     | Error     | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------- |---------:|----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Htit** | **3.102 ms** | **0.2472 ms** | **0.0136 ms** |  **1.00** |    **0.01** |  **358.02 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Htit | 1.185 ms | 0.4581 ms | 0.0251 ms |  0.38 |    0.01 |  189.15 KB |        0.53 |
|            |       |                |          |           |           |       |         |            |             |
| **Ooples**     | **10000** | **QuanTAlib.Htit** | **6.351 ms** | **7.0266 ms** | **0.3851 ms** |  **1.00** |    **0.08** | **4644.97 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Htit | 3.182 ms | 0.2639 ms | 0.0145 ms |  0.50 |    0.03 | 1846.21 KB |        0.40 |
