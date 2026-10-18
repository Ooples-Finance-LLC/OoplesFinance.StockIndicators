```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.402
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation            | Mean      | Error     | StdDev    | Gen0    | Gen1    | Allocated |
|------------------ |------ |--------------------- |----------:|----------:|----------:|--------:|--------:|----------:|
| **BuilderFull**       | **1000**  | **TaLib(...)Block [26]** |  **88.94 μs** |  **2.153 μs** |  **1.281 μs** | **11.7188** |  **1.9531** |  **95.94 KB** |
| BuilderLatestOnly | 1000  | TaLib(...)Block [26] |  86.78 μs |  5.568 μs |  3.683 μs |  5.8594 |  0.4883 |  48.05 KB |
| **BuilderFull**       | **1000**  | **TaLib(...)alled [21]** |  **77.64 μs** |  **1.732 μs** |  **1.146 μs** | **12.6953** |  **2.4414** | **102.13 KB** |
| BuilderLatestOnly | 1000  | TaLib(...)alled [21] |  73.93 μs |  6.225 μs |  4.117 μs |  6.8359 |  0.4883 |  54.24 KB |
| **BuilderFull**       | **1000**  | **TaLib(...)South [31]** |  **60.50 μs** |  **1.293 μs** |  **0.676 μs** | **11.7188** |  **1.9531** |  **95.94 KB** |
| BuilderLatestOnly | 1000  | TaLib(...)South [31] |  56.04 μs |  1.074 μs |  0.710 μs |  5.8594 |  0.4883 |  48.05 KB |
| **BuilderFull**       | **10000** | **TaLib(...)Block [26]** | **705.63 μs** |  **7.392 μs** |  **4.399 μs** | **70.3125** | **31.2500** | **588.52 KB** |
| BuilderLatestOnly | 10000 | TaLib(...)Block [26] | 670.88 μs |  3.502 μs |  2.084 μs | 11.7188 |       - | 118.39 KB |
| **BuilderFull**       | **10000** | **TaLib(...)alled [21]** | **603.35 μs** | **26.832 μs** | **17.747 μs** | **70.3125** | **27.3438** | **594.71 KB** |
| BuilderLatestOnly | 10000 | TaLib(...)alled [21] | 558.91 μs |  9.531 μs |  6.304 μs | 11.7188 |       - | 124.59 KB |
| **BuilderFull**       | **10000** | **TaLib(...)South [31]** | **454.04 μs** |  **7.723 μs** |  **5.108 μs** | **70.3125** | **31.2500** | **588.53 KB** |
| BuilderLatestOnly | 10000 | TaLib(...)South [31] | 423.79 μs |  3.795 μs |  2.510 μs | 11.7188 |       - |  118.4 KB |
