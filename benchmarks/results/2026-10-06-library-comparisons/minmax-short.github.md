```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3
LaunchCount=1  WarmupCount=3

```
| Method     | Bars  | PairId               | Mean        | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |------------:|------------:|-----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)inMax [22]** |   **365.69 μs** | **1,000.10 μs** |  **54.819 μs** |  **1.02** |    **0.19** |  **48.8281** |  **16.1133** |        **-** |  **402.46 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)inMax [22] |    10.44 μs |    19.76 μs |   1.083 μs |  0.03 |    0.00 |   3.8986 |   0.2136 |        - |   31.91 KB |        0.08 |
|            |       |                      |             |             |            |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)inMax [22]** | **3,707.82 μs** | **1,849.75 μs** | **101.391 μs** |  **1.00** |    **0.03** | **839.8438** | **714.8438** | **496.0938** | **5046.01 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)inMax [22] |   136.61 μs |   300.99 μs |  16.498 μs |  0.04 |    0.00 |  38.0859 |   9.5215 |        - |  313.16 KB |        0.06 |
