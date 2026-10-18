```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation            | Mean      | Error     | StdDev   | Gen0    | Gen1    | Allocated |
|------------------ |------ |--------------------- |----------:|----------:|---------:|--------:|--------:|----------:|
| **BuilderFull**       | **1000**  | **TaLib(...)Cover [28]** |  **25.94 μs** |  **1.313 μs** | **0.687 μs** |  **7.8125** |  **0.9766** |  **70.12 KB** |
| BuilderLatestOnly | 1000  | TaLib(...)Cover [28] |  23.79 μs |  2.343 μs | 1.550 μs |  2.6855 |       - |  22.24 KB |
| **BuilderFull**       | **1000**  | **TaLib(...)gLine [26]** |  **29.72 μs** |  **2.960 μs** | **1.761 μs** |  **8.5449** |  **1.2207** |  **70.12 KB** |
| BuilderLatestOnly | 1000  | TaLib(...)gLine [26] |  21.69 μs |  0.983 μs | 0.585 μs |  2.6855 |       - |  22.23 KB |
| **BuilderFull**       | **10000** | **TaLib(...)Cover [28]** | **190.04 μs** |  **3.651 μs** | **2.415 μs** | **68.3594** | **27.3438** | **562.69 KB** |
| BuilderLatestOnly | 10000 | TaLib(...)Cover [28] | 151.51 μs |  2.539 μs | 1.328 μs | 10.7422 |  0.9766 |  92.56 KB |
| **BuilderFull**       | **10000** | **TaLib(...)gLine [26]** | **198.96 μs** | **12.737 μs** | **8.425 μs** | **68.3594** | **27.3438** | **562.67 KB** |
| BuilderLatestOnly | 10000 | TaLib(...)gLine [26] | 151.87 μs |  4.935 μs | 2.581 μs | 10.7422 |  0.9766 |  92.55 KB |
