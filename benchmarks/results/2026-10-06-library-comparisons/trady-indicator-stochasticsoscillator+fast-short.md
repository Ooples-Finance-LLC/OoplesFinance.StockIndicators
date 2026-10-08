```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error      | StdDev    | Ratio | RatioSD | Gen0      | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |----------:|-----------:|----------:|------:|--------:|----------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)+Fast [42]** |  **4.547 ms** | **10.3090 ms** | **0.5651 ms** |  **1.01** |    **0.15** |         **-** |  **704.38 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)+Fast [42] |  7.872 ms |  0.3081 ms | 0.0169 ms |  1.75 |    0.18 |         - | 3365.69 KB |        4.78 |
|            |       |                      |           |            |           |       |         |           |            |             |
| **Ooples**     | **10000** | **Trady(...)+Fast [42]** | **11.646 ms** | **11.0995 ms** | **0.6084 ms** |  **1.00** |    **0.06** |         **-** | **8139.61 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)+Fast [42] | 27.750 ms | 54.6689 ms | 2.9966 ms |  2.39 |    0.25 | 1000.0000 |   25380 KB |        3.12 |
