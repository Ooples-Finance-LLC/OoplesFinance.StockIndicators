```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean      | Error      | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated  | Alloc Ratio |
|----------- |------ |--------------- |----------:|-----------:|----------:|------:|--------:|----------:|----------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetPvo** | **20.472 ms** | **12.6937 ms** | **0.6958 ms** |  **1.00** |    **0.04** |         **-** |         **-** |    **6638 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetPvo |  1.040 ms |  0.9431 ms | 0.0517 ms |  0.05 |    0.00 |         - |         - |  373.29 KB |        0.06 |
|            |       |                |           |            |           |       |         |           |           |            |             |
| **Ooples**     | **10000** | **Skender.GetPvo** | **61.521 ms** | **95.7872 ms** | **5.2504 ms** |  **1.00** |    **0.10** | **8000.0000** | **1000.0000** | **68728.5 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetPvo |  1.246 ms |  1.1176 ms | 0.0613 ms |  0.02 |    0.00 |         - |         - | 3868.41 KB |        0.06 |
