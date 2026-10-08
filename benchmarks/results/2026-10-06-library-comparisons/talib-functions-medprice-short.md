```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean         | Error        | StdDev     | Median      | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|-------------:|-----------:|------------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)Price [24]** |  **1,851.37 μs** |  **1,692.87 μs** |  **92.792 μs** | **1,884.30 μs** |  **1.00** |    **0.06** |  **377.22 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)Price [24] |     24.97 μs |     67.51 μs |   3.700 μs |    26.90 μs |  0.01 |    0.00 |   21.88 KB |        0.06 |
|            |       |                      |              |              |            |             |       |         |            |             |
| **Ooples**     | **10000** | **TaLib(...)Price [24]** | **10,055.07 μs** | **10,715.75 μs** | **587.367 μs** | **9,796.90 μs** |  **1.00** |    **0.07** | **4809.34 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)Price [24] |    288.33 μs |  6,853.28 μs | 375.651 μs |    82.90 μs |  0.03 |    0.03 |  162.17 KB |        0.03 |
