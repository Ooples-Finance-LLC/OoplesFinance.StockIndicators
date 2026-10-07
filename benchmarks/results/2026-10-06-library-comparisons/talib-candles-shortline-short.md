```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |------------:|-----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)tLine [23]** |  **2,662.3 μs** | **2,310.9 μs** | **126.67 μs** |  **1.00** |    **0.06** |  **266.16 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)tLine [23] |  1,058.8 μs | 1,220.8 μs |  66.92 μs |  0.40 |    0.03 |   17.28 KB |        0.06 |
|            |       |                      |             |            |           |       |         |            |             |
| **Ooples**     | **10000** | **TaLib(...)tLine [23]** | **22,844.7 μs** | **8,458.7 μs** | **463.65 μs** |  **1.00** |    **0.02** | **3713.91 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)tLine [23] |    966.8 μs |   511.4 μs |  28.03 μs |  0.04 |    0.00 |  122.75 KB |        0.03 |
