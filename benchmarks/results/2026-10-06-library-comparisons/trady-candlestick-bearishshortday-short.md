```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean          | Error         | StdDev      | Ratio     | RatioSD | Gen0          | Gen1         | Gen2         | Allocated       | Alloc Ratio |
|----------- |------ |--------------------- |--------------:|--------------:|------------:|----------:|--------:|--------------:|-------------:|-------------:|----------------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)rtDay [33]** |      **2.805 ms** |      **1.140 ms** |   **0.0625 ms** |      **1.00** |    **0.03** |             **-** |            **-** |            **-** |       **266.59 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)rtDay [33] |    636.720 ms |    534.163 ms |  29.2793 ms |    227.10 |   10.03 |   140000.0000 |    1000.0000 |            - |   1144914.66 KB |    4,294.60 |
|            |       |                      |               |               |             |           |         |               |              |              |                 |             |
| **Ooples**     | **10000** | **Trady(...)rtDay [33]** |      **4.316 ms** |      **4.079 ms** |   **0.2236 ms** |      **1.00** |    **0.06** |             **-** |            **-** |            **-** |      **3709.75 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)rtDay [33] | 80,054.841 ms | 10,819.491 ms | 593.0528 ms | 18,579.28 |  823.02 | 14917000.0000 | 3048000.0000 | 1691000.0000 | 116208130.59 KB |   31,325.06 |
