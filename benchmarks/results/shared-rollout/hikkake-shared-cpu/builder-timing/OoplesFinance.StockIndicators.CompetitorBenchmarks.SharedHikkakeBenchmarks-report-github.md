```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.402
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation            | Mean      | Error    | StdDev   | Gen0    | Gen1    | Allocated |
|------------------ |------ |--------------------- |----------:|---------:|---------:|--------:|--------:|----------:|
| **BuilderFull**       | **1000**  | **TaLib(...)kkake [21]** |  **17.43 μs** | **1.704 μs** | **1.127 μs** |  **7.3242** |  **0.9766** |  **60.17 KB** |
| BuilderLatestOnly | 1000  | TaLib(...)kkake [21] |  10.06 μs | 0.410 μs | 0.271 μs |  1.5259 |       - |  12.28 KB |
| **BuilderFull**       | **1000**  | **TaLib(...)ified [29]** |  **29.39 μs** | **2.747 μs** | **1.635 μs** |  **7.8125** |  **1.2207** |  **65.05 KB** |
| BuilderLatestOnly | 1000  | TaLib(...)ified [29] |  23.52 μs | 0.529 μs | 0.350 μs |  2.0752 |       - |  17.17 KB |
| **BuilderFull**       | **10000** | **TaLib(...)kkake [21]** |  **81.73 μs** | **1.524 μs** | **1.008 μs** | **67.3828** | **22.4609** | **552.73 KB** |
| BuilderLatestOnly | 10000 | TaLib(...)kkake [21] |  49.95 μs | 1.239 μs | 0.819 μs |  9.7656 |  1.4648 |   82.6 KB |
| **BuilderFull**       | **10000** | **TaLib(...)ified [29]** | **203.36 μs** | **8.410 μs** | **5.563 μs** | **66.4063** | **21.4844** | **557.62 KB** |
| BuilderLatestOnly | 10000 | TaLib(...)ified [29] | 174.20 μs | 9.707 μs | 6.420 μs | 10.7422 |  1.9531 |  87.49 KB |
