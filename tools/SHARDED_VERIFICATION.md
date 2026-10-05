# Sharded correctness verification

CI builds each platform's contract verifier once, then distributes the unchanged
binary across 20 shards. Configurations are sorted by ordinal name and assigned
by index modulo 20. The unit suite likewise builds once and uses VSTest's complete
fully qualified method inventory; all theory rows for a method stay together.
Mutation workers now allow 20 concurrent jobs as well. Actual concurrency remains
subject to GitHub's runner availability and account limits.

The existing formula/platform and build-and-test gate names remain aggregate
gates. Contract aggregation rejects missing/duplicate shards, changed inventories,
mismatched assembly hashes, failures, and omitted or misassigned configurations.
Only the complete union receives `all-discovered-configurations` scope, and it
must then pass both existing formula and numerical evidence checks. A shard alone
cannot satisfy those release gates. Unit aggregation checks actual passing TRX
results against the discovered methods and rejects missing shards or methods,
failures, skips, or overlapping assignments. No test result is inferred from a
successful build.

`tools/CorrectnessVerifier` retains its unfiltered and substring-filtered modes:

```text
CorrectnessVerifier output.xml --shard-index 0 --shard-count 20
python tools/merge_correctness_shards.py --input shards --output complete.xml --count 20
```

`tools/unit_test_shards.py` provides `plan`, `run`, and `merge` commands; the CI
workflow shows their complete arguments. The test filter is carried in runsettings
to avoid Windows command-line length limits. Sharding does not split an individual
long-running configuration or theory method, so wall-clock speedup is not guaranteed
to be exactly 20-fold. CI source-build evidence also does not replace exact-package
identity verification.

Validation for this change: 43 Python tooling tests; actionlint on both modified
workflows; verifier builds and a real single-configuration shard on net10.0,
net8.0 and net461; complete discovery of 7,635 unit methods from the current binary;
and two real unit shards with successful TRX reconciliation. These smoke checks
validate the runner and accounting, not completion of the full hosted inventory.
