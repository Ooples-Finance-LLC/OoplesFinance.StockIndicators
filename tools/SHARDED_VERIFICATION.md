# Sharded correctness verification

CI builds one immutable NuGet package containing every supported framework, then
builds each platform's verifier against that package. The consumer assembly hash
must match its package entry. Each unchanged consumer is distributed across 20 shards. Configurations are sorted by ordinal name and assigned
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
to be exactly 20-fold. Every CI shard records the loaded package assembly hash and package identity;
aggregation rejects mismatched packages and preserves that identity in full evidence.

`overflowReferenceSlots` lists outputs whose independent references can recognize
unrepresentable results; it does not promise that every configuration overflows.
The numerical gate requires independent trajectories and retains full finite-value
or independently proven first-overflow rejection evidence for every required
fixture. Paired cases declare `inputSeries="2"` and must cover all 18 numerical
classes with each of five benchmark shapes, including preview and reset replay.
A missing combination fails the gate.

The package-consumer build script verifies the assembly extracted from the NuGet
candidate before sharing it. Each shard checks its actually loaded assembly hash.
The merge rejects missing or inconsistent package hash, version, and framework
metadata whenever package evidence is supplied.
