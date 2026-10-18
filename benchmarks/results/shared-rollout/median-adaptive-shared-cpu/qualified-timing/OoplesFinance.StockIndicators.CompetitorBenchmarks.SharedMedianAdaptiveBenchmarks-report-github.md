```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation      | Count | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0       | Gen1     | Allocated    | Alloc Ratio |
|------------------ |--------------- |------ |----------:|----------:|----------:|------:|--------:|-----------:|---------:|-------------:|------------:|
| **BuilderFull**       | **QuanTAlib.Maaf** | **1000**  |  **1.261 ms** | **0.0566 ms** | **0.0374 ms** |  **0.54** |    **0.02** |          **-** |        **-** |     **58.29 KB** |       **0.017** |
| BuilderLatestOnly | QuanTAlib.Maaf | 1000  |  1.204 ms | 0.0170 ms | 0.0089 ms |  0.52 |    0.01 |          - |        - |     10.36 KB |       0.003 |
| NativeSingle      | QuanTAlib.Maaf | 1000  |  2.333 ms | 0.0342 ms | 0.0226 ms |  1.00 |    0.01 |   406.2500 |        - |   3385.86 KB |       1.000 |
| NativeParallel    | QuanTAlib.Maaf | 1000  |  3.633 ms | 0.0638 ms | 0.0422 ms |  1.56 |    0.02 |  1875.0000 |  62.5000 |  15306.02 KB |       4.521 |
|                   |                |       |           |           |           |       |         |            |          |              |             |
| **BuilderFull**       | **QuanTAlib.Maaf** | **10000** | **13.038 ms** | **1.1724 ms** | **0.7755 ms** |  **0.59** |    **0.04** |          **-** |        **-** |    **551.75 KB** |       **0.016** |
| BuilderLatestOnly | QuanTAlib.Maaf | 10000 | 12.236 ms | 0.2024 ms | 0.1204 ms |  0.55 |    0.01 |          - |        - |     81.55 KB |       0.002 |
| NativeSingle      | QuanTAlib.Maaf | 10000 | 22.192 ms | 0.7224 ms | 0.4299 ms |  1.00 |    0.03 |  4000.0000 | 200.0000 |  33630.24 KB |       1.000 |
| NativeParallel    | QuanTAlib.Maaf | 10000 | 35.246 ms | 1.8162 ms | 1.2013 ms |  1.59 |    0.06 | 18500.0000 | 500.0000 | 151166.19 KB |       4.495 |
