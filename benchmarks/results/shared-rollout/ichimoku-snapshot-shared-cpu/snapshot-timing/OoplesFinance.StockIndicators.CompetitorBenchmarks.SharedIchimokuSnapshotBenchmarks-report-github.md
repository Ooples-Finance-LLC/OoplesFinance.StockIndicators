```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method   | Count | Operation            | Mean       | Error     | StdDev    | Gen0     | Gen1     | Allocated  |
|--------- |------ |--------------------- |-----------:|----------:|----------:|---------:|---------:|-----------:|
| **Snapshot** | **1000**  | **Skender.GetIchimoku**  |   **254.0 μs** |   **1.90 μs** |   **1.13 μs** |  **15.6250** |        **-** |  **129.13 KB** |
| **Snapshot** | **1000**  | **Trady(...)Cloud [29]** |   **275.8 μs** |  **12.97 μs** |   **8.58 μs** |  **19.5313** |   **3.9063** |  **175.15 KB** |
| **Snapshot** | **10000** | **Skender.GetIchimoku**  | **2,946.1 μs** | **490.16 μs** | **291.69 μs** | **140.6250** |  **62.5000** | **1255.28 KB** |
| **Snapshot** | **10000** | **Trady(...)Cloud [29]** | **2,917.3 μs** |  **74.08 μs** |  **44.08 μs** | **187.5000** | **171.8750** | **1652.83 KB** |
