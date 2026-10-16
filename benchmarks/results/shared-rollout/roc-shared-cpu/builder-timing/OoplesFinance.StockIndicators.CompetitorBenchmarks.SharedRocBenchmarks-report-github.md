```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation        | Mean         | Error      | StdDev     | Gen0      | Gen1     | Allocated  |
|------------------ |------ |----------------- |-------------:|-----------:|-----------:|----------:|---------:|-----------:|
| **BuilderFull**       | **1000**  | **Skender.GetRoc**   |     **66.68 μs** |   **6.923 μs** |   **4.579 μs** |   **11.7188** |   **1.9531** |  **102.46 KB** |
| BuilderLatestOnly | 1000  | Skender.GetRoc   |     59.75 μs |  13.135 μs |   8.688 μs |    6.8359 |   0.4883 |   54.89 KB |
| **BuilderFull**       | **1000**  | **Skender.GetRocWb** |    **999.17 μs** |  **54.473 μs** |  **32.416 μs** |   **93.7500** |  **23.4375** |  **825.94 KB** |
| BuilderLatestOnly | 1000  | Skender.GetRocWb |    990.97 μs |  29.758 μs |  17.708 μs |   93.7500 |  15.6250 |  778.37 KB |
| **BuilderFull**       | **10000** | **Skender.GetRoc**   |    **498.81 μs** |  **24.145 μs** |  **15.970 μs** |  **105.4688** |  **70.3125** |  **876.32 KB** |
| BuilderLatestOnly | 10000 | Skender.GetRoc   |    440.34 μs |  39.297 μs |  25.993 μs |   46.8750 |  19.5313 |  406.51 KB |
| **BuilderFull**       | **10000** | **Skender.GetRocWb** | **10,618.44 μs** | **723.583 μs** | **378.448 μs** | **1000.0000** | **900.0000** | **8386.11 KB** |
| BuilderLatestOnly | 10000 | Skender.GetRocWb | 10,266.09 μs | 538.390 μs | 320.387 μs |  900.0000 | 800.0000 | 7916.11 KB |
