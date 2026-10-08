```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0      | Gen1      | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |------------:|------------:|------------:|------:|--------:|----------:|----------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)cdExt [23]** | **17,555.5 μs** | **20,578.7 μs** | **1,127.99 μs** |  **1.00** |    **0.08** |         **-** |         **-** | **4664.79 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)cdExt [23] |    268.2 μs |    427.8 μs |    23.45 μs |  0.02 |    0.00 |         - |         - |  119.95 KB |        0.03 |
|            |       |                      |             |             |             |       |         |           |           |            |             |
| **Ooples**     | **10000** | **TaLib(...)cdExt [23]** | **47,364.4 μs** | **63,162.2 μs** | **3,462.14 μs** | **1.004** |    **0.09** | **5000.0000** | **1000.0000** | **48543.8 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)cdExt [23] |    360.4 μs |  2,485.4 μs |   136.24 μs | 0.008 |    0.00 |         - |         - | 1130.97 KB |        0.02 |
