# Shared indicator validation



The `OoplesFinance.StockIndicators.Validation` API runs in tests and development tools.

Normal builder execution does not run synthetic fixtures. The V2 typed builder rejects nonfinite OHLCV inputs and enforces declared `IIndicatorInputDomainContract` requirements on the actual inputs, including derived closes.

The repository's `BuiltInSharedValidationTests` uses this same API for built-in indicators.



## Automatically test your indicators



An xUnit project can discover indicators in your assembly and generate a test for every configuration:



```csharp

public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery

    .Discover(new[] { typeof(MyIndicator).Assembly })

    .Select(c => new object[] { c });



[Theory]

[MemberData(nameof(Cases))]

public async Task IndicatorsSatisfyTheirContracts(IndicatorValidationCase testCase)

    => await IndicatorValidation.ValidateAndThrowAsync(testCase,

        new IndicatorValidationOptions { RequireFormulaReference = true });

```



Discovery examines concrete `IIndicator` implementations in the supplied assemblies, including

`IndicatorBase` and `MultiOutputIndicatorBase` subclasses, without a namespace restriction.

New classes in those assemblies are included automatically. Assembly loading failures, unsupported

constructors, calculation exceptions, and missing outputs are failures, not exclusions.

Abstract base classes are not test subjects. Open generic classes need a concrete wrapper or factory

registration with a concrete discovered type.



Discovery uses optional defaults and supplies required integer length/period/lookback arguments.

For built-ins, it also reads parameter defaults from the corresponding options constructors.

It preserves different period values and generates shorter and longer variants plus a weighted-average

variant where supported. It does not infer meaningful values for arbitrary dependencies or coefficients.

Register named factories for those types; registrations replace that type's inferred configurations:



```csharp

var cases = IndicatorValidationDiscovery.Discover(

    new[] { typeof(MyIndicator).Assembly },

    new[] { new IndicatorValidationCase(typeof(MyIndicator), "configured",

        () => new MyIndicator(myDependency, length: 17)) });

```



Factories must create fresh instances. Register additional cases to cover coefficient boundaries,

special dependencies, or parameter combinations meaningful to the indicator's definition.



## What is checked

`IndicatorValidationReport.FixtureEvidence` records every attempted named fixture, including execution failures. Each row gives its requested bar count, values checked, successful input rejections, and completion/pass status. `Completed` means execution reached the end; a completed fixture can still fail mathematical assertions. Inspect these rows to distinguish exercised input classes from aggregate counts. Empty-input fixtures intentionally check no output values. Single-series field probes are counted separately in the report's total input rejections; paired probes are injected within each fixture and appear in its row. The standalone verifier includes these rows in its XML evidence.

Zero-price and negative-price fixtures are mandatory for built-ins and customer indicators. Declared domain violations are tested for rejection before recovery inputs are used for formula comparisons. Additional fixture names must be unique and must not reuse a generated name.

Every fixture also checks that the run's default series and latest snapshot agree with the indicator's declared primary output. New built-ins and customer subclasses receive these checks automatically, alongside their per-slot formula references.



Every declared output is checked for bar alignment, repeatability across fresh instances, and finite

values from the first bar under the declared startup policy. Both public reference overloads and built-in references compare from bar zero by default, including finite startup values. Unavailable startup requires both the reference and actual output to be NaN under the independently checked startup policy; numerical tolerance does not equate unavailable and finite values. An explicit `includeWarmup: false` on the error-budget overload limits that particular rule to post-warmup evidence. Strict validation requires another reference to cover startup for that output; otherwise it records `StartupFormulaReference` and the missing slots. Input bars must remain unchanged. Fixtures include empty and

one-bar inputs, warmup boundaries, flat prices, rising and falling prices, alternating prices,

a spike, zero volume, and two reproducible random walks. Full fixtures extend past warmup;

an excessive warmup produces a coverage error rather than an empty successful check.

Customer states also replay every accepted fixture before and after `Reset()`, comparing every output with the fresh finite run. This covers scalar, composed, multi-output and composed multi-output state interfaces, including customer dependencies beneath a built-in root. Components retain declaration order and chained inputs use the declared primary output. A mismatch records a `Reset` failure with the customer type, slot and bar; successful fixtures expose `CustomerResetChecked`. These checks run only in validation, not during production updates. Built-in native preview/reset coverage remains in its separate state harness.

Moving averages automatically get constant-preservation checks at prices 50 and 100, using the

last 1,000 bars of fixtures with at least 4,000 bars. These finite convergence budgets are test

policies: a valid very slow filter may require larger `BarsPerFixture` and `MaximumBarsPerFixture`.



`ValidateAsync` returns all collected failures with configuration, fixture, rule, output and bar

details where applicable. `ValidateAndThrowAsync` or `report.ThrowIfInvalid()` throws

`IndicatorValidationException` when a check fails. Cancellation propagates.

`MathematicalRuleCount` distinguishes supplied mathematical contracts from universal checks alone.

Set `IndicatorValidationOptions.RequireMathematicalContract = true` to reject configurations without

any mathematical rule. A positive rule count describes coverage, not completeness of a proof.



## Mathematical contracts



Finite, deterministic output can still implement the wrong formula. Indicators can implement

`IIndicatorValidationContract` to supply automatically checked rules, or callers can attach rules

to `IndicatorValidationCase`. `IndicatorValidationRule.Bounds` checks a declared output range;

`IndicatorValidationRule.Reference` checks an independent formula on each fixture. Arbitrary

named rules can check identities, scaling laws, or relationships between outputs by throwing on failure.



```csharp

public IEnumerable<IndicatorValidationRule> ValidationRules => new[]

{

    IndicatorValidationRule.Bounds(outputSlot: 0, minimum: 0, maximum: 100),

    new IndicatorValidationRule("My mathematical identity", context =>

    {

        // Assert an identity justified by this indicator's definition.

        // Throw with the output slot and bar index when it does not hold.

    })

};

```



Do not declare bounds merely because values usually lie there. PPO has no universal upper bound;

some moving averages overshoot; cumulative indicators need not settle on flat prices.

Passing generated tests is evidence for the exercised properties and configurations, not proof of

every mathematical identity over all inputs. Formula correctness requires suitable contracts and

independent references. The validator's tests deliberately inject NaNs, bad secondary outputs,

wrong finite formulas, nondeterminism, exceptions, and zero-volume defects to check detection.



## Per-output formula references



`IndicatorFormulaCoverage.Inspect(testCase)` reports `ReferencedOutputSlots`,

`MissingOutputSlots`, and `IsComplete` without running fixtures. The validation report

also exposes this inventory as `FormulaCoverage` (null if construction/inspection fails).

Declared coverage does not mean the reference passed.



Set `new IndicatorValidationOptions { RequireFormulaReference = true }` with

`ValidateAndThrowAsync` to fail when **any output** lacks a reference. This works for

built-ins and customer assemblies discovered by `IndicatorValidationDiscovery`.

Supply `IndicatorValidationRule.Reference(slot, independentFormula)` through a case

registration or `IIndicatorValidationContract`. Bounds, constant preservation, and

arbitrary assertion callbacks remain properties; they cannot satisfy this requirement.

Validation is explicit test/development work and does not run during production use.



The built-in registry independently computes standard averages, weighted-window filters,
price transforms, returns, rolling extrema, regression, momentum, stochastic, volume,
and volatility formulas. Aliases inherit their named output references. All reference
arithmetic is separate from production calculation, core, and streaming implementations.
Hand-calculated cases pin initialization, field selection, coefficient normalization,
secondary outputs, and deliberate mutations.

Initialization conventions are explicit: SMA publishes zero before the full window;
WMA and FIR filters use zero padding with their full weight denominator; EMA begins
with cumulative means then uses alpha = 2/(period+1) at every positive period; Wilder
smoothing begins from zero. AveragePrice means (open+close)/2, distinct from OHLC4.
Bollinger bands use population window deviation; StandardDeviationVolatility is instead
a smoothed squared-residual measure. Standard CCI uses the current window's mean and
mean absolute deviation. Bounds and zero-denominator conventions are indicator-specific.

Equal built-in average types at each smoothing stage can be resolved through their
options. Customer average implementations and heterogeneous stage types require their
own references; the registry cannot infer their formulas from one moving-average enum.

`tests/ValidationTests/FormulaReferenceBacklog.txt` lists every discovered configuration

and output still missing a formula reference. Both coverage backlogs are now required to remain empty. Every automatically discovered
built-in configuration must provide an independent formula for every output, and every
catalog output must be represented by either a typed indicator or a native multi-series
state. The formula theory executes all configurations without filtering out missing
references. A new indicator or output without a contract therefore fails CI.

Finite fixtures cannot guarantee detection of every error; references themselves need

review and hand-calculated examples. Current fixtures vary input shape and constructor

periods but do not exhaust parameters, magnitudes, graph compositions, or all histories.


The shared fixtures include multiple trading sessions (four bars per day), so daily
pivot contracts check completed-session aggregation and causality. Coverage describes
the outputs exposed by each discovered v2 type; a middle-band-only type does not
establish coverage of legacy upper and lower band outputs.

Discrete floating-point classifiers need a specified arithmetic convention: Elder
Impulse compares binary64 EMA and MACD-histogram values, using the weighted EMA
recurrence `alpha*x + (1-alpha)*previous` after cumulative-mean initialization.
Its reference pins that evaluation order and requires strict movement on both
components. Near-zero histogram changes can still reflect floating-point rounding;
this contract does not certify the sign of the corresponding exact-real expression.

## Paired primary and benchmark indicators

`IndicatorValidationDiscovery.DiscoverMultiSeries(assemblies)` discovers native `IMultiSeriesIndicatorState` implementations. `MultiSeriesIndicatorValidation.ValidateAndThrow(testCase, new() { RequireFormulaReference = true })` checks every declared output against paired independent references, including identical, scaled, flat, independent, and zero benchmarks. It also checks repeated fresh execution, reset, speculative primary updates, output keys and finite results. Benchmark updates precede primary updates; these aligned fixtures do not claim to cover every asynchronous arrival policy.

Customer states can implement `IMultiSeriesIndicatorValidationContract` to declare output keys and a `MultiSeriesFormulaReference`, or supply `MultiSeriesIndicatorValidationCase` registrations for dependencies and custom constructors. Unsupported discovery and missing required references become failures, never skips. References receive both actual bar series; no fake default benchmark is embedded in an indicator. Validation is explicitly invoked in tests or development and adds no work to normal production updates.

### Results outside binary64 range

An independent oracle can use `IndicatorValidationRule.ReferenceWithOverflowRejection(slot, reference, errorBudget)` when the correctly rounded mathematical result itself overflows double range. The reference returns signed infinity for that case. This is not permission to copy an overflowing production expression into the oracle: prove the final result using independent wider/exact arithmetic. Ordinary `Reference` rules continue to reject infinity.

The overflow-aware rule must be the sole reference for its output, with additional property rules allowed. Validation checks two fresh finite-prefix runs against all applicable rules, then requires two fresh executions to throw `IndicatorOutputException` at the first proven overflow bar, for the same indicator type, output slot and sign. A finite substitute, earlier error, NaN, unrelated exception, or incorrect finite prefix fails. Conflicting references are rejected.

Reports and fixture receipts expose `OutputOverflowRejectionsChecked` separately from input rejections and finite values checked. A successfully rejected fixture stops at the first unrepresentable result; its remaining bars are not evaluated. This evidence does not establish live-state recovery after rejection or validate other execution routes automatically.
