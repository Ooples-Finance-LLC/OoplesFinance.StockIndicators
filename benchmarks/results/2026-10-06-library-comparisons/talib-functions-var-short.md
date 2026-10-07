```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId              | Mean         | Error         | StdDev       | Median       | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |-------------------- |-------------:|--------------:|-------------:|-------------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Var** | **14,487.07 μs** |  **12,067.22 μs** |   **661.445 μs** | **14,121.80 μs** | **1.001** |    **0.06** |         **-** |         **-** |   **4501.7 KB** |       **1.000** |
| Competitor | 1000  | TaLib.Functions.Var |     32.43 μs |      67.63 μs |     3.707 μs |     34.00 μs | 0.002 |    0.00 |         - |         - |    21.25 KB |       0.005 |
|            |       |                     |              |               |              |              |       |         |           |           |             |             |
| **Ooples**     | **10000** | **TaLib.Functions.Var** | **74,191.80 μs** | **119,082.70 μs** | **6,527.325 μs** | **71,401.50 μs** | **1.005** |    **0.11** | **5000.0000** | **1000.0000** | **46268.88 KB** |       **1.000** |
| Competitor | 10000 | TaLib.Functions.Var |    277.97 μs |   5,433.25 μs |   297.815 μs |    124.70 μs | 0.004 |    0.00 |         - |         - |   159.58 KB |       0.003 |
