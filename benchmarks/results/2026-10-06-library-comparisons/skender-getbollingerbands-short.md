```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|----------:|----------:|------:|--------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **Skend(...)Bands [25]** |  **71.052 ms** | **52.999 ms** | **2.9050 ms** |  **1.00** |    **0.05** |  **2000.0000** |         **-** |  **22439.72 KB** |        **1.00** |
| Competitor | 1000  | Skend(...)Bands [25] |   1.168 ms |  1.549 ms | 0.0849 ms |  0.02 |    0.00 |          - |         - |    499.68 KB |        0.02 |
|            |       |                      |            |           |           |       |         |            |           |              |             |
| **Ooples**     | **10000** | **Skend(...)Bands [25]** | **312.451 ms** | **94.258 ms** | **5.1666 ms** | **1.000** |    **0.02** | **27000.0000** | **1000.0000** | **227915.84 KB** |        **1.00** |
| Competitor | 10000 | Skend(...)Bands [25] |   1.989 ms |  2.777 ms | 0.1522 ms | 0.006 |    0.00 |          - |         - |   4947.23 KB |        0.02 |
