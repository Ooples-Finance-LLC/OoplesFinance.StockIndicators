```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.402
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation            | Mean       | Error     | StdDev    | Gen0     | Gen1     | Allocated |
|------------------ |------ |--------------------- |-----------:|----------:|----------:|---------:|---------:|----------:|
| **BuilderFull**       | **1000**  | **Skender.GetAlligator** |   **205.5 μs** |   **7.47 μs** |   **4.44 μs** |   **7.8125** |        **-** |  **96.92 KB** |
| BuilderLatestOnly | 1000  | Skender.GetAlligator |   197.9 μs |   5.90 μs |   3.51 μs |   5.8594 |        - |  49.31 KB |
| **BuilderFull**       | **1000**  | **Skender.GetGator**     |   **318.3 μs** |  **68.21 μs** |  **45.11 μs** |  **13.6719** |   **1.9531** |  **113.2 KB** |
| BuilderLatestOnly | 1000  | Skender.GetGator     |   278.8 μs |  12.04 μs |   7.17 μs |   7.8125 |        - |  65.59 KB |
| **BuilderFull**       | **10000** | **Skender.GetAlligator** | **2,036.3 μs** | **138.19 μs** |  **82.24 μs** | **109.3750** |  **46.8750** | **941.19 KB** |
| BuilderLatestOnly | 10000 | Skender.GetAlligator | 1,939.7 μs |  22.90 μs |  13.63 μs |  46.8750 |  15.6250 | 471.33 KB |
| **BuilderFull**       | **10000** | **Skender.GetGator**     | **2,957.4 μs** | **316.55 μs** | **188.37 μs** | **125.0000** | **109.3750** | **1098.1 KB** |
| BuilderLatestOnly | 10000 | Skender.GetGator     | 2,709.3 μs |  43.92 μs |  22.97 μs |  62.5000 |  15.6250 | 628.23 KB |
