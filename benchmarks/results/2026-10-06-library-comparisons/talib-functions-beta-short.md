```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean         | Error       | StdDev    | Ratio | RatioSD | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|------------:|----------:|------:|--------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Beta** |  **31,458.1 μs** | **18,041.6 μs** | **988.92 μs** | **1.001** |    **0.04** |  **1000.0000** |         **-** |  **10918.54 KB** |       **1.000** |
| Competitor | 1000  | TaLib.Functions.Beta |     172.2 μs |    471.5 μs |  25.84 μs | 0.005 |    0.00 |          - |         - |     39.17 KB |       0.004 |
|            |       |                      |              |             |           |       |         |            |           |              |             |
| **Ooples**     | **10000** | **TaLib.Functions.Beta** | **130,284.4 μs** |  **6,982.4 μs** | **382.73 μs** | **1.000** |    **0.00** | **13000.0000** | **1000.0000** | **112737.75 KB** |       **1.000** |
| Competitor | 10000 | TaLib.Functions.Beta |     231.8 μs |    785.3 μs |  43.05 μs | 0.002 |    0.00 |          - |         - |    329.54 KB |       0.003 |
