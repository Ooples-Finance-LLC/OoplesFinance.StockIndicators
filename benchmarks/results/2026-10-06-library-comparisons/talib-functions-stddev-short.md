```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean          | Error         | StdDev       | Median        | Ratio | RatioSD | Gen0       | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |--------------:|--------------:|-------------:|--------------:|------:|--------:|-----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)tdDev [22]** |  **21,910.90 μs** |  **22,140.23 μs** | **1,213.581 μs** |  **21,633.70 μs** | **1.002** |    **0.07** |  **1000.0000** |         **-** |  **8666.34 KB** |       **1.000** |
| Competitor | 1000  | TaLib(...)tdDev [22] |      47.53 μs |     129.19 μs |     7.081 μs |      43.80 μs | 0.002 |    0.00 |          - |         - |    20.59 KB |       0.002 |
|            |       |                      |               |               |              |               |       |         |            |           |             |             |
| **Ooples**     | **10000** | **TaLib(...)tdDev [22]** | **136,988.53 μs** | **123,623.63 μs** | **6,776.229 μs** | **133,677.30 μs** | **1.002** |    **0.06** | **10000.0000** | **1000.0000** | **87943.53 KB** |       **1.000** |
| Competitor | 10000 | TaLib(...)tdDev [22] |     326.20 μs |   5,392.96 μs |   295.606 μs |     173.90 μs | 0.002 |    0.00 |          - |         - |   157.98 KB |       0.002 |
