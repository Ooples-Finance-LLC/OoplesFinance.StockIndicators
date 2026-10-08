```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Gen0      | Allocated   | Alloc Ratio |
|----------- |------ |--------------- |----------:|----------:|----------:|----------:|------:|--------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Mode** |  **3.009 ms** |  **2.142 ms** | **0.1174 ms** |  **2.999 ms** |  **1.00** |    **0.05** |         **-** |   **267.25 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Mode | 10.444 ms | 11.220 ms | 0.6150 ms | 10.195 ms |  3.47 |    0.21 |         - |  3519.23 KB |       13.17 |
|            |       |                |           |           |           |           |       |         |           |             |             |
| **Ooples**     | **10000** | **QuanTAlib.Mode** |  **7.427 ms** | **60.965 ms** | **3.3417 ms** |  **5.584 ms** |  **1.12** |    **0.57** |         **-** |  **3715.66 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Mode | 23.286 ms | 57.873 ms | 3.1722 ms | 22.938 ms |  3.51 |    1.17 | 4000.0000 | 36087.34 KB |        9.71 |
