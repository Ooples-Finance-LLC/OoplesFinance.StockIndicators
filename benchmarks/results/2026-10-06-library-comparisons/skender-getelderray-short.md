```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId              | Mean     | Error    | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |-------------------- |---------:|---------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetElderRay** | **2.875 ms** | **2.825 ms** | **0.1549 ms** |  **1.00** |    **0.07** |  **386.58 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetElderRay | 1.194 ms | 1.705 ms | 0.0935 ms |  0.42 |    0.03 |  338.31 KB |        0.88 |
|            |       |                     |          |          |           |       |         |            |             |
| **Ooples**     | **10000** | **Skender.GetElderRay** | **4.285 ms** | **4.676 ms** | **0.2563 ms** |  **1.00** |    **0.07** | **4915.01 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetElderRay | 2.059 ms | 7.660 ms | 0.4199 ms |  0.48 |    0.09 |  3317.8 KB |        0.68 |
