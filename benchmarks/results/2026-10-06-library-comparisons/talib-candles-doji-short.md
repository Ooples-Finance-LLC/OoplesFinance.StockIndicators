```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId             | Mean        | Error       | StdDev      | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |------------------- |------------:|------------:|------------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Candles.Doji** |  **2,094.8 μs** |  **2,962.8 μs** |   **162.40 μs** |  **1.00** |    **0.09** |  **265.12 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Candles.Doji |    497.4 μs |    276.2 μs |    15.14 μs |  0.24 |    0.02 |   16.34 KB |        0.06 |
|            |       |                    |             |             |             |       |         |            |             |
| **Ooples**     | **10000** | **TaLib.Candles.Doji** | **12,681.8 μs** | **39,924.7 μs** | **2,188.41 μs** |  **1.02** |    **0.23** | **3712.82 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Candles.Doji |    392.5 μs |    433.2 μs |    23.74 μs |  0.03 |    0.01 |  122.75 KB |        0.03 |
