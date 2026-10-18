```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.402
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation            | Mean      | Error     | StdDev    | Gen0   | Allocated |
|------------------ |------ |--------------------- |----------:|----------:|----------:|-------:|----------:|
| **BuilderFull**       | **1000**  | **Skend(...)nnels [25]** |  **1.416 ms** | **0.0742 ms** | **0.0491 ms** | **7.8125** |  **89.58 KB** |
| BuilderLatestOnly | 1000  | Skend(...)nnels [25] |  1.438 ms | 0.1658 ms | 0.1097 ms |      - |  42.09 KB |
| **BuilderFull**       | **10000** | **Skend(...)nnels [25]** | **14.117 ms** | **0.3487 ms** | **0.2306 ms** |      **-** | **864.36 KB** |
| BuilderLatestOnly | 10000 | Skend(...)nnels [25] | 14.277 ms | 0.6035 ms | 0.3591 ms |      - | 394.73 KB |
