```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |----------:|----------:|----------:|------:|--------:|----------:|----------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)edian [22]** |  **4.546 ms** |  **4.176 ms** | **0.2289 ms** |  **1.00** |    **0.06** |         **-** |         **-** |  **460.26 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)edian [22] | 10.373 ms |  4.744 ms | 0.2601 ms |  2.29 |    0.11 |         - |         - | 2963.91 KB |        6.44 |
|            |       |                      |           |           |           |       |         |           |           |            |             |
| **Ooples**     | **10000** | **Trady(...)edian [22]** | **25.669 ms** | **92.630 ms** | **5.0774 ms** |  **1.02** |    **0.24** |         **-** |         **-** | **5666.34 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)edian [22] | 30.373 ms | 75.893 ms | 4.1600 ms |  1.21 |    0.24 | 3000.0000 | 1000.0000 | 28408.1 KB |        5.01 |
