```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.402
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method   | Count | Operation            | Mean        | Error     | StdDev    | Gen0     | Gen1     | Gen2    | Allocated  |
|--------- |------ |--------------------- |------------:|----------:|----------:|---------:|---------:|--------:|-----------:|
| **Snapshot** | **1000**  | **Skend(...)oints [22]** |    **11.70 μs** |  **0.229 μs** |  **0.137 μs** |   **0.9766** |        **-** |       **-** |   **10.49 KB** |
| **Snapshot** | **1000**  | **Skender.GetPivots**    |   **102.87 μs** |  **9.874 μs** |  **6.531 μs** |  **27.3438** |   **7.8125** |       **-** |  **250.27 KB** |
| **Snapshot** | **10000** | **Skend(...)oints [22]** |   **119.76 μs** |  **3.644 μs** |  **2.410 μs** |  **11.7188** |   **2.9297** |       **-** |  **104.25 KB** |
| **Snapshot** | **10000** | **Skender.GetPivots**    | **1,670.04 μs** | **66.841 μs** | **44.211 μs** | **296.8750** | **195.3125** | **93.7500** | **2501.04 KB** |
