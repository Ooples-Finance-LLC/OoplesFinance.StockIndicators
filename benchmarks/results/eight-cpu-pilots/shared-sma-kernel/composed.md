```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=15  IterationTime=250ms  LaunchCount=1
UnrollFactor=1  WarmupCount=8

```
| Method   | Count | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|--------- |------ |----------:|----------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Fused**    | **1000**  |  **23.92 μs** |  **0.998 μs** |  **0.934 μs** |  **0.29** |    **0.06** |   **7.9688** |  **1.3281** |  **65.35 KB** |        **0.69** |
| Ordinary | 1000  |  85.49 μs | 18.070 μs | 16.903 μs |  1.04 |    0.28 |  11.3323 |  2.1440 |  95.15 KB |        1.00 |
|          |       |           |           |           |       |         |          |         |           |             |
| **Fused**    | **10000** | **226.03 μs** | **18.696 μs** | **14.597 μs** |  **0.34** |    **0.04** |  **75.6914** | **42.2125** | **628.34 KB** |        **0.72** |
| Ordinary | 10000 | 662.46 μs | 72.214 μs | 60.302 μs |  1.01 |    0.12 | 105.4545 | 58.1818 | 869.09 KB |        1.00 |
