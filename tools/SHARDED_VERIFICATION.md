# Sharded correctness verification

Pull requests and branch pushes run the focused unit profile across 20 shards,
plus builds for every supported framework and the oldest supported SDK. The profile
includes selected calculation tests, independent
hand-calculated formula fixtures including Ehlers, and
selected numerical, input, and tooling regressions. Its explicit selection lives
in `tools/unit_test_shards.py`; it is a smoke gate, not exhaustive library evidence.
Every selected theory still executes all of its cases. Plans identify the profile,
full discovery count, selected methods, source revision, and assembly hash.

Nightly CI (04:17 UTC) and manual **CI** workflow dispatches run the complete unit
inventory, all six platform/framework contract combinations, and every reviewed
mutation. Release publishing continues to require exhaustive package contracts and
mutation evidence for the release tag; focused PR results cannot authorize a release.
Nightly/manual concurrency groups are separate from PR runs so a new PR update does
not cancel an exhaustive campaign. Schedules become active after the workflow is
merged into the default branch. No fixed wall-clock SLA is claimed: queue time and
runner limits still apply.

The exhaustive campaign builds one immutable NuGet package containing every supported framework, then
builds each platform's verifier against that package. The consumer assembly hash
must match its package entry. Each unchanged consumer is distributed across 20 shards. Configurations are sorted by ordinal name and assigned
by index modulo 20. The unit suite likewise builds once and uses VSTest's complete
fully qualified method inventory. The two library-wide configuration sweeps each
expose 20 methods with disjoint ordinal partitions, so their thousands of theory
rows are distributed across all 20 workers. Other theory rows stay with their method.
Mutation workers now allow 20 concurrent jobs as well. Actual concurrency remains
subject to GitHub's runner availability and account limits.

The existing formula/platform and build-and-test gate names remain aggregate
gates. Contract aggregation rejects missing/duplicate shards, changed inventories,
mismatched assembly hashes, failures, and omitted or misassigned configurations.
Only the complete union receives `all-discovered-configurations` scope, and it
must then pass both existing formula and numerical evidence checks. A shard alone
cannot satisfy those release gates. Unit aggregation checks actual passing TRX
results against the selected plan methods and rejects missing shards or methods,
failures, skips, or overlapping assignments. No test result is inferred from a
successful build.

`tools/CorrectnessVerifier` retains its unfiltered and substring-filtered modes:

```text
CorrectnessVerifier output.xml --shard-index 0 --shard-count 20
python tools/merge_correctness_shards.py --input shards --output complete.xml --count 20
```

`tools/unit_test_shards.py` provides `plan`, `run`, and `merge` commands; the CI
workflow shows their complete arguments. The test filter is carried in runsettings
to avoid Windows command-line length limits. Both planning and execution defer theory
expansion, avoiding unrelated theory-data generation before method filtering.
Mutation-site validation caches source reads while checking every site. Mutation
evidence tooling tests run once before fan-out instead of in every mutation shard. Sharding does not split an individual
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
