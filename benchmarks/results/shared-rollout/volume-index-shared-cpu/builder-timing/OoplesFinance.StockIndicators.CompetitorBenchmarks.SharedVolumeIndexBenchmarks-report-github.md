```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation            | Mean       | Error     | StdDev    | Gen0     | Gen1     | Allocated  |
|------------------ |------ |--------------------- |-----------:|----------:|----------:|---------:|---------:|-----------:|
| **BuilderFull**       | **1000**  | **Trady(...)Index [35]** |   **176.3 μs** |   **9.10 μs** |   **5.41 μs** |  **19.5313** |   **3.9063** |  **170.48 KB** |
| BuilderLatestOnly | 1000  | Trady(...)Index [35] |   186.7 μs |  18.45 μs |  12.20 μs |  13.6719 |        - |  122.87 KB |
| BuilderFull       | 1000  | Trady(...)Index [35] |   179.5 μs |  19.26 μs |  11.46 μs |  19.5313 |   2.9297 |  165.52 KB |
| BuilderLatestOnly | 1000  | Trady(...)Index [35] |   167.4 μs |   8.43 μs |   5.02 μs |  13.6719 |        - |  117.91 KB |
| **BuilderFull**       | **10000** | **Trady(...)Index [35]** | **1,884.2 μs** | **147.05 μs** |  **87.50 μs** | **203.1250** | **156.2500** | **1672.63 KB** |
| BuilderLatestOnly | 10000 | Trady(...)Index [35] | 1,813.7 μs | 178.22 μs | 106.06 μs | 140.6250 |  46.8750 | 1202.79 KB |
| BuilderFull       | 10000 | Trady(...)Index [35] | 1,777.1 μs |  71.15 μs |  47.06 μs | 203.1250 | 156.2500 | 1665.04 KB |
| BuilderLatestOnly | 10000 | Trady(...)Index [35] | 1,815.0 μs | 164.31 μs | 108.68 μs | 140.6250 |  46.8750 | 1195.16 KB |
