```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean       | Error       | StdDev   | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|------------:|---------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)Crows [33]** |   **250.9 μs** |    **69.76 μs** |  **3.82 μs** |  **1.00** |    **0.02** |  **32.7148** |   **8.7891** |        **-** |  **267.76 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)Crows [33] |   108.7 μs |    40.11 μs |  2.20 μs |  0.43 |    0.01 |   1.4648 |        - |        - |   12.08 KB |        0.05 |
|            |       |                      |            |             |          |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)Crows [33]** | **2,723.4 μs** |   **584.30 μs** | **32.03 μs** |  **1.00** |    **0.01** | **679.6875** | **566.4063** | **496.0938** | **3714.86 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)Crows [33] | 1,195.3 μs | 1,548.90 μs | 84.90 μs |  0.44 |    0.03 |  13.6719 |        - |        - |  117.55 KB |        0.03 |
