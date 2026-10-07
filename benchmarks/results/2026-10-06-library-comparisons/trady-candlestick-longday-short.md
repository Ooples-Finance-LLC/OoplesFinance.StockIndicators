```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean           | Error      | StdDev     | Ratio     | RatioSD | Gen0          | Gen1         | Gen2         | Allocated       | Alloc Ratio |
|----------- |------ |--------------------- |---------------:|-----------:|-----------:|----------:|--------:|--------------:|-------------:|-------------:|----------------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)ngDay [25]** |       **3.162 ms** |   **1.015 ms** |  **0.0556 ms** |      **1.00** |    **0.02** |             **-** |            **-** |            **-** |       **266.59 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)ngDay [25] |   1,290.592 ms |  96.910 ms |  5.3120 ms |    408.19 |    6.41 |   282000.0000 |   70000.0000 |            - |   2307590.96 KB |    8,655.83 |
|            |       |                      |                |            |            |           |         |               |              |              |                 |             |
| **Ooples**     | **10000** | **Trady(...)ngDay [25]** |       **5.536 ms** |   **3.558 ms** |  **0.1950 ms** |      **1.00** |    **0.04** |             **-** |            **-** |            **-** |      **3712.98 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)ngDay [25] | 163,924.828 ms | 925.812 ms | 50.7469 ms | 29,634.60 |  894.54 | 29999000.0000 | 3501000.0000 | 3499000.0000 | 232865574.75 KB |   62,716.55 |
