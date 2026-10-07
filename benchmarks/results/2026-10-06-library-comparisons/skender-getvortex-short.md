```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId            | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |------------------ |------------:|------------:|------------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetVortex** | **16,655.4 μs** | **19,275.2 μs** | **1,056.54 μs** |  **1.00** |    **0.08** |         **-** |         **-** |  **5462.52 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetVortex |    577.2 μs |    741.1 μs |    40.62 μs |  0.03 |    0.00 |         - |         - |   239.91 KB |        0.04 |
|            |       |                   |             |             |             |       |         |           |           |             |             |
| **Ooples**     | **10000** | **Skender.GetVortex** | **36,899.6 μs** | **33,529.3 μs** | **1,837.86 μs** |  **1.00** |    **0.06** | **6000.0000** | **1000.0000** | **56146.79 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetVortex |  1,264.6 μs |  9,055.9 μs |   496.39 μs |  0.03 |    0.01 |         - |         - |  2331.66 KB |        0.04 |
