```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean         | Error        | StdDev      | Ratio | RatioSD | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |--------------- |-------------:|-------------:|------------:|------:|--------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetAdx** |  **36,894.5 μs** |  **18,476.6 μs** | **1,012.77 μs** |  **1.00** |    **0.03** |  **1000.0000** |         **-** |  **14174.05 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetAdx |     932.6 μs |     683.6 μs |    37.47 μs |  0.03 |    0.00 |          - |         - |     333.3 KB |        0.02 |
|            |       |                |              |              |             |       |         |            |           |              |             |
| **Ooples**     | **10000** | **Skender.GetAdx** | **102,662.9 μs** | **181,260.4 μs** | **9,935.49 μs** |  **1.01** |    **0.12** | **17000.0000** | **1000.0000** | **144899.35 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetAdx |   1,426.9 μs |   7,135.5 μs |   391.12 μs |  0.01 |    0.00 |          - |         - |    3299.1 KB |        0.02 |
