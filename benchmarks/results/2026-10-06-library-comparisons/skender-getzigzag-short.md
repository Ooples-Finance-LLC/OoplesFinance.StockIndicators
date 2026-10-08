```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId            | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |------------------ |----------:|----------:|----------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetZigZag** | **15.606 ms** | **45.571 ms** | **2.4979 ms** |  **1.02** |    **0.20** |         **-** |         **-** |  **3793.51 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetZigZag |  1.816 ms |  1.280 ms | 0.0702 ms |  0.12 |    0.02 |         - |         - |   428.19 KB |        0.11 |
|            |       |                   |           |           |           |       |         |           |           |             |             |
| **Ooples**     | **10000** | **Skender.GetZigZag** | **69.779 ms** | **53.101 ms** | **2.9107 ms** |  **1.00** |    **0.05** | **4000.0000** | **1000.0000** | **39160.98 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetZigZag |  3.554 ms |  4.842 ms | 0.2654 ms |  0.05 |    0.00 |         - |         - |  4087.81 KB |        0.10 |
