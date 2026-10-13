```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.402
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation            | Mean        | Error      | StdDev    | Gen0     | Gen1     | Allocated  |
|------------------ |------ |--------------------- |------------:|-----------:|----------:|---------:|---------:|-----------:|
| **BuilderFull**       | **1000**  | **Skender.GetBop**       |    **49.26 μs** |   **3.516 μs** |  **2.325 μs** |   **9.7656** |   **0.9766** |   **79.57 KB** |
| BuilderLatestOnly | 1000  | Skender.GetBop       |    45.79 μs |   6.407 μs |  4.238 μs |   4.3945 |   0.2441 |   31.96 KB |
| **BuilderFull**       | **1000**  | **Skender.GetElderRay**  |   **118.26 μs** |   **8.713 μs** |  **5.763 μs** |   **9.7656** |   **1.9531** |   **80.62 KB** |
| BuilderLatestOnly | 1000  | Skender.GetElderRay  |   111.44 μs |   3.037 μs |  2.009 μs |   3.9063 |        - |      33 KB |
| **BuilderFull**       | **1000**  | **Trady(...)nLine [44]** |   **185.46 μs** |   **3.018 μs** |  **1.996 μs** |  **18.5547** |   **2.9297** |  **153.98 KB** |
| BuilderLatestOnly | 1000  | Trady(...)nLine [44] |   176.12 μs |   1.527 μs |  0.908 μs |  12.6953 |        - |  106.37 KB |
| **BuilderFull**       | **10000** | **Skender.GetBop**       |   **359.61 μs** |  **31.720 μs** | **20.981 μs** |  **83.9844** |  **48.8281** |  **693.99 KB** |
| BuilderLatestOnly | 10000 | Skender.GetBop       |   279.09 μs |   8.462 μs |  5.597 μs |  27.3438 |   7.8125 |  224.14 KB |
| **BuilderFull**       | **10000** | **Skender.GetElderRay**  | **1,186.56 μs** | **126.742 μs** | **83.832 μs** |  **93.7500** |  **46.8750** |  **784.19 KB** |
| BuilderLatestOnly | 10000 | Skender.GetElderRay  | 1,100.67 μs |  18.101 μs | 11.973 μs |  31.2500 |   7.8125 |  314.33 KB |
| **BuilderFull**       | **10000** | **Trady(...)nLine [44]** | **2,314.38 μs** |  **38.518 μs** | **22.922 μs** | **234.3750** | **171.8750** | **1931.62 KB** |
| BuilderLatestOnly | 10000 | Trady(...)nLine [44] | 2,267.51 μs |  52.636 μs | 34.816 μs | 171.8750 |  46.8750 | 1461.77 KB |
