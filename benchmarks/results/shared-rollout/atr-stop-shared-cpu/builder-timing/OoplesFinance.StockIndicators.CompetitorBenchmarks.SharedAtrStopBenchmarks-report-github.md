```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.402
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation            | Mean       | Error     | StdDev    | Gen0    | Gen1    | Allocated |
|------------------ |------ |--------------------- |-----------:|----------:|----------:|--------:|--------:|----------:|
| **BuilderFull**       | **1000**  | **Skender.GetAtrStop**   |   **413.0 μs** |  **20.77 μs** |  **13.74 μs** |       **-** |       **-** |  **96.64 KB** |
| BuilderLatestOnly | 1000  | Skender.GetAtrStop   |   412.2 μs |   6.78 μs |   4.04 μs |  3.9063 |       - |  49.06 KB |
| **BuilderFull**       | **1000**  | **Skend(...)elier [21]** |   **286.0 μs** |  **43.47 μs** |  **25.87 μs** |  **7.8125** |       **-** |   **73.3 KB** |
| BuilderLatestOnly | 1000  | Skend(...)elier [21] |   255.0 μs |   9.10 μs |   5.41 μs |  1.9531 |       - |  25.68 KB |
| **BuilderFull**       | **1000**  | **Skend(...)Trend [21]** |   **465.9 μs** |  **18.53 μs** |  **12.26 μs** | **11.7188** |       **-** |  **96.68 KB** |
| BuilderLatestOnly | 1000  | Skend(...)Trend [21] |   448.2 μs |   6.54 μs |   3.42 μs |  3.9063 |       - |  49.06 KB |
| **BuilderFull**       | **1000**  | **Trady(...)rExit [30]** |   **310.2 μs** |   **7.64 μs** |   **5.05 μs** |  **7.8125** |       **-** |  **73.67 KB** |
| BuilderLatestOnly | 1000  | Trady(...)rExit [30] |   301.5 μs |   7.70 μs |   4.58 μs |  1.9531 |       - |  26.05 KB |
| **BuilderFull**       | **10000** | **Skender.GetAtrStop**   | **4,103.0 μs** |  **24.55 μs** |  **12.84 μs** | **93.7500** | **31.2500** | **941.06 KB** |
| BuilderLatestOnly | 10000 | Skender.GetAtrStop   | 4,161.8 μs | 139.85 μs |  92.50 μs | 31.2500 |       - |  471.2 KB |
| **BuilderFull**       | **10000** | **Skend(...)elier [21]** | **2,631.4 μs** |  **57.42 μs** |  **30.03 μs** | **78.1250** | **31.2500** |  **706.6 KB** |
| BuilderLatestOnly | 10000 | Skend(...)elier [21] | 2,625.3 μs | 118.76 μs |  70.67 μs | 15.6250 |       - | 236.75 KB |
| **BuilderFull**       | **10000** | **Skend(...)Trend [21]** | **4,590.7 μs** |  **86.41 μs** |  **57.16 μs** | **93.7500** | **31.2500** | **941.06 KB** |
| BuilderLatestOnly | 10000 | Skend(...)Trend [21] | 4,619.6 μs | 197.58 μs | 130.69 μs | 31.2500 |       - | 471.19 KB |
| **BuilderFull**       | **10000** | **Trady(...)rExit [30]** | **3,198.5 μs** |  **45.92 μs** |  **27.33 μs** | **62.5000** | **31.2500** | **707.11 KB** |
| BuilderLatestOnly | 10000 | Trady(...)rExit [30] | 3,131.8 μs |  42.18 μs |  27.90 μs | 15.6250 |       - | 237.13 KB |
