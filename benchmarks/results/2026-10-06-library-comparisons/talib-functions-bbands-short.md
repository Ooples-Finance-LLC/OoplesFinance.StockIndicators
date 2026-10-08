```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean         | Error       | StdDev      | Ratio | RatioSD | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|------------:|------------:|------:|--------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)bands [22]** |  **38,472.0 μs** | **25,748.0 μs** | **1,411.33 μs** | **1.001** |    **0.04** |  **1000.0000** |         **-** |  **13181.38 KB** |       **1.000** |
| Competitor | 1000  | TaLib(...)bands [22] |     259.4 μs |    268.6 μs |    14.72 μs | 0.007 |    0.00 |          - |         - |    103.51 KB |       0.008 |
|            |       |                      |              |             |             |       |         |            |           |              |             |
| **Ooples**     | **10000** | **TaLib(...)bands [22]** | **195,907.2 μs** | **53,418.9 μs** | **2,928.07 μs** | **1.000** |    **0.02** | **16000.0000** | **1000.0000** | **133703.88 KB** |       **1.000** |
| Competitor | 10000 | TaLib(...)bands [22] |     345.5 μs |  1,434.2 μs |    78.61 μs | 0.002 |    0.00 |          - |         - |    973.95 KB |       0.007 |
