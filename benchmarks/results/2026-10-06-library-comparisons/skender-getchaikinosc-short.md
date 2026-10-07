```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0       | Gen1      | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |----------:|----------:|----------:|------:|--------:|-----------:|----------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Skend(...)inOsc [21]** | **28.181 ms** | **22.214 ms** | **1.2176 ms** |  **1.00** |    **0.05** |  **1000.0000** |         **-** | **8475.07 KB** |        **1.00** |
| Competitor | 1000  | Skend(...)inOsc [21] |  1.063 ms |  1.315 ms | 0.0721 ms |  0.04 |    0.00 |          - |         - |  492.89 KB |        0.06 |
|            |       |                      |           |           |           |       |         |            |           |            |             |
| **Ooples**     | **10000** | **Skend(...)inOsc [21]** | **84.411 ms** | **78.790 ms** | **4.3188 ms** |  **1.00** |    **0.06** | **10000.0000** | **1000.0000** | **86467.7 KB** |        **1.00** |
| Competitor | 10000 | Skend(...)inOsc [21] |  2.284 ms | 13.519 ms | 0.7410 ms |  0.03 |    0.01 |          - |         - | 4852.27 KB |        0.06 |
