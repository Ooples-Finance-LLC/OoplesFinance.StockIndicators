```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.402
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation            | Mean         | Error        | StdDev     | Gen0    | Gen1    | Allocated |
|------------------ |------ |--------------------- |-------------:|-------------:|-----------:|--------:|--------:|----------:|
| **BuilderFull**       | **1000**  | **Trady(...)Index [39]** |  **1,477.16 μs** |   **230.078 μs** | **152.183 μs** |       **-** |       **-** |  **64.97 KB** |
| BuilderLatestOnly | 1000  | Trady(...)Index [39] |  1,443.85 μs |   119.407 μs |  71.057 μs |       - |       - |  17.43 KB |
| **BuilderFull**       | **1000**  | **Trady(...)ating [45]** |  **1,456.67 μs** |    **17.509 μs** |  **11.581 μs** |  **7.8125** |       **-** |  **66.11 KB** |
| BuilderLatestOnly | 1000  | Trady(...)ating [45] |  1,485.81 μs |   111.988 μs |  66.642 μs |       - |       - |   18.5 KB |
| **BuilderFull**       | **1000**  | **Trady(...)Index [40]** |  **1,395.30 μs** |   **313.486 μs** | **207.352 μs** |  **7.8125** |       **-** |  **65.04 KB** |
| BuilderLatestOnly | 1000  | Trady(...)Index [40] |  1,237.59 μs |    49.540 μs |  29.480 μs |       - |       - |  17.43 KB |
| **BuilderFull**       | **1000**  | **Trady(...)cator [41]** |    **673.52 μs** |    **28.503 μs** |  **16.962 μs** |  **7.8125** |       **-** |     **65 KB** |
| BuilderLatestOnly | 1000  | Trady(...)cator [41] |    661.94 μs |    17.578 μs |  10.460 μs |       - |       - |  17.39 KB |
| **BuilderFull**       | **1000**  | **Trady(...)ement [40]** |     **30.82 μs** |     **2.573 μs** |   **1.346 μs** |  **8.0566** |  **1.2207** |  **66.82 KB** |
| BuilderLatestOnly | 1000  | Trady(...)ement [40] |     26.21 μs |     1.657 μs |   1.096 μs |  2.1973 |       - |  19.21 KB |
| **BuilderFull**       | **1000**  | **Trady(...)cator [40]** |    **628.39 μs** |     **5.813 μs** |   **3.040 μs** |  **7.8125** |       **-** |     **65 KB** |
| BuilderLatestOnly | 1000  | Trady(...)cator [40] |    646.13 μs |    76.013 μs |  50.278 μs |       - |       - |  17.39 KB |
| **BuilderFull**       | **1000**  | **Trady(...)ement [39]** |     **31.26 μs** |     **2.796 μs** |   **1.849 μs** |  **8.0566** |  **1.2207** |  **66.82 KB** |
| BuilderLatestOnly | 1000  | Trady(...)ement [39] |     26.35 μs |     1.477 μs |   0.879 μs |  2.1973 |       - |  19.21 KB |
| **BuilderFull**       | **10000** | **Trady(...)Index [39]** | **14,372.88 μs** |   **920.767 μs** | **609.030 μs** |       **-** |       **-** | **628.94 KB** |
| BuilderLatestOnly | 10000 | Trady(...)Index [39] | 14,125.27 μs |   301.162 μs | 199.200 μs |       - |       - | 159.33 KB |
| **BuilderFull**       | **10000** | **Trady(...)ating [45]** | **14,682.73 μs** |   **288.690 μs** | **190.950 μs** |       **-** |       **-** | **630.21 KB** |
| BuilderLatestOnly | 10000 | Trady(...)ating [45] | 14,901.91 μs |   528.389 μs | 314.436 μs |       - |       - |  160.4 KB |
| **BuilderFull**       | **10000** | **Trady(...)Index [40]** | **12,871.18 μs** | **1,086.026 μs** | **718.339 μs** |       **-** |       **-** | **628.97 KB** |
| BuilderLatestOnly | 10000 | Trady(...)Index [40] | 12,696.61 μs |   202.492 μs | 133.936 μs |       - |       - | 159.16 KB |
| **BuilderFull**       | **10000** | **Trady(...)cator [41]** |  **6,743.59 μs** |   **129.957 μs** |  **77.335 μs** | **66.6667** |       **-** | **628.46 KB** |
| BuilderLatestOnly | 10000 | Trady(...)cator [41] |  6,730.02 μs |   135.481 μs |  89.613 μs |       - |       - |  158.7 KB |
| **BuilderFull**       | **10000** | **Trady(...)ement [40]** |    **243.93 μs** |    **12.942 μs** |   **8.560 μs** | **76.1719** | **74.2188** | **629.71 KB** |
| BuilderLatestOnly | 10000 | Trady(...)ement [40] |    206.02 μs |     4.000 μs |   2.092 μs | 19.5313 |  5.8594 | 159.86 KB |
| **BuilderFull**       | **10000** | **Trady(...)cator [40]** |  **6,400.79 μs** |   **137.568 μs** |  **81.865 μs** | **62.5000** |       **-** | **628.42 KB** |
| BuilderLatestOnly | 10000 | Trady(...)cator [40] |  6,383.95 μs |   240.036 μs | 158.769 μs |       - |       - | 158.27 KB |
| **BuilderFull**       | **10000** | **Trady(...)ement [39]** |    **231.24 μs** |     **2.995 μs** |   **1.981 μs** | **76.1719** | **74.2188** | **629.71 KB** |
| BuilderLatestOnly | 10000 | Trady(...)ement [39] |    213.26 μs |    15.691 μs |  10.378 μs | 19.5313 |  5.8594 | 159.86 KB |
