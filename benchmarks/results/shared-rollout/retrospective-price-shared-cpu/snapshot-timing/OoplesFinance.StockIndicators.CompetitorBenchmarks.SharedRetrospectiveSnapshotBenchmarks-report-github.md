```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method   | Count | Operation      | Mean      | Error     | StdDev    | Gen0    | Gen1    | Allocated |
|--------- |------ |--------------- |----------:|----------:|----------:|--------:|--------:|----------:|
| **Snapshot** | **1000**  | **Skender.GetDpo** |  **53.20 μs** |  **0.517 μs** |  **0.308 μs** |  **5.8594** |       **-** |  **54.71 KB** |
| **Snapshot** | **1000**  | **Skender.GetFcb** |  **26.21 μs** |  **3.074 μs** |  **1.829 μs** |  **5.8594** |       **-** |  **56.05 KB** |
| **Snapshot** | **10000** | **Skender.GetDpo** | **588.31 μs** | **25.874 μs** | **17.114 μs** | **66.4063** | **27.3438** | **546.93 KB** |
| **Snapshot** | **10000** | **Skender.GetFcb** | **443.49 μs** |  **4.815 μs** |  **2.866 μs** | **66.4063** | **23.4375** | **548.24 KB** |
