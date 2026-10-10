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
| **OoplesLatestOnlyBuilder** | **Asin**       | **1,256.5 μs** | **44.87 μs** | **26.70 μs** | **203.1250** | **203.1250** | **203.1250** | **1568.58 KB** |
| **OoplesLatestOnlyBuilder** | **SmaDecimal** |   **595.0 μs** | **22.68 μs** | **15.00 μs** | **125.0000** | **125.0000** | **125.0000** |  **786.04 KB** |
| **OoplesLatestOnlyBuilder** | **SmaGrid**    |   **565.7 μs** |  **3.23 μs** |  **2.14 μs** | **121.0938** | **121.0938** | **121.0938** |  **786.03 KB** |
