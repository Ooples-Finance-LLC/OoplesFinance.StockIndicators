```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean          | Error          | StdDev      | Ratio     | RatioSD | Gen0          | Gen1         | Gen2         | Allocated       | Alloc Ratio |
|----------- |------ |--------------------- |--------------:|---------------:|------------:|----------:|--------:|--------------:|-------------:|-------------:|----------------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)rtDay [33]** |      **2.788 ms** |      **0.3444 ms** |   **0.0189 ms** |      **1.00** |    **0.01** |             **-** |            **-** |            **-** |       **266.59 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)rtDay [33] |    641.900 ms |    322.1934 ms |  17.6605 ms |    230.29 |    5.65 |   142000.0000 |    1000.0000 |            - |   1163464.98 KB |    4,364.19 |
|            |       |                      |               |                |             |           |         |               |              |              |                 |             |
| **Ooples**     | **10000** | **Trady(...)rtDay [33]** |      **4.219 ms** |      **2.4586 ms** |   **0.1348 ms** |      **1.00** |    **0.04** |             **-** |            **-** |            **-** |      **3711.72 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)rtDay [33] | 80,944.414 ms | 18,045.8986 ms | 989.1566 ms | 19,199.40 |  576.33 | 14979000.0000 | 1765000.0000 | 1699000.0000 | 116676061.05 KB |   31,434.51 |
