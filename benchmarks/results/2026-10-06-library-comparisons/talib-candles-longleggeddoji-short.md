```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean        | Error        | StdDev      | Median      | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |------------:|-------------:|------------:|------------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)dDoji [28]** |  **2,315.3 μs** |   **2,668.6 μs** |   **146.28 μs** |  **2,259.0 μs** |  **1.00** |    **0.08** |  **265.12 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)dDoji [28] |    780.8 μs |     247.3 μs |    13.55 μs |    773.6 μs |  0.34 |    0.02 |   15.64 KB |        0.06 |
|            |       |                      |             |              |             |             |       |         |            |             |
| **Ooples**     | **10000** | **TaLib(...)dDoji [28]** | **12,624.1 μs** | **126,055.7 μs** | **6,909.54 μs** | **15,452.2 μs** |  **1.40** |    **1.23** | **3711.88 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)dDoji [28] |    611.5 μs |     287.7 μs |    15.77 μs |    607.3 μs |  0.07 |    0.05 |   122.8 KB |        0.03 |
