```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |------------:|------------:|----------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skend(...)elier [21]** |  **7,636.1 μs** |  **9,576.0 μs** | **524.90 μs** |  **1.00** |    **0.09** |         **-** |         **-** |  **1961.03 KB** |        **1.00** |
| Competitor | 1000  | Skend(...)elier [21] |    699.2 μs |  1,070.8 μs |  58.70 μs |  0.09 |    0.01 |         - |         - |   254.01 KB |        0.13 |
|            |       |                      |             |             |           |       |         |           |           |             |             |
| **Ooples**     | **10000** | **Skend(...)elier [21]** | **14,954.8 μs** | **12,918.3 μs** | **708.10 μs** |  **1.00** |    **0.06** | **2000.0000** | **1000.0000** | **20675.99 KB** |        **1.00** |
| Competitor | 10000 | Skend(...)elier [21] |  1,651.2 μs |  2,965.6 μs | 162.55 μs |  0.11 |    0.01 |         - |         - |  2477.64 KB |        0.12 |
