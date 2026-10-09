# Elevated ETW CPU and GC evidence

Six PerfView captures profile commit `f945ae64`, before the ingestion change in
this batch. Each workload warms for three seconds, then runs for ten seconds.
`manifest.json` records exact workload PIDs, the production DLL hash and raw ETL
hashes. Raw ETLs and public native PDBs remain local in
`C:/Users/cheat/temp/si-elevated-current`.

`cpu-summary.json` uses exclusive foreground samples from the exact logged PID,
excludes setup stacks, and approximately removes the three-second warmup. Native
symbols resolve all but 0.4-0.6% of leaf samples. Inlined work remains attributed
to its caller. Percentages across separate traces are not per-operation speed
comparisons. The compressed XML exports contain only the corresponding workload
process; `analyze.py <directory>` reproduces the CPU summaries from these exports.

| Workload | Principal exclusive foreground CPU |
|---|---|
| Asin builder | native asin_fma 67.2%, FillAsin 19.7%, native asin 1.8% |
| Asin TALib adapter | native asin_fma 43.5%, adapter 41.1%, Double.Asin 5.5% |
| Grid SMA builder | FillSma 67.5%, ProcessInPlace 22.5% |
| Grid SMA TALib adapter | adapter 60.7%, CalcSimpleMA 27.5% |
| Decimal SMA builder | FillSma 56.4%, ProcessBoundedPositive 37.1% |
| Decimal SMA TALib adapter | adapter 61.9%, CalcSimpleMA 25.8% |

`gc-summary.json` and the per-process GC CSVs cover the whole workload process,
including setup and warmup. GC CPU percentages for builder / adapter are 13.6 /
9.8 for Asin, 8.1 / 10.7 for grid SMA, and 6.8 / 10.2 for decimal SMA. These are
elevated ETW measurements, superseding the unsupported zero/NaN GC CPU fields in
the previous EventPipe campaign. Foreground sample percentages and whole-process
GC percentages have different denominators and must not be added together.

Reproduction: edit local executable/output paths in `collect.ps1`, run it from
an elevated PowerShell, then export all-process CPU stacks with PerfView
`UserCommand SaveCPUStacks <trace.etl.zip>` and GC with `UserCommand GCStats`.
Resolve native PDBs before export. Do not select the process by the name `dotnet`:
PerfView can select an unrelated build process. Filter using the exact PID in
each workload log. This PerfView version can throw after writing the CPU ZIP;
check that the XML parses and contains the expected PID and sample coverage.

The first ingestion candidate kept summaries local and copied the snapshot only
on the last row. Its JIT output promoted grid fields but spilled positive-range
fields; its benchmark did not establish a gain. It was replaced, not shipped.
`rejected-local-state-*` preserves that experiment. The final change instead uses
one bar local for both validation and the final snapshot. It preserves the
certification algorithm and avoids the second 48-byte copy without an added
last-row branch. Generated-code evidence and final measurements accompany this
report; no global rollout or raw-performance win is implied.
