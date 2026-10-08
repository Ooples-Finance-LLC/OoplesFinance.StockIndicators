```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean       | Error     | StdDev   | Ratio | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|----------:|---------:|------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)ength [29]** |   **215.0 μs** |  **20.08 μs** |  **1.10 μs** |  **1.00** |  **32.7148** |   **8.7891** |        **-** |  **267.63 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)ength [29] |   102.3 μs |  19.87 μs |  1.09 μs |  0.48 |   1.4648 |        - |        - |   12.08 KB |        0.05 |
|            |       |                      |            |           |          |       |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)ength [29]** | **2,304.1 μs** | **421.15 μs** | **23.08 μs** |  **1.00** | **679.6875** | **554.6875** | **496.0938** | **3714.91 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)ength [29] | 1,031.1 μs |  70.90 μs |  3.89 μs |  0.45 |  13.6719 |        - |        - |  117.55 KB |        0.03 |
