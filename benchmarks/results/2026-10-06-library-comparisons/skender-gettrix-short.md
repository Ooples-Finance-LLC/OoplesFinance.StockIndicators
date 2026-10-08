```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId          | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |---------------- |------------:|------------:|----------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetTrix** | **14,884.6 μs** | **17,885.3 μs** | **980.35 μs** |  **1.00** |    **0.08** |         **-** |         **-** |  **5181.31 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetTrix |    969.8 μs |    150.6 μs |   8.26 μs |  0.07 |    0.00 |         - |         - |   201.68 KB |        0.04 |
|            |       |                 |             |             |           |       |         |           |           |             |             |
| **Ooples**     | **10000** | **Skender.GetTrix** | **37,649.2 μs** |  **7,426.5 μs** | **407.07 μs** |  **1.00** |    **0.01** | **6000.0000** | **1000.0000** | **53650.15 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetTrix |  1,270.5 μs |  3,574.5 μs | 195.93 μs |  0.03 |    0.00 |         - |         - |  1951.03 KB |        0.04 |
