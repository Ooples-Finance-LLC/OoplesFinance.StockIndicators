```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation            | Count | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Gen2     | Allocated  | Alloc Ratio |
|------------------ |--------------------- |------ |------------:|----------:|----------:|------:|--------:|----------:|----------:|---------:|-----------:|------------:|
| **BuilderFull**       | **Skend(...)dline [22]** | **1000**  |  **3,020.2 μs** | **122.86 μs** |  **64.26 μs** | **21.35** |    **0.49** |         **-** |         **-** |        **-** |  **100.94 KB** |        **0.49** |
| BuilderLatestOnly | Skend(...)dline [22] | 1000  |  1,685.7 μs |  30.21 μs |  19.98 μs | 11.92 |    0.19 |         - |         - |        - |   53.33 KB |        0.26 |
| NativeSingle      | Skend(...)dline [22] | 1000  |    141.5 μs |   2.79 μs |   1.66 μs |  1.00 |    0.02 |   25.3906 |    6.8359 |        - |  207.71 KB |        1.00 |
| NativeParallel    | Skend(...)dline [22] | 1000  |    267.1 μs |  32.20 μs |  21.30 μs |  1.89 |    0.15 |  128.9063 |   50.7813 |        - |  945.81 KB |        4.55 |
|                   |                      |       |             |           |           |       |         |           |           |          |            |             |
| **BuilderFull**       | **Skend(...)dline [22]** | **10000** | **16,715.1 μs** | **571.60 μs** | **340.15 μs** | **10.00** |    **0.21** |         **-** |         **-** |        **-** |  **946.25 KB** |        **0.46** |
| BuilderLatestOnly | Skend(...)dline [22] | 10000 | 16,747.5 μs | 362.36 μs | 239.68 μs | 10.02 |    0.16 |         - |         - |        - |  476.29 KB |        0.23 |
| NativeSingle      | Skend(...)dline [22] | 10000 |  1,671.2 μs |  23.67 μs |  14.09 μs |  1.00 |    0.01 |  265.6250 |  187.5000 |  62.5000 | 2071.83 KB |        1.00 |
| NativeParallel    | Skend(...)dline [22] | 10000 |  5,050.1 μs | 605.60 μs | 360.38 μs |  3.02 |    0.21 | 1250.0000 | 1156.2500 | 218.7500 | 9404.33 KB |        4.54 |
