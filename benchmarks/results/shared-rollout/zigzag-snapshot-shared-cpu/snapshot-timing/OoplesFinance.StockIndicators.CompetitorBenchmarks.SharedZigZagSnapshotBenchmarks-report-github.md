```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.402
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method   | Count | Operation         | Mean      | Error     | StdDev    | Gen0      | Gen1      | Gen2     | Allocated |
|--------- |------ |------------------ |----------:|----------:|----------:|----------:|----------:|---------:|----------:|
| **Snapshot** | **1000**  | **Skender.GetZigZag** |  **4.868 ms** | **0.1558 ms** | **0.0927 ms** |  **272.7273** |         **-** |        **-** |   **2.79 MB** |
| **Snapshot** | **10000** | **Skender.GetZigZag** | **43.091 ms** | **2.3585 ms** | **1.4035 ms** | **3250.0000** | **1250.0000** | **500.0000** |  **27.57 MB** |
