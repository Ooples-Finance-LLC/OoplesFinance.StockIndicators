```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.402
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation            | Mean      | Error     | StdDev    | Gen0      | Gen1     | Allocated |
|------------------ |------ |--------------------- |----------:|----------:|----------:|----------:|---------:|----------:|
| **BuilderFull**       | **1000**  | **TaLib(...)cdFix [23]** |  **3.363 ms** | **0.5145 ms** | **0.3062 ms** |  **333.3333** |        **-** |    **3.5 MB** |
| BuilderLatestOnly | 1000  | TaLib(...)cdFix [23] |  3.176 ms | 0.1923 ms | 0.1006 ms |  406.2500 |  31.2500 |   3.46 MB |
| **BuilderFull**       | **10000** | **TaLib(...)cdFix [23]** | **33.948 ms** | **2.8510 ms** | **1.6966 ms** | **4250.0000** | **250.0000** |  **35.78 MB** |
| BuilderLatestOnly | 10000 | TaLib(...)cdFix [23] | 33.035 ms | 1.0133 ms | 0.6702 ms | 4250.0000 | 250.0000 |  35.32 MB |
