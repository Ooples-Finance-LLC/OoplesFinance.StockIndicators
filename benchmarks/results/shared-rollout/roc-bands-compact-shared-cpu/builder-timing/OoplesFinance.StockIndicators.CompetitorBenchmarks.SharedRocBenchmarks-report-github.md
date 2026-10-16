```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.402
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation        | Mean       | Error     | StdDev    | Gen0    | Gen1    | Allocated |
|------------------ |------ |----------------- |-----------:|----------:|----------:|--------:|--------:|----------:|
| **BuilderFull**       | **1000**  | **Skender.GetRocWb** |   **457.1 μs** |  **11.45 μs** |   **5.99 μs** |       **-** |       **-** | **105.36 KB** |
| BuilderLatestOnly | 1000  | Skender.GetRocWb |   457.7 μs |  13.85 μs |   8.24 μs |  3.9063 |       - |  57.72 KB |
| **BuilderFull**       | **10000** | **Skender.GetRocWb** | **4,731.0 μs** | **228.00 μs** | **150.81 μs** | **93.7500** | **31.2500** |   **1020 KB** |
| BuilderLatestOnly | 10000 | Skender.GetRocWb | 4,696.6 μs | 149.09 μs |  98.62 μs | 62.5000 | 31.2500 | 550.18 KB |
