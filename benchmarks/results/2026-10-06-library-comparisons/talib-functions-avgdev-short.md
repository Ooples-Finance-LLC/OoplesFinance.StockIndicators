```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0       | Gen1      | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|-------------:|-------------:|------:|--------:|-----------:|----------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)vgDev [22]** | **15,217.50 μs** | **12,306.81 μs** |   **674.578 μs** | **1.001** |    **0.05** |  **1000.0000** |         **-** | **8797.02 KB** |       **1.000** |
| Competitor | 1000  | TaLib(...)vgDev [22] |     51.57 μs |     73.26 μs |     4.015 μs | 0.003 |    0.00 |          - |         - |   20.23 KB |       0.002 |
|            |       |                      |              |              |              |       |         |            |           |            |             |
| **Ooples**     | **10000** | **TaLib(...)vgDev [22]** | **61,401.20 μs** | **84,506.04 μs** | **4,632.061 μs** | **1.004** |    **0.09** | **10000.0000** | **1000.0000** |   **89674 KB** |       **1.000** |
| Competitor | 10000 | TaLib(...)vgDev [22] |    502.00 μs |  3,864.44 μs |   211.823 μs | 0.008 |    0.00 |          - |         - |   162.5 KB |       0.002 |
