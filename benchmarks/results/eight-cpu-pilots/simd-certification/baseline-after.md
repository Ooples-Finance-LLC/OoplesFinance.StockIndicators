```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=6

```
| Method                  | Case       | Mean       | Error    | StdDev   | Gen0     | Gen1     | Gen2     | Allocated  |
|------------------------ |----------- |-----------:|---------:|---------:|---------:|---------:|---------:|-----------:|
| **OoplesLatestOnlyBuilder** | **Asin**       | **1,249.2 μs** | **24.35 μs** | **14.49 μs** | **187.5000** | **187.5000** | **187.5000** | **1568.15 KB** |
| **OoplesLatestOnlyBuilder** | **SmaDecimal** |   **834.5 μs** |  **4.28 μs** |  **2.83 μs** | **117.1875** | **117.1875** | **117.1875** |  **786.13 KB** |
| **OoplesLatestOnlyBuilder** | **SmaGrid**    |   **729.5 μs** | **44.94 μs** | **29.73 μs** | **128.9063** | **128.9063** | **128.9063** |  **786.03 KB** |
