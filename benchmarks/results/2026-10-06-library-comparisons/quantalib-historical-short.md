```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|----------:|----------:|------:|--------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Historical** |  **58.673 ms** | **33.394 ms** | **1.8304 ms** |  **1.00** |    **0.04** |  **4000.0000** |         **-** |  **36127.06 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Historical |   2.393 ms |  8.477 ms | 0.4647 ms |  0.04 |    0.01 |          - |         - |    529.56 KB |        0.01 |
|            |       |                      |            |           |           |       |         |            |           |              |             |
| **Ooples**     | **10000** | **QuanTAlib.Historical** | **356.973 ms** | **84.820 ms** | **4.6493 ms** |  **1.00** |    **0.02** | **44000.0000** | **1000.0000** | **363966.07 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Historical |   6.209 ms | 16.095 ms | 0.8822 ms |  0.02 |    0.00 |          - |         - |   4373.17 KB |        0.01 |
