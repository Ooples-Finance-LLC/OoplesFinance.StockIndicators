```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean         | Error       | StdDev    | Ratio | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|------------:|----------:|------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)tSine [22]** |   **127.410 ms** |  **19.6381 ms** | **1.0764 ms** | **1.000** |  **9000.0000** | **1000.0000** |  **76252.01 KB** |       **1.000** |
| Competitor | 1000  | TaLib(...)tSine [22] |     1.235 ms |   0.2778 ms | 0.0152 ms | 0.010 |          - |         - |     72.27 KB |       0.001 |
|            |       |                      |              |             |           |       |            |           |              |             |
| **Ooples**     | **10000** | **TaLib(...)tSine [22]** | **1,315.843 ms** | **161.8397 ms** | **8.8710 ms** | **1.000** | **97000.0000** | **1000.0000** | **802433.71 KB** |       **1.000** |
| Competitor | 10000 | TaLib(...)tSine [22] |     6.193 ms |   2.7080 ms | 0.1484 ms | 0.005 |          - |         - |    652.06 KB |       0.001 |
