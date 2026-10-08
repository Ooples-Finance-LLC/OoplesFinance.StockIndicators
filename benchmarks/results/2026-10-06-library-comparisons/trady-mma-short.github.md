```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|-----------:|----------:|------:|--------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)erage [37]** |   **2.372 ms** |  **0.4940 ms** | **0.0271 ms** |  **1.00** |    **0.01** |         **-** |    **264.5 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)erage [37] |  27.057 ms | 40.0805 ms | 2.1969 ms | 11.41 |    0.81 |         - |   1485.2 KB |        5.62 |
|            |       |                      |            |            |           |       |         |           |             |             |
| **Ooples**     | **10000** | **Trady(...)erage [37]** |   **3.371 ms** |  **4.8959 ms** | **0.2684 ms** |  **1.00** |    **0.10** |         **-** |  **3712.25 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)erage [37] | 243.147 ms | 30.2365 ms | 1.6574 ms | 72.42 |    4.86 | 1000.0000 | 13291.04 KB |        3.58 |
