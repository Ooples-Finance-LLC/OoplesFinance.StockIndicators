```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0      | Gen1      | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |------------:|-------------:|------------:|------:|--------:|----------:|----------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Skend(...)icSar [23]** | **11,819.6 μs** |  **17,302.3 μs** |   **948.39 μs** |  **1.00** |    **0.10** |         **-** |         **-** | **3805.62 KB** |        **1.00** |
| Competitor | 1000  | Skend(...)icSar [23] |    732.7 μs |     269.8 μs |    14.79 μs |  0.06 |    0.00 |         - |         - |  208.41 KB |        0.05 |
|            |       |                      |             |              |             |       |         |           |           |            |             |
| **Ooples**     | **10000** | **Skend(...)icSar [23]** | **65,425.6 μs** | **175,554.7 μs** | **9,622.74 μs** |  **1.02** |    **0.19** | **4000.0000** | **1000.0000** | **38249.2 KB** |        **1.00** |
| Competitor | 10000 | Skend(...)icSar [23] |  1,713.7 μs |     604.4 μs |    33.13 μs |  0.03 |    0.00 |         - |         - | 2018.91 KB |        0.05 |
