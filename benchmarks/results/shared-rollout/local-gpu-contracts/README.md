# Local GPU contract validation

The methods listed in `scripts/competitor-gpu-methods.json` require an FP64
OpenCL GPU. They execute locally with their original assertions, including
RequiredGPU execution, independent references, output presence, startup
boundaries, allocation-arm availability and settings restoration. Some methods
also exercise CPU arms. These are correctness checks, not benchmark timings.

From the repository root on a compatible GPU machine:

```powershell
python scripts/verify-competitor-local-gpu.py record "GPU model and driver"
```

The command builds the net10 comparison test project, runs the selected methods
serially, requires every TRX result to pass and saves `receipt.json` and
`gpu.trx.gz`. The receipt records the test assembly hash, runtime, adapter
description, passed-case count and a fingerprint of production and comparator
sources, resources, build configuration, method inventory and validation scripts.
Commit both evidence files with the source change. Existing competitor timings
remain reusable; this command does not run BenchmarkDotNet timing methods.

Hosted CI discovers the full compiled method inventory, validates the local
receipt against current sources and partitions those methods from the hosted
inventory. Unknown methods, stale evidence, missing results, failures and skips
fail the gate. The union retains every discovered method; new GPU-dependent
methods fail on the hosted runner until explicitly classified and validated.
Ordinary hosted correctness checks remain automatic. A local pass does not
claim a hosted GPU run, universal device support or a performance win.
