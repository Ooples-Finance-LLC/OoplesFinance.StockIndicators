```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean        | Error      | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |------------:|-----------:|----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)gStar [25]** |   **251.88 μs** | **184.392 μs** | **10.107 μs** |  **1.00** |    **0.05** |  **33.2031** |   **8.3008** |        **-** |  **273.57 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)gStar [25] |    74.16 μs |   8.312 μs |  0.456 μs |  0.29 |    0.01 |   1.4648 |        - |        - |   12.08 KB |        0.04 |
|            |       |                      |             |            |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)gStar [25]** | **2,749.43 μs** | **507.890 μs** | **27.839 μs** |  **1.00** |    **0.01** | **683.5938** | **585.9375** | **496.0938** | **3768.22 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)gStar [25] |   796.82 μs | 207.825 μs | 11.392 μs |  0.29 |    0.00 |  13.6719 |        - |        - |  117.55 KB |        0.03 |
