```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.402
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method   | Count | Operation           | Mean       | Error     | StdDev    | Gen0      | Gen1     | Allocated  |
|--------- |------ |-------------------- |-----------:|----------:|----------:|----------:|---------:|-----------:|
| **Snapshot** | **1000**  | **Skender.GetRenko**    |   **526.3 μs** |  **27.84 μs** |  **16.57 μs** |  **109.3750** |        **-** |  **938.91 KB** |
| **Snapshot** | **1000**  | **Skender.GetRenkoAtr** |   **586.5 μs** |  **38.78 μs** |  **20.28 μs** |  **109.3750** |        **-** |  **908.93 KB** |
| **Snapshot** | **10000** | **Skender.GetRenko**    | **5,610.9 μs** | **655.20 μs** | **389.90 μs** | **1125.0000** | **156.2500** | **9384.36 KB** |
| **Snapshot** | **10000** | **Skender.GetRenkoAtr** | **5,911.8 μs** | **299.78 μs** | **198.29 μs** | **1093.7500** |  **62.5000** | **9118.48 KB** |
