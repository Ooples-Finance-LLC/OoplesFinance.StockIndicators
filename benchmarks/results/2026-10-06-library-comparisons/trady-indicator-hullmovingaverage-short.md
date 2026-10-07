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
| **Ooples**     | **1000**  | **Trady(...)erage [33]** |  **3.320 ms** | **10.431 ms** | **0.5718 ms** |  **1.02** |    **0.21** |         **-** |   **307.84 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)erage [33] | 12.845 ms |  1.729 ms | 0.0948 ms |  3.94 |    0.55 |         - |  2947.39 KB |        9.57 |
|            |       |                      |           |           |           |       |         |           |             |             |
| **Ooples**     | **10000** | **Trady(...)erage [33]** |  **7.744 ms** |  **3.542 ms** | **0.1941 ms** |  **1.00** |    **0.03** |         **-** |  **4115.94 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)erage [33] | 42.393 ms | 21.423 ms | 1.1743 ms |  5.48 |    0.18 | 1000.0000 | 18790.49 KB |        4.57 |
