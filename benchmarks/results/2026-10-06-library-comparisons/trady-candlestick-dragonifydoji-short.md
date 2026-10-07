```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error    | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |----------:|---------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)yDoji [31]** |  **3.062 ms** | **4.689 ms** | **0.2570 ms** |  **1.00** |    **0.11** |  **272.96 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)yDoji [31] |  1.954 ms | 1.174 ms | 0.0643 ms |  0.64 |    0.05 |  847.64 KB |        3.11 |
|            |       |                      |           |          |           |       |         |            |             |
| **Ooples**     | **10000** | **Trady(...)yDoji [31]** | **18.947 ms** | **8.614 ms** | **0.4722 ms** |  **1.00** |    **0.03** | **3791.63 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)yDoji [31] |  5.560 ms | 4.807 ms | 0.2635 ms |  0.29 |    0.01 |  6402.5 KB |        1.69 |
