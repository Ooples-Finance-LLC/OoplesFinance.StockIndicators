```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation            | Mean      | Error     | StdDev    | Gen0      | Gen1     | Gen2     | Allocated |
|------------------ |------ |--------------------- |----------:|----------:|----------:|----------:|---------:|---------:|----------:|
| **BuilderFull**       | **1000**  | **TaLib.Functions.Mavp** |  **4.401 ms** | **0.9961 ms** | **0.6588 ms** |  **625.0000** | **125.0000** |        **-** |   **5.35 MB** |
| BuilderLatestOnly | 1000  | TaLib.Functions.Mavp |  3.867 ms | 0.1524 ms | 0.0907 ms |  656.2500 | 187.5000 |        - |   5.35 MB |
| **BuilderFull**       | **10000** | **TaLib.Functions.Mavp** | **41.778 ms** | **2.4341 ms** | **1.6100 ms** | **6750.0000** | **500.0000** | **250.0000** |  **56.57 MB** |
| BuilderLatestOnly | 10000 | TaLib.Functions.Mavp | 41.531 ms | 0.7459 ms | 0.3901 ms | 6750.0000 | 500.0000 | 250.0000 |  56.57 MB |
