```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean     | Error       | StdDev    | Median    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |---------:|------------:|----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)gStar [26]** | **2.511 ms** |   **1.3961 ms** | **0.0765 ms** | **2.4700 ms** |  **1.00** |    **0.04** |  **266.38 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)gStar [26] | 1.224 ms |   0.2560 ms | 0.0140 ms | 1.2234 ms |  0.49 |    0.01 |   17.28 KB |        0.06 |
|            |       |                      |          |             |           |           |       |         |            |             |
| **Ooples**     | **10000** | **TaLib(...)gStar [26]** | **7.323 ms** | **106.6929 ms** | **5.8482 ms** | **4.0064 ms** |  **1.41** |    **1.26** | **3713.48 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)gStar [26] | 1.086 ms |   3.0096 ms | 0.1650 ms | 0.9962 ms |  0.21 |    0.10 |  122.75 KB |        0.03 |
