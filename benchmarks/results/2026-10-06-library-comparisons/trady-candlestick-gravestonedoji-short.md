```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean     | Error     | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |---------:|----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)eDoji [32]** | **3.080 ms** |  **2.035 ms** | **0.1115 ms** |  **1.00** |    **0.04** |  **272.96 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)eDoji [32] | 1.910 ms |  1.206 ms | 0.0661 ms |  0.62 |    0.03 |  848.63 KB |        3.11 |
|            |       |                      |          |           |           |       |         |            |             |
| **Ooples**     | **10000** | **Trady(...)eDoji [32]** | **5.085 ms** |  **3.517 ms** | **0.1928 ms** |  **1.00** |    **0.05** | **3792.62 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)eDoji [32] | 6.799 ms | 21.959 ms | 1.2037 ms |  1.34 |    0.21 | 6402.83 KB |        1.69 |
