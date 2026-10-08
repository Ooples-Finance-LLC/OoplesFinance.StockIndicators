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
| **Ooples**     | **1000**  | **Trady(...)+Slow [42]** |  **4.435 ms** | **10.187 ms** | **0.5584 ms** |  **1.01** |    **0.15** |         **-** |         **-** |         **-** |   **704.38 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)+Slow [42] |  8.761 ms |  2.025 ms | 0.1110 ms |  2.00 |    0.20 |         - |         - |         - |   5480.4 KB |        7.78 |
|            |       |                      |           |           |           |       |         |           |           |           |             |             |
| **Ooples**     | **10000** | **Trady(...)+Slow [42]** | **12.672 ms** | **11.612 ms** | **0.6365 ms** |  **1.00** |    **0.06** |         **-** |         **-** |         **-** |  **8141.25 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)+Slow [42] | 41.360 ms | 14.846 ms | 0.8138 ms |  3.27 |    0.15 | 4000.0000 | 2000.0000 | 1000.0000 | 42289.63 KB |        5.19 |
