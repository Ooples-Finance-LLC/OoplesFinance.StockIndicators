```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error      | StdDev    | Median    | Ratio | RatioSD | Gen0      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |----------:|-----------:|----------:|----------:|------:|--------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)+Fast [32]** |  **4.935 ms** |   **4.277 ms** | **0.2344 ms** |  **4.814 ms** |  **1.00** |    **0.06** |         **-** |   **817.22 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)+Fast [32] |  7.030 ms |   1.248 ms | 0.0684 ms |  7.016 ms |  1.43 |    0.06 |         - |  2945.78 KB |        3.60 |
|            |       |                      |           |            |           |           |       |         |           |             |             |
| **Ooples**     | **10000** | **Trady(...)+Fast [32]** | **23.280 ms** | **174.676 ms** | **9.5746 ms** | **18.363 ms** |  **1.10** |    **0.52** |         **-** |  **9256.37 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)+Fast [32] | 22.122 ms |  31.040 ms | 1.7014 ms | 22.684 ms |  1.05 |    0.31 | 1000.0000 | 23798.67 KB |        2.57 |
