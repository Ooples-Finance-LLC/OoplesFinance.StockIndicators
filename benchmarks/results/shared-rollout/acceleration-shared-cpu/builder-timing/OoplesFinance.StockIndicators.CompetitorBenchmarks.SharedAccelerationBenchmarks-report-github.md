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
| **BuilderFull**       | **1000**  | **TaLib(...)bands [24]** |  **6.175 ms** | **0.3248 ms** | **0.2148 ms** |  **500.0000** | **125.0000** |    **4.8 MB** |
| BuilderLatestOnly | 1000  | TaLib(...)bands [24] |  5.898 ms | 0.2447 ms | 0.1280 ms |  593.7500 |  93.7500 |   4.76 MB |
| **BuilderFull**       | **10000** | **TaLib(...)bands [24]** | **15.410 ms** | **0.1897 ms** | **0.0992 ms** | **6142.8571** | **285.7143** |  **48.66 MB** |
| BuilderLatestOnly | 10000 | TaLib(...)bands [24] | 15.261 ms | 0.2379 ms | 0.1574 ms | 6000.0000 | 285.7143 |  48.21 MB |
