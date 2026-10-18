```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.402
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method   | Count | Operation            | Mean      | Error     | StdDev    | Gen0      | Gen1      | Gen2     | Allocated |
|--------- |------ |--------------------- |----------:|----------:|----------:|----------:|----------:|---------:|----------:|
| **Snapshot** | **1000**  | **Skend(...)icSar [23]** |  **3.770 ms** | **0.3150 ms** | **0.2083 ms** |  **250.0000** |   **62.5000** |        **-** |   **2.02 MB** |
| **Snapshot** | **1000**  | **TaLib.Functions.Sar**  |  **3.909 ms** | **0.1180 ms** | **0.0702 ms** |  **250.0000** |         **-** |        **-** |   **2.06 MB** |
| **Snapshot** | **1000**  | **TaLib(...)arExt [22]** |  **3.941 ms** | **0.1003 ms** | **0.0597 ms** |  **250.0000** |         **-** |        **-** |   **2.06 MB** |
| **Snapshot** | **1000**  | **Trady(...)verse [39]** |  **3.278 ms** | **0.1518 ms** | **0.1004 ms** |  **218.7500** |         **-** |        **-** |   **1.77 MB** |
| **Snapshot** | **10000** | **Skend(...)icSar [23]** | **48.530 ms** | **9.5140 ms** | **6.2929 ms** | **2750.0000** | **1000.0000** | **250.0000** |  **20.25 MB** |
| **Snapshot** | **10000** | **TaLib.Functions.Sar**  | **39.333 ms** | **1.0248 ms** | **0.6099 ms** | **2500.0000** |         **-** |        **-** |  **20.65 MB** |
| **Snapshot** | **10000** | **TaLib(...)arExt [22]** | **39.018 ms** | **1.2696 ms** | **0.7555 ms** | **2500.0000** |         **-** |        **-** |  **20.65 MB** |
| **Snapshot** | **10000** | **Trady(...)verse [39]** | **32.183 ms** | **0.5256 ms** | **0.3128 ms** | **2000.0000** |         **-** |        **-** |  **17.95 MB** |
