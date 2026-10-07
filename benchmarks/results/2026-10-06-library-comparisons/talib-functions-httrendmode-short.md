```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0        | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|-----------:|----------:|------:|--------:|------------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)dMode [27]** |   **134.181 ms** | **35.9700 ms** | **1.9716 ms** |  **1.00** |    **0.02** |  **10000.0000** | **1000.0000** | **82179.77 KB** |       **1.000** |
| Competitor | 1000  | TaLib(...)dMode [27] |     1.525 ms |  0.4479 ms | 0.0246 ms |  0.01 |    0.00 |           - |         - |    35.93 KB |       0.000 |
|            |       |                      |              |            |           |       |         |             |           |             |             |
| **Ooples**     | **10000** | **TaLib(...)dMode [27]** | **1,368.954 ms** | **93.1958 ms** | **5.1084 ms** | **1.000** |    **0.00** | **105000.0000** | **1000.0000** | **864969.6 KB** |       **1.000** |
| Competitor | 10000 | TaLib(...)dMode [27] |     6.634 ms |  0.5026 ms | 0.0276 ms | 0.005 |    0.00 |           - |         - |   290.44 KB |       0.000 |
