# V2 numerical correctness assurance

Status: implementation authorized and in progress. Executed evidence and adopted compatibility decisions are recorded in [V2_CORRECTNESS_IMPLEMENTATION.md](V2_CORRECTNESS_IMPLEMENTATION.md). Unchecked library-wide exit gates remain required.

## User stories and success definition

As a trading-system developer, I need each indicator to document its formula, admissible inputs, initialization, time alignment and numerical error behavior, so that invalid data or unavailable results cannot silently become trading signals.

As a customer indicator author, I need the same discoverable validation harness used for built-ins, with generated cases and actionable failures, so that inheriting from the indicator base class does not bypass correctness checks.

Success means auditable, library-wide evidence against explicit contracts, with release gates that fail when evidence is missing. Tests cannot guarantee correctness for every possible program or input. Analytic guarantees must identify their assumptions; market-data truth and trading profitability are outside this guarantee.

Inheritance can provide automatic discovery and generic checks, but cannot infer an arbitrary indicator's intended mathematics. A customer indicator must supply a reviewed formula contract or independent oracle to obtain formula assurance. Missing evidence must never receive the same result as complete validation.

## Baseline and review evidence

- Repository: Ooples-Finance-LLC/OoplesFinance.StockIndicators.
- Reviewed working tree: branch `feat/component-average-stateful`, HEAD `486d29fbb00a970d3c8e0fc37b5de266146f9555`, including extensive inherited uncommitted changes. HEAD alone does not identify the reviewed implementation.
- Previous coverage inventory: 830 catalog names, 1,482 outputs, no uncovered or unexposed outputs; 966 single-series types and 4,177 configurations; six native multi-series indicators with 28 configurations. Both formula backlogs were empty.
- Previous verification: 9,026 shared tests passed; 763 affected regressions passed after the final Mobility tie refinement. These are prior, overlapping results, not tests rerun during this review.
- This review used static inspection and a small isolated executable against the existing Release/net10.0 DLL. It did not rebuild the library or run the full suite.
- DLL SHA-256: `CAD5DFD74003A8F81190B533C585B5A58172EEF2C998C90B0EC53B3F70626C25`.
- Local reproduction artifacts: `C:/Users/cheat/temp/si-correctness-plan-review/` (`Program.cs`, `Review.csproj`, `results.log`). Preserve the relevant probes as committed regressions during S0/S1; these temporary files are not durable CI evidence.

Reference coverage measures registered output coverage. It does not establish formula provenance, oracle independence, sufficient input diversity or bounded numerical error.

## Prioritized adversarial findings

| Priority | Finding and evidence | Required outcome |
| --- | --- | --- |
| P1 | Reproduced future-data consumption: `BatchCompute.cs:205` pairs primary and benchmark rows by index, truncating to the smaller count. Shifted timestamps allow a future benchmark price into an earlier primary result. | Explicit causal alignment and missing-data policy; reject unsupported alignment; no silent truncation. |
| P1 | Default customer validation passes with missing formula references. `IndicatorValidation.cs:15,91` makes strict coverage optional. Built-in CI already requests strict coverage. | Distinguish smoke checks, incomplete evidence and strict correctness validation; customers receive the same strict checks. |
| P1 | Strict validation passes positive infinity during warmup. Finite/reference checks begin after warmup (`IndicatorValidation.cs:129`, `IndicatorValidationRule.cs:61,87`). | Check every bar against a declared startup/unavailable policy. Do not assume every startup output must be finite. |
| P1 | A zero-output implementation passes a tiny signed reference under `1e-9 + 1e-9*abs(expected)` (`IndicatorValidationRule.cs:75-76`). | Output-specific numerical budgets and independent exact sign/status/discrete checks where the contract requires them. |
| P1 | Complete reference coverage only counts referenced slots (`IndicatorFormulaCoverage.cs:25`); recurrence references may use observed prior output. | Independent specification and trajectory evidence; distinguish recurrence residual checks from independent full trajectories. |
| P1 | Fixed fixture shapes, two random-walk seeds and coupled period scaling leave parameter/input classes untested (`IndicatorValidationFixtures.cs:13,24`, `IndicatorValidationCase.cs:71-73`). | Independently varied parameters, adversarial domains, deterministic generation, shrinking and persisted counterexamples. |
| P1 | Shared lifecycle coverage is limited; multi-series fixtures use aligned benchmark-final then primary events with primary previews (`MultiSeriesIndicatorValidation.cs:128,196`). | Contract-driven paired event streams, causality and state-transition checks across supported routes. |
| P1 | RSMK maps nonpositive price pairs to a zero log ratio (`StatefulIndicators.Batch22.cs:374`). Finite inputs can overflow intermediates; decimal has a narrower range than double (`BuiltInFormulaReferences.RelativeMotion.cs:1441`). | Explicit domains and undefined outcomes; independent numerical references appropriate to the domain and conditioning. |
| P2 | No dedicated mutation/shrinking campaign configuration found in the inspected sources/tests/workflows. CI tests net10 on Windows; other inspected target jobs build only. Hosted enforcement was not audited. | Mutation evidence, supported-platform execution and exact-artifact release enforcement, with hosted settings verified. |

The first four findings were reproduced. The remaining findings describe inspected assurance limitations or contract gaps; they are not claims that every affected indicator returns an incorrect value.

### Reproduction recipes and observed results

1. **Missing contract:** a customer `IndicatorBase` implementing `IIndicatorValidationContract` with an empty rule list reports `valid=True`, `complete=False`, 11 fixtures and zero failures under defaults. With `RequireFormulaReference=true`, it reports invalid with a missing-reference failure.
2. **Warmup infinity:** declare `WarmupBars=5`; return positive infinity on the first five updates and close thereafter. Reference close on every bar. Strict validation reports `valid=True`, `complete=True`, 13 fixtures, zero failures.
3. **Erased signal:** return zero; reference `(Close - 100) * 1e-14`, with no warmup. Strict validation reports `valid=True`, `complete=True`, 11 fixtures, zero failures. The comparator follows its current tolerance; that tolerance is inadequate for a contract requiring this signed signal.
4. **Future benchmark:** primary days 0/1 have closes 100/110; benchmark days 1/2 have closes 100/200; OHLC equals close. Evaluate public catalog `RSMKIndicator(catalog.Price("market"), length:1, smoothLength:1)` through Build/Start/Subscribe/GetSeries. Outputs are `0,-59.783700075561974`. The second result matches `100 * (log(110/200) - log(100/100))`, consuming day 2 for primary day 1.

## Required contract for every indicator

Each contract must identify:

- Formula source, version, variant, derivation when needed, units and every output slot; independently reviewed interpretation where sources disagree.
- Required input fields, finite/nonfinite policy, valid zero/negative domains, configuration ranges and relationships, supported magnitude and resource limits. OHLC/volume constraints apply only where the declared data model requires them.
- Initialization, seeds, warmup, first valid bar, empty/short input behavior and zero-denominator or unresolved-state semantics.
- Timestamp ordering, duplicates, timezone/session boundaries, alignment, maximum staleness and supported correction behavior. No observation after the decision timestamp may influence an output.
- Per-output error criteria tied to units and conditioning; absolute/relative/ULP or interval criteria as justified, plus exact status/discrete and declared sign requirements. Near-zero ambiguity must have explicit semantics.
- Supported batch, streaming, preview, reset, chunked, custom-component and fallback routes; rejection before state mutation or a documented atomic recovery policy.
- Evidence level: examples, independent numerical oracle, conditional properties, analytic derivation, machine-checked proof where available, mutation and platform results. An observed maximum error is not a proven bound.

## Implementation stories and exit gates

Implement and verify bounded stories in dependency order. Each story produces reviewable changes and durable regression evidence. A pilot or one repaired indicator does not complete the library-wide objective.

### S0 — Freeze baseline and decide contract semantics

Dependencies: none.

- [ ] Preserve all four probes and capture an identifiable source snapshot, including inherited changes, without mixing unrelated work.
- [ ] Inventory all public types, aliases, outputs, parameter families, native/batch/fast/custom/fallback routes and customer discovery paths; build the shared-primitive dependency map.
- [x] Review source/derivation evidence for recent Guppy pivot precedence, Mobility density and MESA coefficient smoothing changes, including numerical tie behavior. [Derivation review](V2_FORMULA_VARIANTS.md) explicitly separates library conventions from still-unauthenticated primary-publication attribution.
- [x] Retain the approved rolling population deviation for `StandardDevation.Std`; the selected moving average controls Signal. Do not reopen this decision.
- [ ] Decide strict versus smoke API compatibility, result/status versus exception behavior, supported alignment policies and formula variants. Record migrations and owners for unresolved decisions.
- [x] Audit hosted branch/release enforcement and the declared supported runtime/OS/architecture matrix. Hosted required-check names were empty; auditing this gap does not satisfy S7 enforcement.

Exit: baseline and contract decisions are reviewable; missing evidence is explicitly recorded, not silently treated as passing.

### S1 — Close demonstrated validation and alignment gaps

Dependencies: S0.

- [ ] Prevent future benchmark use, enforce the chosen alignment/missing/staleness policy, and handle unequal lengths explicitly.
- [ ] Provide strict shared validation for built-ins and customer indicators. Automatically discover eligible implementations; require explicit factories/configuration registrations when constructors cannot be generated safely, and fail coverage on omissions.
- [ ] Offer clearly named smoke validation separately. Strict validation must fail/throw on missing formula evidence and return actionable indicator/configuration/fixture/bar diagnostics.
- [ ] Validate every startup output against its declared status policy, including infinity and NaN behavior.
- [ ] Replace the tiny-signal blind spot with a justified output contract, including exact decision requirements where applicable.
- [ ] Verify new built-in and customer subclass discovery with representative constructor shapes and missing-contract cases.

Exit: all four preserved probes detect the original defect or incomplete contract; corrected valid cases pass. Misaligned inputs cannot consume future observations or silently disappear. Failures remain reproducible with complete diagnostics.

### S2 — Establish numerical and independent-oracle pilot

Dependencies: S0; integrate through S1.

- [ ] Pilot SMA, EMA, population deviation, RSI, RSMK, a recursive DSP filter and mixed custom-average composition.
- [ ] Select primary sources/derivations and independently structured reference kernels; add hand-calculated or published vectors with documented provenance.
- [ ] Use exact arithmetic where feasible; evaluate adaptive precision or interval methods for the remaining pilot. Verify convergence/rounding and avoid double intermediates that invalidate higher-precision claims.
- [ ] Define error budgets from units, conditioning and intended decision semantics, including long-run recurrence drift and cancellation.
- [ ] Add only mathematically valid metamorphic properties, with explicit preconditions: scaling, translation, bounds, causality or constant-input behavior where applicable.
- [ ] Separate recurrence residual checks from independent trajectories; document assumptions for any analytic bound. Select tools after a bounded pilot rather than prescribing a dependency in advance.

Exit: each pilot output has reviewed provenance, domain, independent evidence, numerical budget and counterexamples demonstrating detection of plausible errors. Precision alone is not accepted as formula correctness.

### S3 — Generate, shrink and retain adversarial cases

Dependencies: S1 and S2.

- [ ] Generate small/exhaustive domains where feasible, extreme scales and offsets, tiny spreads/denominators, subnormals, overflow-adjacent values, volume edge cases and valid/invalid OHLC classes.
- [ ] Independently vary period relationships, coefficients, multipliers and heterogeneous components, including empty inputs, short history, long history and window eviction.
- [ ] Generate gaps, sessions, duplicate/out-of-order/corrected events, primary/benchmark previews and missing/stale observations according to supported contracts.
- [ ] Check prefix causality, preview noncommit, reset, chunking and component-state isolation across declared routes.
- [ ] Persist seed, generator version, configuration and event order; shrink while preserving the triggering validity conditions. Promote minimized failures into the permanent regression corpus.
- [ ] Report coverage of required input/configuration/event classes and explicit resource budgets. Budget exhaustion is incomplete evidence, not a pass.

Exit: failures replay deterministically and shrink usefully; mandatory adversarial classes are exercised without treating raw test counts as diversity.

### S4 — Enforce runtime domains and state integrity

Dependencies: S0–S2; use S3 cases.

- [ ] Validate configuration once and necessary per-bar inputs cheaply; do not run generated fixture suites during production updates.
- [ ] Distinguish valid zero from unavailable, invalid or unresolved outcomes through the agreed API. Reject unsupported values explicitly; do not mask them with undocumented clamps.
- [ ] Check overflow/underflow and nonfinite behavior against declared domains. Define what happens when intermediate results leave the supported range.
- [ ] Prove by state-transition tests that rejected updates cannot corrupt subsequent valid results.
- [ ] Document migration behavior and measure guard overhead on representative paths without weakening contracts for speed.

Exit: declared invalid inputs fail deterministically; valid-domain inputs follow the contract; rejection preserves state. Validation does not claim to verify the truth of vendor data or silently repair corporate actions.

### S5 — Challenge the tests with deliberate faults

Dependencies: S2 and S3.

- [ ] Seed critical faults: sign/divisor/coefficient changes, sample versus population variance, indexing/lag, wrong initialization, removed guards, output mapping, future-data use, preview commit, omitted reset and component reorder.
- [ ] Kill 100% of designated critical fault seeds with meaningful assertions; a harness crash, timeout or inconclusive run is not credited as a kill.
- [ ] Establish per-module automated mutation floors from the pilot; inspect survivors, require reviewer rationale for equivalent mutants and prohibit blanket exclusions that hide critical paths.

Exit: every critical seed is detected; remaining survivors have explicit disposition. An aggregate mutation score cannot conceal dangerous survivors.

### S6 — Complete library-wide rollout

Dependencies: S1–S5.

- [ ] Roll out by shared-primitive dependency and financial/numerical risk in bounded batches; keep per-indicator and per-output evidence visible.
- [ ] Cover all 830 current catalog names and 1,482 outputs, reconciled against the live inventory, plus every new addition automatically.
- [ ] Cover all supported public parameter families and routes, not just typed defaults or registered sample constructors; include legacy parameters, native multi-series and customer composition.
- [ ] Require reviewed contracts and generated adversarial classes for additions. Missing registration, output, configuration family or oracle evidence fails the strict CI gate.
- [ ] Fix exposed defects within approved batches and retain minimized regressions. Review new material scope separately without calling incomplete coverage complete.

Exit: the live inventory has no unexplained omissions or placeholder contracts; each output meets the evidence standard, each required parameter/event class is exercised, and strict validation works for customer implementations. A finite suite does not exhaust every admissible double-valued input.

### S7 — Bind release claims to tested artifacts

Dependencies: S5 and S6; establish CI infrastructure earlier as needed.

- [ ] PR gates run deterministic contracts, regression corpus and affected critical mutations; scheduled jobs expand seeds and long-history campaigns within recorded budgets.
- [ ] Execute correctness checks across the declared supported runtime/OS/CPU matrix. Document any unsupported combinations rather than assuming build success establishes runtime correctness.
- [ ] Archive source snapshot, contract/oracle versions, package hash, platform, seeds, error observations, mutation dispositions and review evidence.
- [ ] Enforce release gates on the exact package tested; prevent an unverified rebuild from replacing it. Verify hosted enforcement instead of inferring it from YAML.
- [ ] Complete the captured PR #243 follow-up and publish qualified assurance claims describing assumptions and remaining limitations.

Exit: a released package can be traced to passing required evidence and review; missing or incomplete required evidence blocks release. No claim of universal mathematical correctness or profitability.

## PR #243 follow-up retained in scope

PR #243 was merged on 2026-09-23. The captured initial review set contains five threads, two unresolved:

1. [Preserve all requested average components in declaration order, including mixed/custom and additional built-in stages](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/pull/243#discussion_r4064403560).
2. [Use floating tolerance for route output parity](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/pull/243#discussion_r4065533663).

Inherited local edits appear to address both (`CustomIndicatorEngine.cs`, `ComponentAverageParityTests.cs`, `SignalOutputTests.cs`). This is not evidence that upstream is fixed. Verify the final diff and relevant tests, deliver a follow-up and only resolve findings when proven. Route-parity tolerance is separate from a formula's numerical accuracy budget. Newly arriving comments require separate triage.

## Adversarial review of this plan

| How this plan could still fail | Countermeasure / completion requirement |
| --- | --- |
| Production and oracle repeat the same mistaken interpretation. | Independent source/derivation review, different reference structure, hand/published vectors and deliberate fault seeds. |
| Higher precision gives a more precise answer to the wrong formula. | Separate specification review from arithmetic accuracy; report both evidence levels. |
| Thousands of generated cases cover the same easy regime. | Required class counters, independently varied parameters and durable minimized counterexamples. |
| Tolerance permits a wrong trading decision. | Contract-specific sign/status/discrete rules and explicit ill-conditioned or ambiguous outcomes. |
| A zero hides missing data or an unresolved cycle. | Explicit startup/domain/status semantics, tested from the first bar. |
| Mutation scores are inflated by easy faults or exclusions. | Mandatory critical seeds and reviewed survivor dispositions per module. |
| A successful pilot is reported as library-wide completion. | S6 inventory gate across outputs, public parameter families and routes. |
| Runtime validation makes the library unusably slow. | Validate configuration once; use cheap necessary guards; measure overhead while preserving semantics. |
| Tests pass on a different binary from the release. | Exact-package hash, traceable snapshot and enforced artifact promotion. |
| Customer discovery looks complete but skips constructors or unknown formulas. | Explicit missing-factory/contract failures; no formula guarantee inferred solely from inheritance. |

## Scope, decisions and delivery

This plan covers correctness contracts, adversarial validation, direct defects they reveal, customer validation and release evidence. It does not add trading features, promise profitability, automatically repair source data, migrate frameworks or select new numerical dependencies without evaluation.

Review strict-default compatibility, failure/status API, alignment policies and formula variants before coding. Population deviation is already decided. The accuracy-first contracts constrain later performance optimizations; beating competitors is a separate performance phase requiring like-for-like semantics and benchmarks.

This is substantial staged work, not a single-indicator patch. Estimate the full rollout after S0's dependency inventory and S2's measured pilot. Do not invent a completion date before those establish the remaining effort. Each batch ends with adversarial diff review and the smallest sufficient relevant verification; full release checks follow the approved matrix. Keep completed edits, actual test results and unresolved limitations distinct.
