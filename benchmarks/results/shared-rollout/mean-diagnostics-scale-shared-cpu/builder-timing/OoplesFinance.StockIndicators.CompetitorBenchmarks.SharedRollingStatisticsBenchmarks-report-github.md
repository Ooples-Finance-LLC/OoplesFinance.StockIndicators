```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.402
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation            | Mean      | Error     | StdDev    | Gen0       | Gen1     | Allocated |
|------------------ |------ |--------------------- |----------:|----------:|----------:|-----------:|---------:|----------:|
| **BuilderFull**       | **1000**  | **Skend(...)lysis [22]** | **13.706 ms** | **7.6371 ms** | **5.0515 ms** |  **2250.0000** | **250.0000** |  **19.52 MB** |
| BuilderLatestOnly | 1000  | Skend(...)lysis [22] |  7.079 ms | 0.1045 ms | 0.0622 ms |  2400.0000 | 466.6667 |  19.48 MB |
| **BuilderFull**       | **10000** | **Skend(...)lysis [22]** | **71.889 ms** | **4.0341 ms** | **2.6683 ms** | **24750.0000** | **250.0000** | **197.86 MB** |
| BuilderLatestOnly | 10000 | Skend(...)lysis [22] | 69.185 ms | 2.2930 ms | 1.3645 ms | 24750.0000 | 250.0000 |  197.4 MB |
