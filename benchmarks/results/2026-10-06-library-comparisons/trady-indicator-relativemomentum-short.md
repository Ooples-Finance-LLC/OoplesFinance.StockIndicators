```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error      | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Gen2      | Allocated | Alloc Ratio |
|----------- |------ |--------------------- |----------:|-----------:|----------:|------:|--------:|----------:|----------:|----------:|----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)entum [32]** |  **8.331 ms** |   **8.708 ms** | **0.4773 ms** |  **1.00** |    **0.07** |         **-** |         **-** |         **-** |   **2.84 MB** |        **1.00** |
| Competitor | 1000  | Trady(...)entum [32] |  7.755 ms |   1.684 ms | 0.0923 ms |  0.93 |    0.05 |         - |         - |         - |   3.74 MB |        1.32 |
|            |       |                      |           |            |           |       |         |           |           |           |           |             |
| **Ooples**     | **10000** | **Trady(...)entum [32]** | **31.308 ms** |  **19.926 ms** | **1.0922 ms** |  **1.00** |    **0.04** | **3000.0000** | **1000.0000** |         **-** |   **29.8 MB** |        **1.00** |
| Competitor | 10000 | Trady(...)entum [32] | 37.014 ms | 118.341 ms | 6.4867 ms |  1.18 |    0.18 | 2000.0000 | 1000.0000 | 1000.0000 |  26.37 MB |        0.89 |
