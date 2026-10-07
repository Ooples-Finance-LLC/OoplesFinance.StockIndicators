```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |----------:|----------:|----------:|------:|--------:|----------:|----------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)ntile [26]** |  **4.659 ms** |  **2.430 ms** | **0.1332 ms** |  **1.00** |    **0.03** |         **-** |         **-** |  **461.23 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)ntile [26] | 10.069 ms |  1.053 ms | 0.0577 ms |  2.16 |    0.05 |         - |         - | 2625.95 KB |        5.69 |
|            |       |                      |           |           |           |       |         |           |           |            |             |
| **Ooples**     | **10000** | **Trady(...)ntile [26]** | **12.718 ms** | **68.164 ms** | **3.7363 ms** |  **1.05** |    **0.37** |         **-** |         **-** | **5667.06 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)ntile [26] | 21.338 ms | 11.316 ms | 0.6203 ms |  1.77 |    0.41 | 2000.0000 | 1000.0000 |   25794 KB |        4.55 |
