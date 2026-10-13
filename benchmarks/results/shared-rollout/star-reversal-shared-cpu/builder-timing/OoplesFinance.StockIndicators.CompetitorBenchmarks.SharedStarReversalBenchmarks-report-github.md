```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.402
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation            | Mean      | Error     | StdDev    | Gen0    | Gen1    | Allocated |
|------------------ |------ |--------------------- |----------:|----------:|----------:|--------:|--------:|----------:|
| **BuilderFull**       | **1000**  | **TaLib(...)dBaby [27]** |  **42.28 μs** |  **2.203 μs** |  **1.457 μs** | **11.7188** |  **1.9531** |  **94.82 KB** |
| BuilderLatestOnly | 1000  | TaLib(...)dBaby [27] |  40.29 μs |  3.571 μs |  2.362 μs |  5.8594 |  0.2441 |  46.94 KB |
| **BuilderFull**       | **1000**  | **TaLib(...)iStar [29]** |  **44.16 μs** |  **0.913 μs** |  **0.604 μs** | **11.7188** |  **1.9531** |  **95.76 KB** |
| BuilderLatestOnly | 1000  | TaLib(...)iStar [29] |  40.29 μs |  0.586 μs |  0.306 μs |  6.1035 |  0.2441 |  47.87 KB |
| **BuilderFull**       | **1000**  | **TaLib(...)gStar [25]** |  **40.64 μs** |  **3.606 μs** |  **2.385 μs** | **10.9863** |  **1.7090** |  **88.44 KB** |
| BuilderLatestOnly | 1000  | TaLib(...)gStar [25] |  35.00 μs |  2.175 μs |  1.439 μs |  5.3711 |  0.2441 |  40.55 KB |
| **BuilderFull**       | **1000**  | **TaLib(...)iStar [29]** |  **47.50 μs** |  **2.919 μs** |  **1.930 μs** | **11.7188** |  **1.9531** |  **96.23 KB** |
| BuilderLatestOnly | 1000  | TaLib(...)iStar [29] |  39.59 μs |  1.143 μs |  0.756 μs |  6.1035 |  0.2441 |  48.34 KB |
| **BuilderFull**       | **1000**  | **TaLib(...)gStar [25]** |  **39.35 μs** |  **3.069 μs** |  **1.826 μs** | **11.2305** |  **1.7090** |  **88.44 KB** |
| BuilderLatestOnly | 1000  | TaLib(...)gStar [25] |  34.80 μs |  0.974 μs |  0.644 μs |  5.3711 |  0.2441 |  40.55 KB |
| **BuilderFull**       | **10000** | **TaLib(...)dBaby [27]** | **302.50 μs** |  **4.614 μs** |  **3.052 μs** | **70.3125** | **56.6406** | **587.39 KB** |
| BuilderLatestOnly | 10000 | TaLib(...)dBaby [27] | 269.85 μs |  2.805 μs |  1.467 μs | 13.6719 |  1.9531 | 117.27 KB |
| **BuilderFull**       | **10000** | **TaLib(...)iStar [29]** | **321.93 μs** |  **9.788 μs** |  **6.474 μs** | **74.2188** | **41.0156** | **608.49 KB** |
| BuilderLatestOnly | 10000 | TaLib(...)iStar [29] | 285.81 μs |  2.063 μs |  1.227 μs | 15.6250 |  3.9063 | 138.36 KB |
| **BuilderFull**       | **10000** | **TaLib(...)gStar [25]** | **273.17 μs** |  **3.269 μs** |  **1.945 μs** | **76.1719** | **39.0625** | **621.32 KB** |
| BuilderLatestOnly | 10000 | TaLib(...)gStar [25] | 245.11 μs |  5.310 μs |  3.160 μs | 17.5781 |  3.9063 |  151.2 KB |
| **BuilderFull**       | **10000** | **TaLib(...)iStar [29]** | **331.56 μs** | **24.372 μs** | **16.120 μs** | **72.2656** | **35.1563** | **608.02 KB** |
| BuilderLatestOnly | 10000 | TaLib(...)iStar [29] | 283.65 μs |  1.989 μs |  1.183 μs | 15.6250 |  3.9063 | 137.89 KB |
| **BuilderFull**       | **10000** | **TaLib(...)gStar [25]** | **275.61 μs** |  **9.240 μs** |  **6.112 μs** | **76.1719** | **74.2188** | **626.01 KB** |
| BuilderLatestOnly | 10000 | TaLib(...)gStar [25] | 239.21 μs |  3.477 μs |  2.069 μs | 19.5313 |  3.9063 | 155.89 KB |
