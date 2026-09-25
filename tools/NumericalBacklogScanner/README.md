# Numerical backlog scanner

This diagnostic tool tests existing independent formula contracts against all 18 numerical fixture classes, each at its original timestamps and with weekly spacing across calendar boundaries without changing enrollment, tolerances, or production code. It discovers cases through the public validation API. Passing results are review candidates, not mathematical proofs or release-gate evidence.

Build once against an existing library binary to avoid rebuilding the library per family:

```powershell
dotnet build tools/NumericalBacklogScanner -c Release -p:IndicatorAssembly=<absolute-path-to-library.dll>
```

Run one representative of every backlog type, then check all configurations of promising families using a subset file (one fully qualified configuration name per line):

```powershell
dotnet tools/NumericalBacklogScanner/bin/Release/net10.0/NumericalBacklogScanner.dll tests/ValidationTests/NumericalFixtureBacklog.txt frontier.jsonl representatives 8
dotnet tools/NumericalBacklogScanner/bin/Release/net10.0/NumericalBacklogScanner.dll candidates.txt candidates.jsonl all 8
```

Calendar-spaced copies prevent daily/monthly/yearly formulas from passing solely on zero startup outputs. Each numerical fixture has at least 256 bars and extends 32 bars beyond declared warmup, matching the shared validator (8192-bar ceiling). Each completed result is flushed immediately with failures, fixture-level evidence and formula coverage. Rerunning the identical command resumes completed cases. Assembly, scanner, runtime, OS, architecture, selection and fixture-policy changes reject stale evidence. A malformed/truncated log fails closed; preserve it and use a new output file. Ctrl+C requests cancellation; the three-minute per-case cancellation is cooperative, so a synchronous calculation can exceed it. Exit 0 means every selected case passed, 1 means completed diagnostic failures, and 2 means interruption/incomplete work. Fatal argument or evidence errors also exit nonzero.

Review the reference arithmetic, output policies, execution routes and appropriate injected faults before promoting candidates. Input-domain rejections and unavailable warmup values are recorded separately by the shared validator and are not numerical output comparisons. The scanner intentionally keeps all ordinary validation checks too. No background service or agent is required; workers execute test cases in one process.

CLI verification after building: `python -m unittest discover -s tools/NumericalBacklogScanner/tests -p test_scanner.py`.

After a scan completes, create review queues and rank common failure signatures:

```powershell
python tools/summarize_numerical_backlog.py results.jsonl
```

This writes `.summary.json`, `.candidates.txt` and `.passing-families.txt` beside the evidence. A family is complete only relative to the scan selection; use `all` mode on the full backlog for the broad queue. The summarizer refuses incomplete, duplicate or inconsistent records. Passing queues still require review and never edit the enrollment backlog. Use the same compiled library for related scans so results have one source identity.

Process larger related families together: review their independent references and numerical policies, fix shared defects, add direct-route tests and meaningful faults, then do one grouped build/verification. A passing diagnostic is not a reason to relax a tolerance or skip route/fault checks.

Scanner CLI tests live separately from the lightweight `tools/tests` suite, which mutation shards run before any .NET build. This avoids rebuilding the library once per shard just to test the diagnostic tool.
