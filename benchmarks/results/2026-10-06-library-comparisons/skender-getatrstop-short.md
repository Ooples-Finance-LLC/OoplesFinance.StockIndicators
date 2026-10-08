```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId             | Mean      | Error      | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |------------------- |----------:|-----------:|----------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetAtrStop** | **18.776 ms** | **11.4961 ms** | **0.6301 ms** |  **1.00** |    **0.04** |         **-** |         **-** |  **6023.97 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetAtrStop |  1.067 ms |  0.9011 ms | 0.0494 ms |  0.06 |    0.00 |         - |         - |   358.56 KB |        0.06 |
|            |       |                    |           |            |           |       |         |           |           |             |             |
| **Ooples**     | **10000** | **Skender.GetAtrStop** | **56.363 ms** | **65.7999 ms** | **3.6067 ms** |  **1.00** |    **0.08** | **7000.0000** | **1000.0000** | **61991.29 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetAtrStop |  2.690 ms |  0.3069 ms | 0.0168 ms |  0.05 |    0.00 |         - |         - |  3512.52 KB |        0.06 |
