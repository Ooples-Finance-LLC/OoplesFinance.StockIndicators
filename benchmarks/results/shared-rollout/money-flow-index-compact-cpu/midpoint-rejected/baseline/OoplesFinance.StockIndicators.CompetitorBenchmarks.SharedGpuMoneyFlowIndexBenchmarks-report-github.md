```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation           | Count | Mean      | Error    | StdDev   | Gen0    | Gen1    | Allocated |
|------------------ |-------------------- |------ |----------:|---------:|---------:|--------:|--------:|----------:|
| **BuilderFull**       | **Skender.GetMfi**      | **1000**  |  **27.69 μs** | **3.990 μs** | **2.639 μs** |  **7.8125** |       **-** |  **65.38 KB** |
| BuilderLatestOnly | Skender.GetMfi      | 1000  |  22.86 μs | 1.463 μs | 0.968 μs |  1.9531 |       - |  17.77 KB |
| **BuilderFull**       | **Skender.GetMfi**      | **10000** | **217.58 μs** | **6.195 μs** | **4.098 μs** | **76.1719** | **35.1563** | **628.27 KB** |
| BuilderLatestOnly | Skender.GetMfi      | 10000 | 172.04 μs | 2.500 μs | 1.307 μs | 18.5547 |  5.8594 | 158.41 KB |
| **BuilderFull**       | **TaLib.Functions.Mfi** | **1000**  |  **27.18 μs** | **1.789 μs** | **1.064 μs** |  **7.8125** |  **1.4648** |  **65.38 KB** |
| BuilderLatestOnly | TaLib.Functions.Mfi | 1000  |  25.44 μs | 2.096 μs | 1.386 μs |  2.0752 |       - |  17.77 KB |
| **BuilderFull**       | **TaLib.Functions.Mfi** | **10000** | **207.92 μs** | **7.705 μs** | **4.585 μs** | **76.1719** | **35.1563** | **628.27 KB** |
| BuilderLatestOnly | TaLib.Functions.Mfi | 10000 | 172.81 μs | 3.422 μs | 2.263 μs | 18.5547 |  5.8594 | 158.41 KB |
