```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |----------:|----------:|----------:|----------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)Aroon [21]** |  **2.698 ms** |  **3.416 ms** | **0.1872 ms** |  **2.598 ms** |  **1.00** |    **0.08** |         **-** |         **-** |   **518.25 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)Aroon [21] | 11.043 ms |  7.286 ms | 0.3994 ms | 10.827 ms |  4.11 |    0.27 |         - |         - |  6509.55 KB |       12.56 |
|            |       |                      |           |           |           |           |       |         |           |           |             |             |
| **Ooples**     | **10000** | **Trady(...)Aroon [21]** |  **8.226 ms** | **81.974 ms** | **4.4932 ms** |  **5.645 ms** |  **1.18** |    **0.73** |         **-** |         **-** |  **6233.86 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)Aroon [21] | 30.333 ms | 44.921 ms | 2.4623 ms | 30.672 ms |  4.34 |    1.60 | 5000.0000 | 1000.0000 | 50273.49 KB |        8.06 |
