```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean           | Error        | StdDev      | Ratio     | RatioSD  | Gen0          | Gen1         | Gen2         | Allocated       | Alloc Ratio |
|----------- |------ |--------------------- |---------------:|-------------:|------------:|----------:|---------:|--------------:|-------------:|-------------:|----------------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)hadow [33]** |       **3.358 ms** |     **1.146 ms** |   **0.0628 ms** |      **1.00** |     **0.02** |             **-** |            **-** |            **-** |       **266.59 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)hadow [33] |   1,317.166 ms |   464.066 ms |  25.4370 ms |    392.33 |     9.11 |   282000.0000 |   61000.0000 |            - |   2307644.96 KB |    8,656.04 |
|            |       |                      |                |              |             |           |          |               |              |              |                 |             |
| **Ooples**     | **10000** | **Trady(...)hadow [33]** |       **4.505 ms** |    **13.787 ms** |   **0.7557 ms** |      **1.02** |     **0.22** |             **-** |            **-** |            **-** |      **3709.75 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)hadow [33] | 164,398.388 ms | 6,051.775 ms | 331.7182 ms | 37,221.32 | 5,684.87 | 29999000.0000 | 3501000.0000 | 3499000.0000 | 232866601.39 KB |   62,771.51 |
