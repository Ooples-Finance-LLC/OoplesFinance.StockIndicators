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
| **Ooples**     | **1000**  | **Trady(...)hange [28]** |   **482.5 μs** |    **76.28 μs** |   **4.18 μs** |  **1.00** |    **0.01** |  **69.3359** |  **21.9727** |        **-** |  **568.94 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)hange [28] |   476.0 μs |   174.94 μs |   9.59 μs |  0.99 |    0.02 |  78.1250 |  25.8789 |        - |     641 KB |        1.13 |
|            |       |                      |            |             |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **Trady(...)hange [28]** | **5,858.6 μs** | **4,686.74 μs** | **256.90 μs** |  **1.00** |    **0.05** | **984.3750** | **812.5000** | **429.6875** | **6783.33 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)hange [28] | 9,301.2 μs | 8,107.52 μs | 444.40 μs |  1.59 |    0.09 | 890.6250 | 609.3750 | 312.5000 | 6300.12 KB |        0.93 |
