```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean     | Error     | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |---------:|----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetAlligator** | **3.200 ms** | **0.6163 ms** | **0.0338 ms** |  **1.00** |    **0.01** |  **420.34 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetAlligator | 1.001 ms | 0.3701 ms | 0.0203 ms |  0.31 |    0.01 |  209.97 KB |        0.50 |
|            |       |                      |          |           |           |       |         |            |             |
| **Ooples**     | **10000** | **Skender.GetAlligator** | **7.285 ms** | **0.4810 ms** | **0.0264 ms** |  **1.00** |    **0.00** | **5227.45 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetAlligator | 1.775 ms | 4.1009 ms | 0.2248 ms |  0.24 |    0.03 |  2025.7 KB |        0.39 |
