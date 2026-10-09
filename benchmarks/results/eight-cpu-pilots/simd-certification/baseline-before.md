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
| **OoplesLatestOnlyBuilder** | **Asin**       | **1,262.3 μs** | **32.57 μs** | **17.03 μs** | **195.3125** | **195.3125** | **195.3125** | **1568.43 KB** |
| **OoplesLatestOnlyBuilder** | **SmaDecimal** |   **835.8 μs** |  **3.91 μs** |  **2.33 μs** | **117.1875** | **117.1875** | **117.1875** |  **786.05 KB** |
| **OoplesLatestOnlyBuilder** | **SmaGrid**    |   **700.2 μs** |  **2.15 μs** |  **1.28 μs** | **128.9063** | **128.9063** | **128.9063** |  **786.07 KB** |
