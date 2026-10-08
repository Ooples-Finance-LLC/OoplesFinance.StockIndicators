```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |------------:|------------:|------------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skend(...)sform [26]** | **11,331.8 μs** |  **7,117.2 μs** |   **390.12 μs** |  **1.00** |    **0.04** |         **-** |         **-** |  **3226.35 KB** |        **1.00** |
| Competitor | 1000  | Skend(...)sform [26] |    702.5 μs |    788.6 μs |    43.23 μs |  0.06 |    0.00 |         - |         - |   177.75 KB |        0.06 |
|            |       |                      |             |             |             |       |         |           |           |             |             |
| **Ooples**     | **10000** | **Skend(...)sform [26]** | **35,347.2 μs** | **81,238.7 μs** | **4,452.97 μs** |  **1.01** |    **0.16** | **3000.0000** | **1000.0000** | **33348.02 KB** |        **1.00** |
| Competitor | 10000 | Skend(...)sform [26] |  2,135.3 μs |  2,748.8 μs |   150.67 μs |  0.06 |    0.01 |         - |         - |  1706.67 KB |        0.05 |
