```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean           | Error         | StdDev      | Ratio     | RatioSD | Gen0          | Gen1         | Gen2         | Allocated       | Alloc Ratio |
|----------- |------ |--------------------- |---------------:|--------------:|------------:|----------:|--------:|--------------:|-------------:|-------------:|----------------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)thods [37]** |       **3.936 ms** |      **2.135 ms** |   **0.1170 ms** |      **1.00** |    **0.04** |             **-** |            **-** |            **-** |       **269.36 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)thods [37] |     856.659 ms |    627.974 ms |  34.4214 ms |    217.79 |    9.40 |   186000.0000 |    1000.0000 |            - |   1521260.11 KB |    5,647.70 |
|            |       |                      |                |               |             |           |         |               |              |              |                 |             |
| **Ooples**     | **10000** | **Trady(...)thods [37]** |       **7.405 ms** |      **2.904 ms** |   **0.1592 ms** |      **1.00** |    **0.03** |             **-** |            **-** |            **-** |      **3719.69 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)thods [37] | 102,898.916 ms | 16,213.454 ms | 888.7141 ms | 13,899.69 |  277.57 | 19463000.0000 | 2094000.0000 | 2092000.0000 | 152632416.36 KB |   41,033.67 |
