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
| **Ooples**     | **1000**  | **Skend(...)yStop [25]** | **12.447 ms** | **13.260 ms** | **0.7268 ms** |  **1.00** |    **0.07** |         **-** |         **-** |  **4298.45 KB** |        **1.00** |
| Competitor | 1000  | Skend(...)yStop [25] |  1.434 ms |  1.043 ms | 0.0572 ms |  0.12 |    0.01 |         - |         - |   402.99 KB |        0.09 |
|            |       |                      |           |           |           |       |         |           |           |             |             |
| **Ooples**     | **10000** | **Skend(...)yStop [25]** | **61.522 ms** | **40.815 ms** | **2.2372 ms** |  **1.00** |    **0.04** | **5000.0000** | **1000.0000** | **43503.86 KB** |        **1.00** |
| Competitor | 10000 | Skend(...)yStop [25] |  2.863 ms | 11.213 ms | 0.6146 ms |  0.05 |    0.01 |         - |         - |  3953.77 KB |        0.09 |
