```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation            | Mean      | Error     | StdDev    | Gen0    | Gen1    | Allocated |
|------------------ |------ |--------------------- |----------:|----------:|----------:|--------:|--------:|----------:|
| **BuilderFull**       | **1000**  | **TaLib.Functions.Mavp** |  **95.69 μs** |  **6.290 μs** |  **3.743 μs** |  **7.8125** |       **-** |  **66.38 KB** |
| BuilderLatestOnly | 1000  | TaLib.Functions.Mavp |  86.29 μs |  2.668 μs |  1.395 μs |  1.9531 |       - |  18.77 KB |
| **BuilderFull**       | **10000** | **TaLib.Functions.Mavp** | **908.69 μs** | **17.736 μs** | **11.731 μs** | **70.3125** | **23.4375** | **629.33 KB** |
| BuilderLatestOnly | 10000 | TaLib.Functions.Mavp | 889.18 μs | 65.970 μs | 39.258 μs | 15.6250 |       - | 159.47 KB |
