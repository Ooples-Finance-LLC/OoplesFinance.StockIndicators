```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |----------:|----------:|----------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)lator [31]** |  **3.115 ms** | **10.865 ms** | **0.5956 ms** |  **1.02** |    **0.23** |         **-** |         **-** |   **509.27 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)lator [31] | 11.077 ms | 27.666 ms | 1.5165 ms |  3.64 |    0.71 |         - |         - |  6834.19 KB |       13.42 |
|            |       |                      |           |           |           |       |         |           |           |             |             |
| **Ooples**     | **10000** | **Trady(...)lator [31]** |  **5.842 ms** |  **2.931 ms** | **0.1606 ms** |  **1.00** |    **0.03** |         **-** |         **-** |  **6147.42 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)lator [31] | 31.813 ms | 42.844 ms | 2.3485 ms |  5.45 |    0.37 | 5000.0000 | 1000.0000 | 52176.28 KB |        8.49 |
