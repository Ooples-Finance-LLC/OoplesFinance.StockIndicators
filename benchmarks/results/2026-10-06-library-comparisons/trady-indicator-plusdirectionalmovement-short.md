```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |----------:|----------:|----------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)ement [39]** |  **5.902 ms** | **10.129 ms** | **0.5552 ms** |  **1.01** |    **0.12** |         **-** |         **-** |   **1835.2 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)ement [39] |  2.328 ms |  8.013 ms | 0.4392 ms |  0.40 |    0.07 |         - |         - |   745.98 KB |        0.41 |
|            |       |                      |           |           |           |       |         |           |           |             |             |
| **Ooples**     | **10000** | **Trady(...)ement [39]** | **17.046 ms** | **17.570 ms** | **0.9631 ms** |  **1.00** |    **0.07** | **2000.0000** | **1000.0000** | **19426.95 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)ement [39] |  5.086 ms |  2.326 ms | 0.1275 ms |  0.30 |    0.02 |         - |         - |  6621.88 KB |        0.34 |
