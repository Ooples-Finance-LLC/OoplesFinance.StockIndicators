```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean       | Error      | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|-----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)onOsc [24]** | **2,950.1 μs** | **7,994.2 μs** | **438.19 μs** |  **1.01** |    **0.18** |  **509.27 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)onOsc [24] |   237.8 μs |   404.9 μs |  22.20 μs |  0.08 |    0.01 |   77.77 KB |        0.15 |
|            |       |                      |            |            |           |       |         |            |             |
| **Ooples**     | **10000** | **TaLib(...)onOsc [24]** | **4,421.2 μs** | **6,997.4 μs** | **383.55 μs** |  **1.00** |    **0.10** | **6142.22 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)onOsc [24] |   371.8 μs |   764.7 μs |  41.91 μs |  0.08 |    0.01 |  716.37 KB |        0.12 |
