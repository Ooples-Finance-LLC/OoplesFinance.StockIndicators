```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean         | Error      | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|-----------:|-----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)thods [39]** |   **187.369 μs** | **449.014 μs** | **24.6120 μs** |  **1.01** |    **0.16** |  **32.2266** |   **8.0566** |        **-** |  **264.85 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)thods [39] |     5.598 μs |   5.130 μs |  0.2812 μs |  0.03 |    0.00 |   1.4725 |   0.0305 |        - |   12.08 KB |        0.05 |
|            |       |                      |              |            |            |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)thods [39]** | **1,882.383 μs** | **288.164 μs** | **15.7952 μs** |  **1.00** |    **0.01** | **681.6406** | **570.3125** | **498.0469** | **3713.24 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)thods [39] |   101.037 μs |  36.992 μs |  2.0276 μs |  0.05 |    0.00 |  14.2822 |        - |        - |  117.55 KB |        0.03 |
