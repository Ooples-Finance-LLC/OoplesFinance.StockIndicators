```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean        | Error        | StdDev       | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |------------:|-------------:|-------------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)AdOsc [21]** | **24,277.0 μs** |  **13,717.9 μs** |    **751.92 μs** | **1.001** |    **0.04** |         **-** |         **-** |  **7786.45 KB** |       **1.000** |
| Competitor | 1000  | TaLib(...)AdOsc [21] |    126.8 μs |     117.7 μs |      6.45 μs | 0.005 |    0.00 |         - |         - |    38.23 KB |       0.005 |
|            |       |                      |             |              |              |       |         |           |           |             |             |
| **Ooples**     | **10000** | **TaLib(...)AdOsc [21]** | **79,934.2 μs** | **282,160.5 μs** | **15,466.17 μs** | **1.023** |    **0.23** | **9000.0000** | **1000.0000** | **79508.52 KB** |       **1.000** |
| Competitor | 10000 | TaLib(...)AdOsc [21] |    189.1 μs |     813.8 μs |     44.61 μs | 0.002 |    0.00 |         - |         - |   328.94 KB |       0.004 |
