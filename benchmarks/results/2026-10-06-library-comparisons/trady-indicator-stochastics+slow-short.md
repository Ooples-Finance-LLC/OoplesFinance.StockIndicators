```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Gen2      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |----------:|----------:|----------:|------:|--------:|----------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)+Slow [32]** |  **4.743 ms** |  **2.987 ms** | **0.1637 ms** |  **1.00** |    **0.04** |         **-** |         **-** |         **-** |   **817.22 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)+Slow [32] |  9.704 ms |  5.062 ms | 0.2774 ms |  2.05 |    0.08 |         - |         - |         - |  5059.46 KB |        6.19 |
|            |       |                      |           |           |           |       |         |           |           |           |             |             |
| **Ooples**     | **10000** | **Trady(...)+Slow [32]** | **14.685 ms** | **41.659 ms** | **2.2835 ms** |  **1.02** |    **0.19** |         **-** |         **-** |         **-** |  **9252.76 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)+Slow [32] | 41.861 ms | 11.916 ms | 0.6531 ms |  2.89 |    0.36 | 4000.0000 | 2000.0000 | 1000.0000 | 41335.71 KB |        4.47 |
