```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId              | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0      | Allocated   | Alloc Ratio |
|----------- |------ |-------------------- |----------:|----------:|----------:|------:|--------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetRenkoAtr** |  **5.101 ms** | **10.488 ms** | **0.5749 ms** |  **1.01** |    **0.14** |         **-** |  **2450.86 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetRenkoAtr |  1.244 ms |  1.084 ms | 0.0594 ms |  0.25 |    0.03 |         - |   222.55 KB |        0.09 |
|            |       |                     |           |           |           |       |         |           |             |             |
| **Ooples**     | **10000** | **Skender.GetRenkoAtr** | **19.552 ms** | **76.255 ms** | **4.1798 ms** |  **1.03** |    **0.29** | **3000.0000** | **24522.67 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetRenkoAtr |  2.577 ms | 10.348 ms | 0.5672 ms |  0.14 |    0.04 |         - |  2147.28 KB |        0.09 |
