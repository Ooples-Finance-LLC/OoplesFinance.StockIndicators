```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean     | Error      | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |---------:|-----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)gLine [22]** | **2.673 ms** |  **0.4945 ms** | **0.0271 ms** |  **1.00** |    **0.01** |  **266.16 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)gLine [22] | 1.015 ms |  0.3213 ms | 0.0176 ms |  0.38 |    0.01 |    16.3 KB |        0.06 |
|            |       |                      |          |            |           |       |         |            |             |
| **Ooples**     | **10000** | **TaLib(...)gLine [22]** | **4.990 ms** | **11.0739 ms** | **0.6070 ms** |  **1.01** |    **0.15** | **3713.54 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)gLine [22] | 1.021 ms |  2.2432 ms | 0.1230 ms |  0.21 |    0.03 |  122.75 KB |        0.03 |
