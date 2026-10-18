```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation            | Count | Mean         | Error      | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------ |--------------------- |------ |-------------:|-----------:|-----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| **BuilderFull**       | **Skender.GetUltimate**  | **1000**  |    **24.515 Î¼s** |  **1.0145 Î¼s** |  **0.6710 Î¼s** |  **0.24** |    **0.01** |   **7.8125** |   **1.2207** |   **65.42 KB** |        **0.43** |
| BuilderLatestOnly | Skender.GetUltimate  | 1000  |    21.530 Î¼s |  3.4486 Î¼s |  2.2810 Î¼s |  0.22 |    0.02 |   2.0752 |   0.1221 |   17.81 KB |        0.12 |
| GpuFull           | Skender.GetUltimate  | 1000  |   349.722 Î¼s |  6.8394 Î¼s |  4.0700 Î¼s |  3.49 |    0.06 |   7.8125 |        - |   64.64 KB |        0.42 |
| GpuLatestOnly     | Skender.GetUltimate  | 1000  |   347.228 Î¼s |  5.3607 Î¼s |  3.5457 Î¼s |  3.47 |    0.05 |   1.9531 |        - |   17.03 KB |        0.11 |
| NativeSingle      | Skender.GetUltimate  | 1000  |   100.132 Î¼s |  2.1861 Î¼s |  1.3009 Î¼s |  1.00 |    0.02 |  18.5547 |   3.9063 |  152.83 KB |        1.00 |
| NativeParallel    | Skender.GetUltimate  | 1000  |    38.275 Î¼s |  1.2501 Î¼s |  0.8269 Î¼s |  0.38 |    0.01 |  23.4375 |   5.8594 |  186.31 KB |        1.22 |
|                   |                      |       |              |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **Skender.GetUltimate**  | **10000** |   **161.606 Î¼s** |  **3.0907 Î¼s** |  **2.0443 Î¼s** |  **0.14** |    **0.00** |  **76.1719** |  **37.1094** |   **628.3 KB** |        **0.41** |
| BuilderLatestOnly | Skender.GetUltimate  | 10000 |   129.877 Î¼s |  1.7685 Î¼s |  1.1698 Î¼s |  0.11 |    0.00 |  18.5547 |   5.8594 |  158.45 KB |        0.10 |
| GpuFull           | Skender.GetUltimate  | 10000 |   751.543 Î¼s | 10.6207 Î¼s |  7.0249 Î¼s |  0.65 |    0.01 |  74.2188 |  31.2500 |  627.54 KB |        0.41 |
| GpuLatestOnly     | Skender.GetUltimate  | 10000 |   688.546 Î¼s |  5.9868 Î¼s |  3.5626 Î¼s |  0.60 |    0.01 |  15.6250 |   3.9063 |  157.69 KB |        0.10 |
| NativeSingle      | Skender.GetUltimate  | 10000 | 1,156.348 Î¼s | 38.7071 Î¼s | 23.0339 Î¼s |  1.00 |    0.03 | 179.6875 | 109.3750 | 1523.99 KB |        1.00 |
| NativeParallel    | Skender.GetUltimate  | 10000 |   425.890 Î¼s | 16.5246 Î¼s | 10.9300 Î¼s |  0.37 |    0.01 | 203.1250 | 144.5313 | 1557.44 KB |        1.02 |
|                   |                      |       |              |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib(...)ltOsc [22]** | **1000**  |    **24.432 Î¼s** |  **2.0096 Î¼s** |  **1.1959 Î¼s** |  **1.41** |    **0.07** |   **7.8125** |   **1.2207** |   **65.42 KB** |        **8.54** |
| BuilderLatestOnly | TaLib(...)ltOsc [22] | 1000  |    20.556 Î¼s |  0.8321 Î¼s |  0.4952 Î¼s |  1.19 |    0.03 |   2.0752 |   0.1221 |   17.81 KB |        2.33 |
| GpuFull           | TaLib(...)ltOsc [22] | 1000  |   383.489 Î¼s | 14.4153 Î¼s |  7.5395 Î¼s | 22.13 |    0.55 |   7.8125 |        - |   64.64 KB |        8.44 |
| GpuLatestOnly     | TaLib(...)ltOsc [22] | 1000  |   344.549 Î¼s |  8.8132 Î¼s |  5.8294 Î¼s | 19.88 |    0.46 |   1.9531 |        - |   17.03 KB |        2.22 |
| NativeSingle      | TaLib(...)ltOsc [22] | 1000  |    17.332 Î¼s |  0.4580 Î¼s |  0.3029 Î¼s |  1.00 |    0.02 |   0.8545 |        - |    7.66 KB |        1.00 |
| NativeParallel    | TaLib(...)ltOsc [22] | 1000  |     6.630 Î¼s |  0.5022 Î¼s |  0.3322 Î¼s |  0.38 |    0.02 |   0.9766 |        - |    8.09 KB |        1.06 |
|                   |                      |       |              |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib(...)ltOsc [22]** | **10000** |   **163.854 Î¼s** |  **3.5808 Î¼s** |  **2.3684 Î¼s** |  **0.96** |    **0.03** |  **76.1719** |  **37.1094** |   **628.3 KB** |        **8.06** |
| BuilderLatestOnly | TaLib(...)ltOsc [22] | 10000 |   139.815 Î¼s |  7.6702 Î¼s |  4.5644 Î¼s |  0.82 |    0.03 |  18.5547 |   5.8594 |  158.45 KB |        2.03 |
| GpuFull           | TaLib(...)ltOsc [22] | 10000 |   779.431 Î¼s | 16.6689 Î¼s | 11.0255 Î¼s |  4.58 |    0.12 |  70.3125 |  23.4375 |  627.58 KB |        8.05 |
| GpuLatestOnly     | TaLib(...)ltOsc [22] | 10000 |   718.110 Î¼s | 33.4388 Î¼s | 19.8989 Î¼s |  4.22 |    0.15 |  15.6250 |   3.9063 |  157.69 KB |        2.02 |
| NativeSingle      | TaLib(...)ltOsc [22] | 10000 |   170.321 Î¼s |  6.2050 Î¼s |  4.1042 Î¼s |  1.00 |    0.03 |   8.7891 |   0.9766 |   77.98 KB |        1.00 |
| NativeParallel    | TaLib(...)ltOsc [22] | 10000 |    44.120 Î¼s |  0.4921 Î¼s |  0.3255 Î¼s |  0.26 |    0.01 |   9.5215 |   1.7090 |    78.4 KB |        1.01 |
