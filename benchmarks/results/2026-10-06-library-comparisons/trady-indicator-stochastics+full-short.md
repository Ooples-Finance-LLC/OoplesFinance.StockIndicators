```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error       | StdDev     | Median    | Ratio | RatioSD | Gen0      | Gen1      | Gen2      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |----------:|------------:|-----------:|----------:|------:|--------:|----------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)+Full [32]** |  **4.896 ms** |   **4.3678 ms** |  **0.2394 ms** |  **4.765 ms** |  **1.00** |    **0.06** |         **-** |         **-** |         **-** |   **817.22 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)+Full [32] |  7.637 ms |   0.1470 ms |  0.0081 ms |  7.636 ms |  1.56 |    0.06 |         - |         - |         - |  4474.36 KB |        5.48 |
|            |       |                      |           |             |            |           |       |         |           |           |           |             |             |
| **Ooples**     | **10000** | **Trady(...)+Full [32]** | **23.111 ms** | **236.9277 ms** | **12.9868 ms** | **16.997 ms** |  **1.19** |    **0.77** |         **-** |         **-** |         **-** |  **9254.73 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)+Full [32] | 27.785 ms |  61.2995 ms |  3.3600 ms | 25.926 ms |  1.44 |    0.57 | 3000.0000 | 2000.0000 | 1000.0000 | 36349.98 KB |        3.93 |
