```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean          | Error         | StdDev       | Ratio  | RatioSD | Gen0          | Gen1          | Gen2          | Allocated    | Alloc Ratio |
|----------- |------ |--------------------- |--------------:|--------------:|-------------:|-------:|--------:|--------------:|--------------:|--------------:|-------------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)Index [36]** |      **79.01 ms** |      **36.64 ms** |     **2.008 ms** |   **1.00** |    **0.03** |     **9000.0000** |     **1000.0000** |             **-** |     **72.54 MB** |        **1.00** |
| Competitor | 1000  | Trady(...)Index [36] |   1,280.87 ms |     183.57 ms |    10.062 ms |  16.22 |    0.38 |   191000.0000 |    65000.0000 |             - |    1530.2 MB |       21.09 |
|            |       |                      |               |               |              |        |         |               |               |               |              |             |
| **Ooples**     | **10000** | **Trady(...)Index [36]** |     **718.32 ms** |     **456.36 ms** |    **25.014 ms** |   **1.00** |    **0.04** |    **91000.0000** |     **1000.0000** |             **-** |    **732.74 MB** |        **1.00** |
| Competitor | 10000 | Trady(...)Index [36] | 159,169.73 ms | 125,206.63 ms | 6,862.998 ms | 221.77 |   10.65 | 20694000.0000 | 11761000.0000 | 11101000.0000 | 154352.54 MB |      210.65 |
