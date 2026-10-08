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
| **Ooples**     | **1000**  | **Trady(...)hadow [33]** |       **3.135 ms** |     **1.201 ms** |   **0.0658 ms** |      **1.00** |    **0.03** |             **-** |            **-** |            **-** |       **266.59 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)hadow [33] |   1,372.932 ms | 2,011.932 ms | 110.2808 ms |    438.02 |   31.48 |   282000.0000 |   61000.0000 |            - |   2307645.62 KB |    8,656.04 |
|            |       |                      |                |              |             |           |         |               |              |              |                 |             |
| **Ooples**     | **10000** | **Trady(...)hadow [33]** |       **5.442 ms** |     **2.983 ms** |   **0.1635 ms** |      **1.00** |    **0.04** |             **-** |            **-** |            **-** |      **3712.09 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)hadow [33] | 164,956.922 ms | 2,482.108 ms | 136.0527 ms | 30,328.12 |  777.44 | 29999000.0000 | 3501000.0000 | 3499000.0000 | 232866616.83 KB |   62,731.88 |
