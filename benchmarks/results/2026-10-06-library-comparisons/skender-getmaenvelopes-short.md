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
| **Ooples**     | **1000**  | **Skend(...)lopes [22]** | **3.572 ms** | **2.8386 ms** | **0.1556 ms** |  **1.00** |    **0.05** |  **387.79 KB** |        **1.00** |
| Competitor | 1000  | Skend(...)lopes [22] | 1.086 ms | 0.6858 ms | 0.0376 ms |  0.30 |    0.01 |  283.92 KB |        0.73 |
|            |       |                      |          |           |           |       |         |            |             |
| **Ooples**     | **10000** | **Skend(...)lopes [22]** | **6.983 ms** | **4.8248 ms** | **0.2645 ms** |  **1.00** |    **0.05** | **4917.86 KB** |        **1.00** |
| Competitor | 10000 | Skend(...)lopes [22] | 1.737 ms | 4.4699 ms | 0.2450 ms |  0.25 |    0.03 | 2771.88 KB |        0.56 |
