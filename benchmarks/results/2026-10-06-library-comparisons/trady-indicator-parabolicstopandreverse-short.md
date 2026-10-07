```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |----------:|----------:|----------:|------:|--------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)verse [39]** | **10.701 ms** | **12.340 ms** | **0.6764 ms** |  **1.00** |    **0.08** |         **-** |  **3296.97 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)verse [39] |  2.944 ms |  1.469 ms | 0.0805 ms |  0.28 |    0.02 |         - |   934.27 KB |        0.28 |
|            |       |                      |           |           |           |       |         |           |             |             |
| **Ooples**     | **10000** | **Trady(...)verse [39]** | **57.750 ms** | **35.562 ms** | **1.9493 ms** |  **1.00** |    **0.04** | **4000.0000** | **33485.55 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)verse [39] |  9.374 ms | 11.718 ms | 0.6423 ms |  0.16 |    0.01 |         - |  8495.38 KB |        0.25 |
