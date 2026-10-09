```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=5  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method        | Count | Mean      | Error     | StdDev   | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|-------------- |------ |----------:|----------:|---------:|------:|--------:|-------:|-------:|----------:|------------:|
| **Scalar**        | **1000**  |  **10.26 μs** |  **4.785 μs** | **1.243 μs** |  **0.90** |    **0.10** | **0.8545** |      **-** |   **7.84 KB** |        **1.00** |
| TensorsVector | 1000  |  10.80 μs |  1.307 μs | 0.340 μs |  0.95 |    0.03 | 0.8545 |      - |   7.84 KB |        1.00 |
| Competitor    | 1000  |  11.38 μs |  0.808 μs | 0.210 μs |  1.00 |    0.02 | 0.9155 |      - |   7.87 KB |        1.00 |
|               |       |           |           |          |       |         |        |        |           |             |
| **Scalar**        | **10000** | **146.73 μs** | **18.563 μs** | **4.821 μs** |  **0.94** |    **0.03** | **8.7891** |      **-** |  **78.16 KB** |        **1.00** |
| TensorsVector | 10000 | 162.47 μs | 25.579 μs | 3.958 μs |  1.04 |    0.02 | 8.7891 |      - |  78.16 KB |        1.00 |
| Competitor    | 10000 | 156.63 μs |  7.599 μs | 1.176 μs |  1.00 |    0.01 | 8.7891 | 0.9766 |  78.19 KB |        1.00 |
