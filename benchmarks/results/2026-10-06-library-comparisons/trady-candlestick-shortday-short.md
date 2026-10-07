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
| **Ooples**     | **1000**  | **Trady(...)rtDay [26]** |       **3.173 ms** |     **2.234 ms** |   **0.1225 ms** |      **1.00** |    **0.05** |             **-** |            **-** |            **-** |       **266.59 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)rtDay [26] |   1,360.629 ms |   932.232 ms |  51.0988 ms |    429.19 |   19.80 |   282000.0000 |   67000.0000 |            - |   2307583.84 KB |    8,655.81 |
|            |       |                      |                |              |             |           |         |               |              |              |                 |             |
| **Ooples**     | **10000** | **Trady(...)rtDay [26]** |       **5.458 ms** |     **3.662 ms** |   **0.2007 ms** |      **1.00** |    **0.04** |             **-** |            **-** |            **-** |      **3711.44 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)rtDay [26] | 164,535.003 ms | 4,812.835 ms | 263.8077 ms | 30,173.26 |  943.38 | 29999000.0000 | 3501000.0000 | 3499000.0000 | 232865571.59 KB |   62,742.69 |
