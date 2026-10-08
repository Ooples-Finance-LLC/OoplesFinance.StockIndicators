```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean           | Error        | StdDev      | Ratio     | RatioSD | Gen0          | Gen1         | Gen2         | Allocated       | Alloc Ratio |
|----------- |------ |--------------------- |---------------:|-------------:|------------:|----------:|--------:|--------------:|-------------:|-------------:|----------------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)thods [36]** |       **3.912 ms** |     **1.929 ms** |   **0.1058 ms** |      **1.00** |    **0.03** |             **-** |            **-** |            **-** |       **269.38 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)thods [36] |     816.319 ms |   291.021 ms |  15.9518 ms |    208.79 |    5.97 |   183000.0000 |    1000.0000 |            - |   1498189.18 KB |    5,561.72 |
|            |       |                      |                |              |             |           |         |               |              |              |                 |             |
| **Ooples**     | **10000** | **Trady(...)thods [36]** |       **7.317 ms** |     **2.775 ms** |   **0.1521 ms** |      **1.00** |    **0.03** |             **-** |            **-** |            **-** |      **3713.84 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)thods [36] | 104,328.604 ms | 2,196.537 ms | 120.3996 ms | 14,263.33 |  256.91 | 19653000.0000 | 2103000.0000 | 2101000.0000 | 154215687.13 KB |   41,524.55 |
