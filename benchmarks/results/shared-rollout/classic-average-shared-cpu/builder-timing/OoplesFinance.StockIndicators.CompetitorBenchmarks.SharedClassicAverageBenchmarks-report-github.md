```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.402
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation           | Mean       | Error     | StdDev    | Gen0      | Gen1     | Gen2    | Allocated   |
|------------------ |------ |-------------------- |-----------:|----------:|----------:|----------:|---------:|--------:|------------:|
| **BuilderFull**       | **1000**  | **TaLib.Functions.Apo** |   **521.6 μs** |  **32.31 μs** |  **21.37 μs** |  **250.0000** |  **62.5000** |       **-** |  **2027.88 KB** |
| BuilderLatestOnly | 1000  | TaLib.Functions.Apo |   496.3 μs |   8.22 μs |   5.44 μs |  242.1875 |  58.5938 |       - |   1980.3 KB |
| **BuilderFull**       | **1000**  | **TaLib.Functions.Ma**  |   **160.8 μs** |   **4.66 μs** |   **3.08 μs** |   **84.9609** |  **20.5078** |       **-** |   **688.01 KB** |
| BuilderLatestOnly | 1000  | TaLib.Functions.Ma  |   153.3 μs |   2.42 μs |   1.26 μs |   79.1016 |  13.6719 |       - |   640.39 KB |
| **BuilderFull**       | **1000**  | **TaLib.Functions.Ppo** |   **578.2 μs** |  **13.40 μs** |   **7.97 μs** |  **304.6875** |  **93.7500** |       **-** |  **2490.46 KB** |
| BuilderLatestOnly | 1000  | TaLib.Functions.Ppo |   578.9 μs |  24.30 μs |  14.46 μs |  300.7813 |  74.2188 |       - |  2442.84 KB |
| **BuilderFull**       | **10000** | **TaLib.Functions.Apo** | **5,326.3 μs** | **427.06 μs** | **282.47 μs** | **2375.0000** | **343.7500** | **62.5000** | **18896.19 KB** |
| BuilderLatestOnly | 10000 | TaLib.Functions.Apo | 4,403.6 μs | 166.84 μs |  99.28 μs | 2250.0000 |  62.5000 |       - | 18425.44 KB |
| **BuilderFull**       | **10000** | **TaLib.Functions.Ma**  | **1,368.4 μs** | **142.64 μs** |  **84.88 μs** |  **781.2500** | **718.7500** |       **-** |  **6313.27 KB** |
| BuilderLatestOnly | 10000 | TaLib.Functions.Ma  | 1,248.0 μs |  48.84 μs |  29.06 μs |  726.5625 | 242.1875 |       - |  5843.41 KB |
| **BuilderFull**       | **10000** | **TaLib.Functions.Ppo** | **6,175.4 μs** | **186.49 μs** |  **97.54 μs** | **2968.7500** | **343.7500** | **62.5000** | **23626.31 KB** |
| BuilderLatestOnly | 10000 | TaLib.Functions.Ppo | 5,249.6 μs |  73.91 μs |  43.98 μs | 2843.7500 | 218.7500 |       - | 23155.95 KB |
