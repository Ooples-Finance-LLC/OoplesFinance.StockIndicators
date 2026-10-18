```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.402
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation      | Mean      | Error     | StdDev    | Gen0      | Gen1     | Allocated |
|------------------ |------ |--------------- |----------:|----------:|----------:|----------:|---------:|----------:|
| **BuilderFull**       | **1000**  | **Skender.GetPrs** |  **1.829 ms** | **0.0911 ms** | **0.0602 ms** |  **531.2500** | **156.2500** |   **4.23 MB** |
| BuilderLatestOnly | 1000  | Skender.GetPrs |  1.701 ms | 0.0231 ms | 0.0153 ms |  515.6250 | 156.2500 |   4.18 MB |
| **BuilderFull**       | **10000** | **Skender.GetPrs** | **16.053 ms** | **0.1697 ms** | **0.1010 ms** | **5000.0000** | **285.7143** |  **40.28 MB** |
| BuilderLatestOnly | 10000 | Skender.GetPrs | 15.833 ms | 0.4231 ms | 0.2798 ms | 5000.0000 | 285.7143 |  39.82 MB |
