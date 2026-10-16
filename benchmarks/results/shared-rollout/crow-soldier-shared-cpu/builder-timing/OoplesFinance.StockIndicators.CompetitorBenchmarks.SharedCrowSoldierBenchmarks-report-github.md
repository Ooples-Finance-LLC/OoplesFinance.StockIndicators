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
| **BuilderFull**       | **1000**  | **TaLib(...)Crows [33]** |  **34.46 μs** | **2.178 μs** | **1.441 μs** |  **9.7656** |  **0.9766** |   **79.3 KB** |
| BuilderLatestOnly | 1000  | TaLib(...)Crows [33] |  34.02 μs | 3.866 μs | 2.557 μs |  3.9063 |       - |  31.42 KB |
| **BuilderFull**       | **1000**  | **TaLib(...)Crows [29]** |  **27.90 μs** | **1.066 μs** | **0.558 μs** |  **9.0332** |  **1.4648** |  **74.43 KB** |
| BuilderLatestOnly | 1000  | TaLib(...)Crows [29] |  23.13 μs | 0.542 μs | 0.358 μs |  3.4180 |  0.1221 |  26.54 KB |
| **BuilderFull**       | **1000**  | **TaLib(...)diers [32]** |  **54.90 μs** | **6.209 μs** | **4.107 μs** | **11.7188** |  **1.9531** |  **95.25 KB** |
| BuilderLatestOnly | 1000  | TaLib(...)diers [32] |  47.49 μs | 0.817 μs | 0.540 μs |  5.8594 |  0.2441 |  47.37 KB |
| **BuilderFull**       | **10000** | **TaLib(...)Crows [33]** | **246.01 μs** | **6.712 μs** | **3.994 μs** | **68.3594** | **33.2031** | **571.87 KB** |
| BuilderLatestOnly | 10000 | TaLib(...)Crows [33] | 210.36 μs | 3.109 μs | 2.057 μs | 11.7188 |  1.9531 | 101.75 KB |
| **BuilderFull**       | **10000** | **TaLib(...)Crows [29]** | **180.09 μs** | **1.441 μs** | **0.754 μs** | **69.3359** | **41.0156** | **566.99 KB** |
| BuilderLatestOnly | 10000 | TaLib(...)Crows [29] | 147.11 μs | 1.480 μs | 0.881 μs | 11.7188 |  1.9531 |  96.87 KB |
| **BuilderFull**       | **10000** | **TaLib(...)diers [32]** | **380.14 μs** | **5.592 μs** | **3.699 μs** | **70.3125** | **42.9688** | **587.84 KB** |
| BuilderLatestOnly | 10000 | TaLib(...)diers [32] | 348.26 μs | 5.715 μs | 3.780 μs | 13.6719 |  1.9531 |  117.7 KB |
