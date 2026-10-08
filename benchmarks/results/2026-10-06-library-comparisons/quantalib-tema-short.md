```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------- |------------:|------------:|----------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Tema** | **12,972.2 μs** | **16,149.8 μs** | **885.22 μs** |  **1.00** |    **0.08** |         **-** |         **-** |  **3033.78 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Tema |    185.7 μs |    169.9 μs |   9.32 μs |  0.01 |    0.00 |         - |         - |    52.81 KB |        0.02 |
|            |       |                |             |             |           |       |         |           |           |             |             |
| **Ooples**     | **10000** | **QuanTAlib.Tema** | **31,104.6 μs** |  **7,797.4 μs** | **427.40 μs** |  **1.00** |    **0.02** | **3000.0000** | **1000.0000** | **28767.75 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Tema |    696.1 μs |  3,491.9 μs | 191.40 μs |  0.02 |    0.01 |         - |         - |   475.02 KB |        0.02 |
