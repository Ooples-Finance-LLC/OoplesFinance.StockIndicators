```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|-------------:|-------------:|------:|--------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)dline [27]** | **129,829.4 μs** | **959,819.9 μs** | **52,610.97 μs** | **1.155** |    **0.67** |  **5000.0000** |         **-** |  **44614.47 KB** |       **1.000** |
| Competitor | 1000  | TaLib(...)dline [27] |     675.3 μs |     113.2 μs |      6.21 μs | 0.006 |    0.00 |          - |         - |     39.27 KB |       0.001 |
|            |       |                      |              |              |              |       |         |            |           |              |             |
| **Ooples**     | **10000** | **TaLib(...)dline [27]** | **684,911.3 μs** | **150,086.9 μs** |  **8,226.77 μs** | **1.000** |    **0.01** | **56000.0000** | **1000.0000** | **463983.96 KB** |       **1.000** |
| Competitor | 10000 | TaLib(...)dline [27] |   1,378.9 μs |     177.9 μs |      9.75 μs | 0.002 |    0.00 |          - |         - |    329.68 KB |       0.001 |
