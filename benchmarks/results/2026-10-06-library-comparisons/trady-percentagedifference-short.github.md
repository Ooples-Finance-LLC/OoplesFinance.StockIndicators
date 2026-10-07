```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|------------:|----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)rence [36]** |   **480.6 μs** |    **187.7 μs** |  **10.29 μs** |  **1.00** |    **0.03** |  **69.3359** |  **21.9727** |        **-** |  **568.94 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)rence [36] |   479.8 μs |    169.5 μs |   9.29 μs |  1.00 |    0.02 |  78.1250 |  25.8789 |        - |     641 KB |        1.13 |
|            |       |                      |            |             |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **Trady(...)rence [36]** | **6,621.8 μs** |  **1,047.5 μs** |  **57.42 μs** |  **1.00** |    **0.01** | **984.3750** | **812.5000** | **429.6875** | **6783.28 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)rence [36] | 9,975.0 μs | 10,426.6 μs | 571.52 μs |  1.51 |    0.08 | 890.6250 | 609.3750 | 312.5000 | 6300.06 KB |        0.93 |
