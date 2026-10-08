```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean       | Error       | StdDev   | Ratio | RatioSD | Gen0      | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|------------:|---------:|------:|--------:|----------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)erage [35]** |   **148.8 μs** |    **89.49 μs** |  **4.91 μs** |  **1.00** |    **0.04** |   **35.4004** |   **9.5215** |        **-** |  **290.98 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)erage [35] |   758.0 μs |    64.93 μs |  3.56 μs |  5.10 |    0.14 |   92.7734 |  27.3438 |        - |  762.67 KB |        2.62 |
|            |       |                      |            |             |          |       |         |           |          |          |            |             |
| **Ooples**     | **10000** | **Trady(...)erage [35]** | **1,742.8 μs** |   **234.56 μs** | **12.86 μs** |  **1.00** |    **0.01** |  **560.5469** | **435.5469** | **349.6094** | **3947.17 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)erage [35] | 9,920.8 μs | 1,220.78 μs | 66.91 μs |  5.69 |    0.05 | 1031.2500 | 765.6250 | 312.5000 | 7538.01 KB |        1.91 |
