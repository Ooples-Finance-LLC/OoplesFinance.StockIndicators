```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error      | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |----------:|-----------:|----------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skend(...)ivots [24]** | **12.765 ms** |  **8.7267 ms** | **0.4783 ms** |  **1.00** |    **0.05** |         **-** |         **-** |  **6336.36 KB** |        **1.00** |
| Competitor | 1000  | Skend(...)ivots [24] |  2.214 ms |  0.0701 ms | 0.0038 ms |  0.17 |    0.01 |         - |         - |   735.21 KB |        0.12 |
|            |       |                      |           |            |           |       |         |           |           |             |             |
| **Ooples**     | **10000** | **Skend(...)ivots [24]** | **48.254 ms** | **27.2453 ms** | **1.4934 ms** |  **1.00** |    **0.04** | **7000.0000** | **1000.0000** | **62782.77 KB** |        **1.00** |
| Competitor | 10000 | Skend(...)ivots [24] | 13.104 ms |  9.1016 ms | 0.4989 ms |  0.27 |    0.01 |         - |         - |     7314 KB |        0.12 |
