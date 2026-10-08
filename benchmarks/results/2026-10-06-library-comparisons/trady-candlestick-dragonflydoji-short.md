```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean     | Error     | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |---------:|----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)yDoji [31]** | **2.913 ms** | **0.9230 ms** | **0.0506 ms** |  **1.00** |    **0.02** |  **272.96 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)yDoji [31] | 1.851 ms | 2.2425 ms | 0.1229 ms |  0.64 |    0.04 |  847.33 KB |        3.10 |
|            |       |                      |          |           |           |       |         |            |             |
| **Ooples**     | **10000** | **Trady(...)yDoji [31]** | **3.319 ms** | **3.0696 ms** | **0.1683 ms** |  **1.00** |    **0.06** | **3790.37 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)yDoji [31] | 5.281 ms | 3.4476 ms | 0.1890 ms |  1.59 |    0.08 | 6402.09 KB |        1.69 |
