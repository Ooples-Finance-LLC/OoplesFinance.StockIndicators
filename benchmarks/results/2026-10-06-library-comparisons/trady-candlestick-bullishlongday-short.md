```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean          | Error        | StdDev      | Ratio     | RatioSD  | Gen0          | Gen1         | Gen2         | Allocated       | Alloc Ratio |
|----------- |------ |--------------------- |--------------:|-------------:|------------:|----------:|---------:|--------------:|-------------:|-------------:|----------------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)ngDay [32]** |      **2.754 ms** |     **1.631 ms** |   **0.0894 ms** |      **1.00** |     **0.04** |             **-** |            **-** |            **-** |       **266.59 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)ngDay [32] |    616.723 ms |   163.209 ms |   8.9460 ms |    224.08 |     6.94 |   142000.0000 |    1000.0000 |            - |   1163370.03 KB |    4,363.83 |
|            |       |                      |               |              |             |           |          |               |              |              |                 |             |
| **Ooples**     | **10000** | **Trady(...)ngDay [32]** |      **3.973 ms** |    **15.309 ms** |   **0.8391 ms** |      **1.03** |     **0.29** |             **-** |            **-** |            **-** |      **3710.41 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)ngDay [32] | 81,820.794 ms | 4,778.195 ms | 261.9090 ms | 21,305.43 | 4,372.42 | 14978000.0000 | 3011000.0000 | 1699000.0000 | 116673821.66 KB |   31,445.03 |
