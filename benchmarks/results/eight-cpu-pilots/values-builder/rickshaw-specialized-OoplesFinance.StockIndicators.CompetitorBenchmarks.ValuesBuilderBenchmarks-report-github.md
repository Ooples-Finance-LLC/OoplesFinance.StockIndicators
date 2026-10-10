```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=5  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method        | PairId               | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0    | Gen1    | Allocated | Alloc Ratio |
|-------------- |--------------------- |---------:|---------:|---------:|------:|--------:|--------:|--------:|----------:|------------:|
| WithSnapshots | TaLib(...)awMan [25] | 688.4 μs | 76.13 μs | 19.77 μs |  1.00 |    0.04 | 66.4063 | 23.4375 | 554.37 KB |        1.00 |
| ValuesOnly    | TaLib(...)awMan [25] | 727.6 μs | 72.01 μs | 18.70 μs |  1.06 |    0.04 |  7.8125 |       - |  81.07 KB |        0.15 |
