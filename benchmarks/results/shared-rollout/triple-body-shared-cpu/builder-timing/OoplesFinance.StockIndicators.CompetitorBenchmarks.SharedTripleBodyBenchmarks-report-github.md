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
| **BuilderFull**       | **1000**  | **TaLib(...)nside [25]** |  **38.12 μs** |  **2.157 μs** | **1.427 μs** |  **9.7656** |  **0.9766** |  **82.68 KB** |
| BuilderLatestOnly | 1000  | TaLib(...)nside [25] |  32.16 μs |  2.142 μs | 1.275 μs |  4.3945 |       - |  34.79 KB |
| **BuilderFull**       | **1000**  | **TaLib(...)Crows [22]** |  **29.22 μs** |  **1.132 μs** | **0.674 μs** |  **8.7891** |  **1.4648** |  **71.62 KB** |
| BuilderLatestOnly | 1000  | TaLib(...)Crows [22] |  25.33 μs |  2.251 μs | 1.489 μs |  3.0518 |       - |  23.73 KB |
| **BuilderFull**       | **1000**  | **TaLib(...)River [30]** |  **37.90 μs** |  **3.263 μs** | **2.158 μs** | **10.2539** |  **1.7090** |  **82.68 KB** |
| BuilderLatestOnly | 1000  | TaLib(...)River [30] |  33.19 μs |  3.306 μs | 1.968 μs |  4.3945 |       - |  34.79 KB |
| **BuilderFull**       | **1000**  | **TaLib(...)Crows [31]** |  **37.17 μs** |  **1.543 μs** | **0.918 μs** | **10.2539** |  **1.7090** |  **82.68 KB** |
| BuilderLatestOnly | 1000  | TaLib(...)Crows [31] |  32.12 μs |  0.987 μs | 0.653 μs |  4.3945 |       - |  34.79 KB |
| **BuilderFull**       | **10000** | **TaLib(...)nside [25]** | **272.79 μs** | **11.670 μs** | **7.719 μs** | **68.3594** | **29.2969** | **575.25 KB** |
| BuilderLatestOnly | 10000 | TaLib(...)nside [25] | 233.23 μs |  3.540 μs | 2.342 μs | 11.7188 |  1.9531 | 105.13 KB |
| **BuilderFull**       | **10000** | **TaLib(...)Crows [22]** | **200.35 μs** |  **1.656 μs** | **0.866 μs** | **68.3594** | **35.1563** | **564.19 KB** |
| BuilderLatestOnly | 10000 | TaLib(...)Crows [22] | 163.59 μs |  2.225 μs | 1.164 μs | 10.7422 |  1.9531 |  94.05 KB |
| **BuilderFull**       | **10000** | **TaLib(...)River [30]** | **264.06 μs** |  **2.414 μs** | **1.597 μs** | **68.3594** | **29.2969** | **575.25 KB** |
| BuilderLatestOnly | 10000 | TaLib(...)River [30] | 232.27 μs |  3.260 μs | 2.156 μs | 11.7188 |  1.9531 | 105.13 KB |
| **BuilderFull**       | **10000** | **TaLib(...)Crows [31]** | **275.92 μs** | **12.183 μs** | **8.059 μs** | **68.3594** | **29.2969** | **575.25 KB** |
| BuilderLatestOnly | 10000 | TaLib(...)Crows [31] | 234.47 μs |  8.519 μs | 5.635 μs | 11.7188 |  1.9531 | 105.13 KB |
