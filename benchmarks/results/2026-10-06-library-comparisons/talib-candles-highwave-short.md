```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean       | Error      | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|-----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)hWave [22]** | **2,759.6 μs** | **1,628.4 μs** |  **89.26 μs** |  **1.00** |    **0.04** |  **266.16 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)hWave [22] |   979.1 μs |   168.8 μs |   9.25 μs |  0.36 |    0.01 |   17.61 KB |        0.07 |
|            |       |                      |            |            |           |       |         |            |             |
| **Ooples**     | **10000** | **TaLib(...)hWave [22]** | **5,019.6 μs** | **6,923.6 μs** | **379.51 μs** |  **1.00** |    **0.09** | **3712.88 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)hWave [22] |   987.3 μs | 3,709.8 μs | 203.34 μs |  0.20 |    0.04 |  121.77 KB |        0.03 |
