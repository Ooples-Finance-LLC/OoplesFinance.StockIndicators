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
| **Ooples**     | **1000**  | **Trady(...)ngDay [32]** |      **2.834 ms** |     **0.5135 ms** |   **0.0281 ms** |      **1.00** |    **0.01** |             **-** |            **-** |            **-** |       **266.59 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)ngDay [32] |    615.245 ms |   256.0490 ms |  14.0349 ms |    217.12 |    4.68 |   140000.0000 |    1000.0000 |            - |   1144914.45 KB |    4,294.60 |
|            |       |                      |               |               |             |           |         |               |              |              |                 |             |
| **Ooples**     | **10000** | **Trady(...)ngDay [32]** |      **4.286 ms** |     **3.7461 ms** |   **0.2053 ms** |      **1.00** |    **0.06** |             **-** |            **-** |            **-** |      **3710.41 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)ngDay [32] | 81,169.348 ms | 4,021.5169 ms | 220.4329 ms | 18,966.91 |  774.49 | 14918000.0000 | 3013000.0000 | 1692000.0000 | 116208142.55 KB |   31,319.52 |
