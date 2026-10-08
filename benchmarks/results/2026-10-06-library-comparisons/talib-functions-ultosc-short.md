```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean         | Error        | StdDev      | Ratio | RatioSD | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|-------------:|------------:|------:|--------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)ltOsc [22]** |  **28,361.9 μs** |  **16,437.9 μs** |   **901.02 μs** | **1.001** |    **0.04** |  **1000.0000** |         **-** |   **9830.15 KB** |       **1.000** |
| Competitor | 1000  | TaLib(...)ltOsc [22] |     275.4 μs |     113.5 μs |     6.22 μs | 0.010 |    0.00 |          - |         - |     38.63 KB |       0.004 |
|            |       |                      |              |              |             |       |         |            |           |              |             |
| **Ooples**     | **10000** | **TaLib(...)ltOsc [22]** | **158,234.0 μs** | **146,353.7 μs** | **8,022.14 μs** | **1.002** |    **0.06** | **12000.0000** | **1000.0000** | **104403.34 KB** |       **1.000** |
| Competitor | 10000 | TaLib(...)ltOsc [22] |     427.8 μs |   1,593.8 μs |    87.36 μs | 0.003 |    0.00 |          - |         - |    328.38 KB |       0.003 |
