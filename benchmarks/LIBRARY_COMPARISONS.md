# Full-library comparisons

Stacked on #246. The reflected manifest inventories 421 public API families from
Skender.Stock.Indicators 2.7.3, TALib.NETCore 0.5.0, Trady.Analysis 3.2.8, and
QuanTAlib 1.0.0. There are 401 paired families, 16 unavailable implementations with
verified counterparts (including two explicitly owned definitions), 4 utilities,
and no pending families.

`paired` means full-trajectory correctness and both performance runners exist;
it does not claim that timings have been collected. `pending` makes neither claim.
The production library has no competitor package dependency.

## Implemented comparisons

- SMA, WMA, and EMA in all four libraries.
- Skender and TA-Lib DEMA/TEMA, with distinct initialization formulas.
- TA-Lib and QuanTAlib triangular windows, plus TA-Lib rolling sum.
- TA-Lib Min, Max, MinMax, four price transforms, true range, and all five
  momentum/rate-of-change forms.
- Skender and Trady true range.
- Skender, TA-Lib, and Trady on-balance volume with explicit seed conventions.
  Skender includes its optional SMA and all startup presence flags.
- Skender ROC with momentum, nullable percentage change, and optional nullable SMA.
- Skender ROC with seeded EMA and symmetric RMS bands, including all presence flags.
- Twelve Trady rolling and historical high/low families.
- Trady momentum, gain/loss momentum, and their three generic difference families.
- Trady rate of change and generic percentage difference, including nullable results.
- TA-Lib engulfing, five average-range doji patterns, seven body/shadow patterns,
  four context-reversal patterns, Harami/Harami Cross/Homing Pigeon/Doji Star, and Matching Low/Stick Sandwich/
  Three Line Strike/Three Outside, Piercing Line/Dark Cloud Cover, and On Neck/In Neck/Thrusting,
  Three Inside, Two Crows, Upside Gap Two Crows, Unique Three River, Kicking/Kicking By Length, Rickshaw Man,
  Counterattack, Separating Lines, Gap Side-by-Side White Lines, Three Black Crows,
  Identical Three Crows, Three White Soldiers, Morning/Evening Star,
  Morning/Evening Doji Star, Abandoned Baby, Tristar, Tasuki Gap,
  Up/Down-side Gap Three Methods, Breakaway, Concealing Baby Swallow, Ladder Bottom,
  Hikkake, Modified Hikkake, Mat Hold, Rising/Falling Three Methods, Advance Block,
  Stalled, and Three Stars in the South: all 61 TA-Lib candle families.
- Trady candle direction, up/down trends, trend-qualified engulfing, Doji,
  Dragonfly Doji (both API spellings), Gravestone Doji, and eight percentile-based
  body/shadow patterns, plain and directional Harami, delayed Dark Cloud Cover,
  upside/downside Tasuki gaps, bullish/bearish Abandoned Baby,
  variable-length Rising/Falling Three Methods, and all five trend-star API families.
- Skender Doji and Marubozu, including all eleven calculated outputs and nullable
  price presence. TA-Lib rolling minimum/maximum indices (including the combined
  API), close midpoint, and high/low midpoint.
- TA-Lib raw Balance of Power and Skender simple-averaged Balance of Power,
  including missing windows caused by zero-range candles.

The candle counterparts add 92 public v2 indicators with independent validation
contracts and boundary, extreme-value, reset, and lifecycle checks. Different
competitor definitions have distinct public indicators: Trady uses decimal-ratio
and trend definitions, while TA-Lib uses adaptive prior-window thresholds.
Hammer/Hanging Man near windows exclude the current and immediately preceding
candle. Gaps and shadow comparisons are strict; near boundaries are inclusive.
Takuri requires a lower shadow longer than twice the body; Belt Hold checks the
opening-end shadow.

Trady scalar and close extrema use prepared close-only bars; high/low extrema use
original OHLC fields. Historical counterparts use an Ooples window as long as the
complete input, preserving every expanding-prefix output. Distinct-field fixtures
verify these mappings. Price and extrema timings use realistic OHLC datasets.

TA-Lib AvgPrice maps to Ooples FullTypicalPrice (OHLC/4). Ooples AveragePrice is
the open/close midpoint and is not the same formula. All three true-range
comparisons start at the second candle; Ooples additionally supplies the first
candle's own range. TA-Lib 0.5.0 rejects one-bar inputs even for zero-lookback price
transforms. Executable boundary tests pin that limitation, which is recorded in
the manifest. Ooples' first-bar transform values are checked separately.

Harami permits inclusive body containment; Harami Cross and Homing Pigeon require
strict containment, and Doji Star requires a strict directional body gap. The prior
long-body window excludes that prior candle. The current short-body/doji window
includes it. Independent references and hand-checked tests cover both offsets,
threshold equality, candle color, strict boundaries, and extreme/subnormal prices.

Matching Low and Stick Sandwich use inclusive close tolerances of one twentieth
of the mean range preceding the anchor candle. Three Line Strike uses one-fifth
near-body tolerances on separate historical windows, strict advancing closes,
and a final opposite candle crossing the first open. Its signal retains the first
three candles' direction. Three Outside requires strict engulfing and close
confirmation, without a color constraint on the third candle; its lookback is three.

EMA uses an initial SMA in Skender, TA-Lib, and QuanTAlib. Trady instead starts
from the first close, matched by the new public `FirstValueEma`. Its exact convex
recurrence handles extreme and subnormal values without intermediate overflow.
QuanTAlib's compensated initialization and explicit-alpha constructor remain
listed as pending configurations even though its conventional EMA pair is ready.

`LaggedPriceChange` exposes difference, fractional change, percentage change,
price ratio, percentage ratio, and positive gain/loss as explicit formula choices. History is lazy,
startup outputs are zero, and a zero denominator yields zero for ratio forms.
Exact arithmetic preserves finite results across overflowing intermediate
differences; the runtime rejects mathematically unrepresentable outputs.
Independent rational references verify that distinction and lifecycle behavior.
Trady negative difference is the positive loss magnitude, not a signed negative
change. Both directional forms clamp the opposite direction before overflow.
Pinned-package tests also check the generic APIs and tuple entry points.

`RateOfChangeWithValidity` publishes percentage change and an `IsDefined` flag.
It distinguishes a real zero return from missing history or a zero denominator,
matching Trady's nullable results. The comparison derives its presence mask from
the public flag, and checks the competitor's nullable output against an independent
reference. Both public outputs have exact contracts and lifecycle checks.

`RateOfChangeWithAverage` supplies Skender's three numeric outputs and explicit
ROC/average validity flags. Its average uses rounded ROC values and becomes
unavailable while any missing ROC remains in the window. Disabled averaging
is checked as an entirely absent output. Tests cover disabled and multiple
averaging periods; timings enable a 20-bar average and return all three outputs.
The primary output is ROC. Public raw momentum and ROC overflow are rejected;
exact rolling sums preserve finite averages when an ordinary sum would overflow.

`RateOfChangeRmsBands` matches Skender ROC-with-bands: its bands are symmetric
root-mean-square returns, not centered standard deviation. A missing return
permanently invalidates the EMA, including during its seed; bands recover after
missing returns leave their window. Exact square accumulation avoids intermediate
overflow. Seven public outputs (four numeric series and three validity flags)
have independent contracts; the comparison preserves all four nullable series.

`SeededExponentialAverage` implements DEMA and TEMA with explicit initialization:
Skender seeds all stages with the same first SMA; TA-Lib seeds each stage from
the preceding stage's mature outputs. Their startup lengths differ. Exact convex
recurrences and final extrapolation preserve extreme constants and reject true
output overflow. TA-Lib rejects period one, pinned by tests and the manifest;
Skender and Ooples support it. QuanTAlib's compensated DEMA/TEMA remain pending.

`TriangularWindowAverage` uses exactly N prices, with weights rising toward the
center and falling symmetrically. Even periods have a two-price plateau. This
is distinct from the existing two-N-period-SMA triangular formula. Two exact
rolling sums implement the convolution with one final rounding and constant
update time. `RollingPriceSum` preserves cancellation and rejects true sum
overflow. Both grow storage lazily. TA-Lib rejects period one for both APIs;
Ooples supports it, as does QuanTAlib Trima. Boundary tests pin those differences.

SeededOnBalanceVolume starts at zero for Skender and at first volume for TA-Lib
and Trady, including negative or zero initial closes. Equal closes ignore volume.
Its optional average excludes the seed bar, matching Skender: the first N-value
window is bars 1 through N. Exact accumulation retains small residuals through
large cancellation; true output overflow is rejected. TA-Lib rejects singleton
inputs, recorded by an executable boundary check. Timings use varying volumes.

Piercing Line requires two strictly long bodies, a strict opening gap below the
prior low, and a close strictly inside the prior body above its midpoint. Dark
Cloud Cover requires only the preceding bullish body to be long, a strict gap
above its high, and a close strictly inside its body beyond the penetration
fraction (default 0.5). Exact rolling sums and product comparisons preserve
boundaries at extreme and subnormal prices. Both threshold windows exclude the
body they classify. Dark Cloud Cover accepts finite nonnegative penetration;
values at least one cannot match. Trady PiercingLine remains a throwing stub
with this independently verified alternate counterpart.

StrictHaramiCandle uses opposite bearish/non-bearish colors with strict body
containment and optional strict shadow containment. A doji may match after a
bearish body. BullishHaramiPattern and BearishHaramiPattern additionally require
a strictly directional current candle and prior consecutive falling/rising opens
and closes, respectively. They use constant storage, without subtracting prices.

Trady's directional Harami object/generic routes ignore containedShadows and use
open/close trends rather than high/low trends. Their tuple wrappers instead run
plain Harami, honor containedShadows, and ignore trend period. Executable checks
cover every route and both shadow settings. Tuple counterparts use
StrictHaramiCandle; the public directional counterparts honor their shadow option.
These package limitations are recorded in the manifest.

DelayedDarkCloudCoverPattern preserves Trady's distinct formula: a bullish
anchor after strictly rising highs/lows, then a bearish candle opening above the
anchor close and closing below its midpoint. There is no long-body requirement,
no gap above the prior high, and no lower bound on the bearish close. A delay of
one reports immediately on the bearish candle; larger delays only defer reporting.
Trady's downTrendPeriodCount selects that delay without confirming a downtrend.
Its longPeriodCount/longThreshold arguments are ignored; delay zero fails when it
accesses beyond the final bar. Executable tests cover object, generic, and tuple
routes. The public counterpart requires positive delay and uses exact midpoint
comparisons and storage that grows with received history, not the declared delay.

OnNeckCandle, InNeckCandle, and ThrustingCandle require a prior strictly long
black body and a white candle opening strictly below its low. On Neck closes
within an inclusive tolerance of the prior low; In Neck closes from the prior
close through that close plus tolerance; Thrusting closes strictly beyond that
tolerance and at or below the prior body midpoint. White includes unchanged
open/close values. Tolerance is one twentieth of the mean of five prior ranges;
longness uses ten prior bodies. Both windows exclude the prior pattern candle.
The public periods are independently configurable, with exact rolling sums and
comparisons that preserve range/body overflow and subnormal boundary behavior.

ThreeInsideCandle requires a short second body strictly contained in a long
first body and an opposite-color final close beyond the first open. TwoCrowsCandle
requires a long white body, a black body gapping above it, and a third black
candle opening inside the second body and closing inside the first. It has no
short-body requirement. UpsideGapTwoCrowsCandle instead requires a short second
body and a third black body strictly engulfing it while closing above the first.
UniqueThreeRiverCandle requires a long black first body, a black second body
closing inside it and making a lower low, then a short white body opening above
that low. Its second open may equal the first open.

Three Inside and Upside Gap Two Crows allow short-body equality; Unique Three
River requires strict shortness. Each threshold excludes the body it classifies,
including the third-body threshold in Unique Three River. Exact rolling sums,
independent rational references, and hand-calculated fixtures pin these offsets
and boundary rules. Trady UpsideGapTwoCrows is a confirmed throwing stub linked
to the verified alternate counterpart.

KickingCandle and KickingByLengthCandle require two opposite, strictly long
bodies with both shadows strictly below one tenth of the preceding mean range,
plus a strict full-range gap in the second candle's direction. Each candle uses
its own preceding body/range windows. Kicking reports the second color; Kicking
By Length reports the longer body's color, selecting the first on an exact tie.
RickshawManCandle requires an inclusive doji-body threshold, both shadows strictly
longer than the body, and a body intersecting the inclusive range-midpoint band.
Its band extends one fifth of a separate preceding mean range on either side.
Exact body-length and midpoint comparisons preserve ties and finite classification
when ordinary subtraction or midpoint addition would overflow.

CounterattackCandle requires opposite, strictly long bodies with closes inside an
inclusive tolerance of one twentieth of the mean range preceding the first candle.
SeparatingLinesCandle instead compares opens with that tolerance and requires only
the current body to be strictly long, with its opening-end shadow strictly below
one tenth of the mean range preceding the current candle. Both report the second
color. GapSideBySideWhiteLinesCandle reports the direction of two strict body gaps
relative to the first candle. The second and third candles are white (including
dojis), with inclusive open and body-size tolerances of one twentieth and one
fifth of their respective mean ranges preceding the second candle. Exact linear
comparisons preserve inclusive boundaries and unrepresentable intermediate bodies.

ThreeBlackCrowsCandle requires a preceding white candle, three black candles with
strictly declining closes and very short lower shadows, and later opens strictly
inside the preceding bodies. IdenticalThreeCrowsCandle instead uses inclusive
open-to-prior-close tolerances of one twentieth of preceding mean ranges; it does
not require a preceding white candle. ThreeWhiteSoldiersCandle requires strictly
increasing closes and very short upper shadows; later opens may extend up to one
fifth of the preceding mean range beyond the prior close. Body shrinkage must be
strictly less than three fifths of the preceding mean range, and the final body
must strictly exceed its prior mean. Each threshold excludes its classified candle.
Exact arithmetic preserves strict and inclusive boundaries even for overflowing
rolling sums and subnormal prices. Default lookbacks are 13, 12, and 12 bars.

MorningStarCandle and EveningStarCandle require a long first body, a short middle
body separated by a strict body gap, and a final opposite body above its prior
short-body mean. MorningDojiStarCandle and EveningDojiStarCandle replace the middle
short-body threshold with one tenth of the preceding mean range. The middle
threshold is inclusive; both outer-body thresholds and final penetration are
strict. Penetration defaults to 0.3, accepts finite nonnegative values, and may
exceed one because the final close need not remain inside the first body. Independent
periods have lazy storage and exact rolling comparisons, including exact products
for penetration. Default first-valid index is 12 for all four patterns.

AbandonedBabyCandle applies the doji-star reversal body and penetration thresholds
in either direction, but requires strict full high/low gaps on both sides of the
middle doji. TristarCandle compares all three dojis against the same range threshold
preceding the first candle; a strict middle-body gap followed by a retreat of the
last body signals reversal without requiring a second complete gap. TasukiGapCandle
requires the opposite final body to open strictly inside the second body and close
strictly inside the gap. Its body-size difference must be strictly below one fifth
of the range mean preceding the second candle; the first candle color is irrelevant.
UpDownSideGapThreeMethodsCandle instead requires the first two colors to agree,
with the opposite final close strictly inside the first body. Its lookback is two;
default Tasuki, Tristar, and Abandoned Baby lookbacks are seven, twelve, and twelve.

BreakawayCandle requires a long first body, a strict body gap, two further candles
with strictly continuing highs/lows, and an opposite fifth close inside the gap.
The third color is unrestricted. ConcealingBabySwallowCandle requires four black
candles and reports a bullish signal: the first two have strictly very short
shadows; the third gaps down, with an upper shadow strictly above its threshold
and extending into the prior body; the fourth strictly engulfs its full range.
LadderBottomCandle requires three black candles with declining opens/closes,
a fourth black candle with an upper shadow above its threshold, and a white final
candle opening above the fourth body and closing above its high. A final doji can
qualify. Their default lookbacks are fourteen, thirteen, and fourteen.

The four star reversals and Abandoned Baby have a documented penetration boundary
difference. TA-Lib rounds the binary64 product and then the threshold, while Ooples
evaluates the supplied binary64 parameter exactly. With a body of 5 and penetration
0.3, TA-Lib rounds the product to 1.5; the exact product is slightly smaller. A close
exactly at the rounded threshold can therefore produce different strict signals.
A separate fixed-grid integer oracle verifies Ooples; an explicit rounded-operation
oracle verifies TA-Lib. The shared verifier checks every value, repeat invocation,
and buffer against the appropriate independent reference. Regression tests pin
both signals and prove that other output corruption still fails verification.

HikkakeCandle detects an inside candle followed by a strict directional high/low
breakout. ModifiedHikkakeCandle requires two inside candles and an inclusive near
close on the first inside candle, using one fifth of its preceding mean range.
Both return signed 100 for formation and signed 200 for strict confirmation within
the following three bars. Confirmation occurs once; a new formation takes priority
and replaces pending confirmation. Formations are tracked during the three bars
before visible output, so the first visible value can be a confirmation. Default
lookbacks are five and ten; the modified near period is configurable. State uses
bounded confirmation age rather than an ever-growing bar index.

RisingFallingThreeMethodsCandle requires long first/final bodies surrounding three
strictly short opposite bodies that overlap the first candle's range. Reaction
closes move against the initial direction; the final open and close strictly
resume it. Partial overlap is sufficient. MatHoldCandle requires a white long
first body, a gapped black short second body, two declining short reaction bodies
inside the permitted penetration, then a white close above all reaction highs.
The third/fourth colors are unrestricted, and the final body need not be long;
a final doji can qualify. Both have independent long/short windows and a default
lookback of fourteen. Mat Hold accepts finite nonnegative penetration, defaulting
to 0.5. It also has a pinned exact-versus-rounded boundary: with a first body of 5
and penetration 0.1, a reaction minimum at 6.5 can pass exact arithmetic and fail
TA-Lib's rounded threshold. Both outputs have independent references.

## Verification and performance

```sh
dotnet test benchmarks/OoplesFinance.StockIndicators.CompetitorTests -c Release -p:GeneratePackageOnBuild=false --filter "FullyQualifiedName!~FullTrajectoryMatchesIndependentFormula"
# Run the shard CLI below for SHARD=0 through 19 concurrently, then check the union.
dotnet benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0/OoplesFinance.StockIndicators.CompetitorBenchmarks.dll --verify-library-shard SHARD 20 results/shard-SHARD.json
python scripts/verify-competitor-shards.py benchmarks/competitor-library-manifest.json results 20
dotnet run --project benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks -c Release -- --filter '*LibraryPairBenchmarks*'
```

Sparse outputs carry explicit presence flags. A missing result requires both a
false flag and a NaN placeholder; an unmasked NaN remains a failure. Presence
flags receive the same retained-buffer and poisoning checks as numeric arrays.
Only present mature numbers contribute to the reported checked-value count.

Independent formulas check every mature value and named output, returned-buffer
isolation, repeated execution, adversarial fixtures, and both actual benchmark
inputs (1,000 and 10,000 bars). The permanent workflow runs on stacked PRs, builds
once, checks the API inventory, and discovers contract methods from the compiled test
assembly. Contracts and full trajectories each run across up to 20 parallel shards.
Each contract shard requires passed TRX evidence for every assigned method; failed,
skipped, missing, and unexpected methods fail the job. The final gate requires all
contract shards and the exact complete trajectory union before performance starts.

Separate BenchmarkDotNet ShortRun jobs reuse those binaries across up to 20
parallel shards. Both arms at both sizes require at least three timing samples;
missing or failed cases fail CI. Benchmark setup checks both timed delegates once against the reference; buffer
isolation and repeated invocation remain fully checked by correctness shards.
Inputs are prepared outside measurement; public
runtime work and returned output allocation are included. Initial measurements
need longer follow-up before precise performance claims.

1,036 measured timing cases across 259 pairs are committed under
`results/2026-10-06-library-comparisons`. Raw reports retain observed
differences without universal speedup claims. Every currently paired family has
recorded timing evidence.

The eight percentile candles classify body or shadow lengths using a current-inclusive
window and linearly interpolated quantiles. Public constructors honor period and
percentile parameters. Production compares exact ranks, while the independent
contract reference sorts rational lengths and interpolates the threshold.

## Competitor limitations and remaining work

Executable tests confirm that 15 Trady candle APIs throw NotImplementedException.
Thirteen now link to independently verified TA-Lib counterparts: LongLeggedDoji,
Marubozu, SpinningTop, Hammer, HangingMan, InvertedHammer, ShootingStar, HaramiCross, StickSandwich, PiercingLine, UpsideGapTwoCrows, ThreeBlackCrows, and ThreeWhiteSoldiers.
ShortShadow and Stars now have the explicitly owned Ooples definitions below.
Throwing stubs have no native formula or timing comparison; the manifest
distinguishes alternate package mappings from owned definitions. Executable
package-availability tests keep these limitations checked.

Trady percentile candles recompute the whole percentile series for each candle.
Their full 10,000-bar verification is expensive. Use parallel shards without an
additional serial trajectory replay. CI allows 20 minutes per correctness shard
and 60 per performance shard, and prints per-pair progress and elapsed times.

Trady BearishLongDay, BullishShortDay, and BearishShortDay ignore their period and
percentile constructor arguments. Executable tests pin this behavior; comparison
runners use the effective 20-bar default configuration for Ooples and the reference.
Trady LongLowerShadow actually checks strictly below the 25th percentile, so its
counterpart is explicitly named LowerShadowBelowPercentileCandle. All these
limitations appear in the manifest. LongDay, ShortDay, BullishLongDay, and both
shadow patterns honor their parameters and are compared at multiple periods.

Implement every remaining mapping, including all candle families, and collect all
paired performance evidence before marking this full-library PR complete.

Trady Tasuki gaps use a high/low trend ending at the middle candle. Upside
requires a final open strictly inside the preceding body but allows its close to
cross the entire gap. Downside requires a close strictly inside the full-range
gap and does not restrict the final open. Trady ignores sizeThreshold for both;
object, generic, and tuple tests pin these asymmetric definitions.

Trady abandoned babies combine a high/low trend, two percentile-long outer bodies,
a strict body/range doji, and strict full-range isolation gaps. The bullish body
honors the requested window and percentile; the bearish body uses Trady’s fixed
20-bar 0.75 percentile. Both use the current candle in their percentile windows.
No midpoint penetration test is part of this definition.

Trady Three Methods searches backward for the newest rejection barrier or long
anchor, rather than requiring five candles. The Ooples state retains that anchor
and its following candle; an independent backward scan verifies the equivalent
result. Falling long bodies use Trady’s fixed 20-bar 0.75 percentile; short bodies
honor the requested settings. Overlapping short/long percentiles preserve
rejection precedence.

`LibraryPairBenchmarks` fixes its in-process job to a bounded 30-minute execution
timeout per case. CI uses this configuration without overriding the toolchain.
Timing jobs use one invocation per iteration and an unroll factor of one for
both arms, retaining three warmup and three measured ShortRun iterations. This
bounds repeated calls to competitors with expensive implementations; recorded
samples and confidence intervals retain the resulting timing uncertainty.

Trady trend-qualified stars use percentile outer bodies and a strict relative
first-body midpoint target. Separate independent references verify exact Ooples
and rounded native decimal comparisons. Zero midpoints return no match in
Ooples; native Trady can throw. The misspelled MoringinDojiStar tuple API is
covered separately.

## Explicit conventional definitions and additional contracts

`ConventionalStarCandle` and `ShortShadowsCandle` are Ooples definitions for the
two remaining Trady stubs. The star requires a preceding body at least half its
range, a current body at most one quarter its range, and a strict body gap in
either direction. Both ranges must be positive; shadows may overlap.
ShortShadows requires both shadows to be at most one tenth of a positive range.
Equality qualifies at the size thresholds. Independent rational references,
golden boundaries, extreme/subnormal prices, and lifecycle checks verify both.
No native parity or timings are claimed for these unimplemented Trady APIs.

`PriceCandlePattern` preserves Skender's price-relative Doji and range-relative
Marubozu definitions, including zero-range fractions of one and bearish
Marubozu signals on zero-range candles. Its exact supplied-price/percentage
comparisons can differ from Skender's rounded decimal/binary ratios; separate
references and a pinned boundary regression verify each contract. Price
presence is explicit. Native metadata/sorting is tested; timed inputs use the
same preordered bars for both libraries.

`WindowExtremeIndex` chooses the newest exact tie and reports absolute indices.
`WindowRangeMidpoint` supports close extrema or high/low extrema and computes
their mean without overflowing the intermediate sum. Both grow storage lazily.
Ooples supports period one; the pinned TA-Lib functions reject it. Native
midpoint overflow is pinned independently from the finite Ooples result.

`BalanceOfPowerWithValidity` uses a simple average of complete valid windows.
A zero-range candle makes the value missing until it leaves the window.
TA-Lib's raw formula uses zero for that candle and reuses the existing public
`BalanceOfPower` implementation. Both definitions have independent trajectories.

Accumulation/distribution now covers TA-Lib, Skender, and Trady. Skender includes
the multiplier, flow volume, cumulative line, and optional simple average with
explicit presence. Trady starts with the first volume, adds price-change-based
flow on flat candles, and stays missing after an undefined flat-candle ratio.
Its counterpart is `PriceAdjustedAccumulationDistribution`; the ordinary detailed
line is `AccumulationDistributionWithAverage`. TA-Lib reuses `Adl`.

These timing fixtures use a common 1/1024 price grid, exactly representable in
both decimal and binary64. Unrounded generated workloads remain correctness
stress tests. Separate integer-grid oracles model exact Ooples arithmetic,
native binary64 stage rounding, and decimal package inputs. A pinned one-ULP
price-range fixture demonstrates input collapse during decimal conversion;
corruption tests ensure these separate contracts cannot hide wrong outputs.

`RollingPercentile` covers QuanTAlib and Trady median and interpolated percentile.
Ranks run from zero to one, using rank = percentile*(window count-1). Exact
binary64 rank and convex interpolation avoid overflowing opposite-sign differences
or same-sign sums. Its optional startup average matches QuanTAlib's incomplete
windows; Trady comparisons start at the first complete window. Endpoint and
interior ranks, odd/even windows, duplicate expiration, subnormal rounding,
lifecycle, lazy large periods, and Trady generic/tuple entry points are verified.
Separate references retain native binary64 rounding and decimal input conversion.
QuanTAlib Percentile rejects period one; the Ooples counterpart supports it.

`WilderMovingAverage` covers Skender/QuanTAlib Smma and Trady ModifiedMovingAverage.
Its coefficient is 1/period. The default seed uses the first full-window mean,
with expanding means during startup; its first-value option begins the recurrence
immediately. Each convex update is rounded once and preserves finite extreme and
subnormal inputs. Independent references verify both seeds and package rounding;
period one, maximum periods, lifecycle, and Trady generic/tuple routes are covered.
QuanTAlib Rma remains pending: its pinned implementation uses a divergent recurrence,
so it must not be treated as another name for this conventional Wilder average.

`SeededAverageTrueRange` covers ordinary TA-Lib and Trady ATR.
`AverageTrueRangeWithDetails` supplies all three Skender outputs and presence flags.
The initial candle only establishes previous close; the first average is the mean
of true ranges 1..period. Each later update uses Wilder smoothing. Unpublished
ranges retain extended upper exponents until averaging, so an oversized range can
still produce a finite scalar ATR. Detailed outputs reject unrepresentable published
true ranges or percentages. Percentage is signed 100*ATR/close, absent only when
close is zero or the average is not ready. Later nonzero closes recover normally.
Independent references cover exact Ooples stages and native binary64/decimal formulas.
Skender rejects period one; the Ooples, TA-Lib, and Trady scalar forms accept it.

TA-Lib Natr is covered by the separate normalized comparison below. QuanTAlib Atr has a distinct bar-route defect documented below.

QuanTAlib 1.0.0 `Atr.Calc(TBar)` does not update inherited `Input.IsNew`, yet passes
that stale false flag to its internal EMA. The EMA restores zero state for every
bar. Its normal bar stream therefore returns `(1/period)*trueRange`, including
high-low on the first candle. Executable direct and event-source regressions pin
this behavior; `ScaledTrueRange` is its explicitly named counterpart. This is not
temporal smoothing. Conventional seeded Wilder ATR is available separately.
Manually setting the package's inherited Input property can change its behavior;
the comparison exercises the normal documented bar route without such intervention.
The scaled counterpart divides the exact range before final rounding, preserving
finite results across overflowing intermediate ranges. Independent references
model this contract and the native binary64 multiply/divide stages separately.

`NormalizedSeededAverageTrueRange` computes signed 100*seededATR/close, with zero
for zero-close bars and during startup. It always normalizes period one and never
revises earlier results. Extended unpublished ranges and averages allow finite
percentages even when raw ATR cannot be represented. The TA-Lib Natr comparison
uses separate native and conventional references: native period one returns raw
true range, and later zero closes overwrite the first output while leaving the
current slot untouched. The adapter explicitly zero-initializes its output buffer;
a sentinel regression also exposes that unwritten slot. Prefix-stability and
corruption tests prevent those differences from hiding unrelated errors. Timings
use ordinary positive-price workloads where both implement normalized ATR.

`WindowLinearRegression` covers TA-Lib LinearReg, LinearRegSlope,
LinearRegIntercept, and Tsf, plus Skender Epma. It selects one independently rounded
endpoint, slope, window-local intercept, or one-step forecast from the existing
exact fit engine. The new local intercept uses x=0 at the oldest candle; the
existing LinReg global-series intercept remains unchanged. Only the selected
reading is published, so an unrepresentable slope cannot invalidate a finite
endpoint. Independent centered rational and batch integer-grid oracles cover
startup, window expiration, local coordinates, extreme/subnormal inputs, lazy
maximum periods, lifecycle, and corruption. These native APIs require at least
two observations per window; the Ooples counterpart also defines period one.

`NormalizedConvolution` covers QuanTAlib Convolution with immutable copied,
newest-first weights. Each available prefix is normalized by its exact mass;
zero mass divides the signed weighted response by available count. This does not
replace the weights with equal weights. `EndpointWeightedAverage` covers QuanTAlib
Epma using fixed 2*period-1-3*lag weights, including startup. Only the complete
window equals a regression endpoint; partial windows differ from partial OLS.
Independent references model exact Ooples ratios and native coefficient and
normalization rounding, including nearly cancelled kernel mass. Signed/zero
kernels, direction, copied settings, extreme/subnormal ratios, lifecycle, lazy
maximum endpoint periods, and corruption are verified.

`EndpointWeightedAverage(period, true)` also covers QuanTAlib Mma's expanding-mean
startup. At a complete window its weighted-moment formula equals the regression
endpoint, not Wilder smoothing. Both endpoint startup modes now use exact rolling
moments, avoiding repeated window scans. Independent weighted references retain
coverage of every original fixed-weight configuration. A full 46,341-bar native
regression proves the package's Int32 denominator overflow reverses the correction;
the Ooples endpoint remains correct. Period one is supported by Ooples but rejected
by native Mma. Both Mma and Epma were remeasured after the implementation change.

`WindowRegressionStatistics` supplies all five QuanTAlib Slope readings: slope,
one-based window-local intercept, population standard deviation, R-squared, and
the fitted endpoint, each with a presence flag. Available history is used during
startup. Only slope exists on the first observation. R-squared is undefined on a
flat window; QuanTAlib instead retains its previous R-squared, and its independent
comparison reference explicitly models that difference. Exact rolling moments
preserve variance and correlation for subnormal inputs and avoid intermediate
overflow. The production validation reference uses centered rational regression;
the competitor comparison uses a separate decimal batch calculation.

`RegressionSnapshot.Calculate` supplies Skender Slope's complete batch output.
Intercepts use global one-based coordinates. Its Line field overlays the final
window's fit on the final period rows, including rows whose own rolling statistics
are still in startup. Appending input changes that overlay. This retrospective
contract is separate from the causal window-local indicator. Flat-window
R-squared is absent. Unrepresentable published statistics are rejected; oversized
unpublished startup readings do not invalidate later finite results.

`WindowLinearRegression` also selects slope angle in degrees for TA-Lib
LinearRegAngle. It applies arctangent to the rounded slope. An oversized unpublished
slope still produces a finite signed 90-degree angle; period-one angle is zero.

`WindowDispersion` selects variance, standard deviation, or the latest close's
Z-score from the current window's own mean. It supports sample (count-1) and
population (count) denominators, available-history startup, and a signed finite
multiplier applied before final rounding. One observation and constant-window
Z-scores are zero. Exact rolling moments preserve tiny spreads; normalization and
scaling happen before any potentially overflowing or underflowing publication.
History grows lazily, including maximum-period configurations.

QuanTAlib Variance and Stddev default to sample statistics and also offer population
mode. Zscore always uses sample deviation. TA-Lib Var/StdDev and Trady
StandardDeviation use population statistics with full-window startup. TA-Lib's
uncentered mean(x*x)-mean(x)^2 arithmetic can lose the variance of a large-offset
series entirely; its comparison has a separate staged binary64 reference, while
Ooples is checked against an independent centered reference. TA-Lib Var accepts
period one, StdDev requires two, and both reject one-bar inputs at their range
validator. QuanTAlib requires at least two; Trady and Ooples support period one.
Trady's object, generic, and tuple routes are compared on finite inputs. Its
nullable decimal values and output conversion retain their native startup rules.

`StandardDeviationWithDetails` covers all four Skender StdDev outputs and their
presence flags: population deviation, mean, Z-score, and optional deviation SMA.
The price window must be complete; smoothing then requires its own full window
of published deviations. Disabled smoothing stays absent, and a period of one
reproduces the deviation output. Both histories grow lazily without adding their
periods in an integer counter. Exact zero variance makes Z-score absent; nonzero
subnormal variance remains defined even if the published deviation rounds to zero.

Skender's floating mean can create a nonzero deviation and defined Z-score on a
constant nonbinary series such as 0.1. Decimal quote conversion can instead collapse
distinct binary64 prices into a constant series. Separate exact-grid and staged
native references cover both differences; the independent square-root oracle
certifies its estimate against exact adjacent-double midpoints. Skender also
throws OverflowException when its checked lookback+smoothing addition overflows,
even when the input is too short to produce any complete window. The Ooples counterpart uses actual window counts.

`WindowMeanAbsoluteDeviation` and `MovingAverageDiagnostics` cover TA-Lib AvgDev
and Skender SMA analysis. The detailed output includes the mean, absolute error,
squared error, signed relative absolute error, and four presence flags. Errors
are centered on the published rounded mean: for prices 2^53 and 2^53+2, the
published mean is 2^53 and squared error is 2, rather than population variance 1.
Relative error averages abs(price-mean)/price with signed denominators and no
factor of 100. Zero prices make only relative error absent, with recovery after
the zero expires. Exact intermediate ratios retain finite cancellation even when
individual terms exceed binary64 range; unrepresentable published outputs are
rejected. Scalar absolute deviation does not compute unselected error outputs.
Both histories grow lazily, including maximum periods. The scalar publishes
available history; detailed outputs require a full window. TA-Lib requires period
two, while the Ooples counterparts and Skender support one. Separate native
references retain summation order and decimal quote conversion; finite quote,
tuple, and chained Skender routes receive matching field checks.

`PriorPriceChannel` supplies the full preceding-window channel for Skender
Donchian: upper, lower, center, width, and explicit presence for each field. The
current candle is excluded, and the first reading follows a complete preceding
window. Width divides the range by the exact midpoint before rounding; an exact
zero midpoint makes width absent, while an underflowed published midpoint does
not. True negative extrema are preserved. Skender initializes its highest price
to zero, clamping all-negative high windows; its independent native reference
retains that defect and its decimal arithmetic. Both native behavior and the
Ooples formula have direct regressions.
Skender's decimal midpoint addition also overflows on constant decimal.MaxValue
prices even though the midpoint itself is representable; a direct boundary test
pins that limitation.

`WindowPriceRange` supplies Trady's highest-high minus lowest-low difference,
with available-history startup aligned to Trady's first complete window. All
three native object/generic/tuple routes are checked. Trady returns missing values
for nonpositive periods, while Ooples rejects them. Both new public counterparts
grow storage lazily, and the runtime rejects unrepresentable final outputs.

Skender Williams %R and TA-Lib WillR use the existing exact `WilliamsR` public
counterpart. Skender computes stochastic minus 100 and returns -100 for flat
windows. TA-Lib divides by a range already scaled by -100 and returns zero when
that denominator is zero, including after underflow. Its overflowing range can
also turn a finite result into zero. Separate staged native references retain
these differences, and direct flat/subnormal/overflow tests verify them. Ooples
preserves the complete ratio before rounding and returns -100 on flat ranges.
TA-Lib requires period two; Skender and Ooples support period one.

`WindowAroon` covers five Aroon families with explicit window and equal-extreme
conventions. TA-Lib uses period+1 current-inclusive candles and the newest equal
extreme; Skender uses that window with the oldest equal extreme; Trady uses
period candles with the newest equal extreme. All three Ooples values and their
presence flags require a complete window. Percentages and oscillator are rounded
independently from exact ratios, and the period+1 size uses a wide counter so
maximum periods do not overflow or allocate eagerly.

The Skender maximum search starts at price zero and an index before the series,
giving invalid negative Up values on nonpositive high windows. Its oracle retains
that defect, while Ooples retains the true extrema. Skender's oscillator subtracts
published binary64 components, Trady subtracts published decimal components,
and TA-Lib multiplies an already rounded factor by the index difference. Separate
references and regressions pin these differences. Native Trady object/tuple
routes and its public generic oscillator route are verified. TA-Lib requires
period two; Skender, Trady, and Ooples support period one.

`VolumeWeightedPrice` covers Skender VWAP/VWMA and Trady VWAP. It supports
cumulative history or a complete rolling window, typical price or close, and an
inclusive date cutoff. Exact price-volume products and total volume are retained
until the final ratio; typical price is not rounded before weighting. Zero total
volume is explicitly absent and can recover. Rolling history grows lazily;
cumulative mode uses constant storage. Skender rejects an anchor before the first
nonempty quote series but accepts a future anchor with absent outputs. Ooples
treats earlier anchors as including all history. Trady throws for zero total
volume and can overflow its decimal products despite a finite weighted price.
Its cumulative and rolling modes and both object/tuple routes are verified.

`MoneyFlowWithDetails` supplies all three Skender CMF fields: multiplier, flow,
and an unscaled, unclipped CMF ratio. Multiplier and complete flow are rounded
separately. CMF uses the exact sum of those published flows divided by exact total
volume; it does not use a sum of unrounded theoretical flows. Zero range yields
zero flow, and zero volume makes only CMF absent. Native binary64 stages and
decimal quote conversion have separate references; collapse of small ranges or
volumes is pinned by direct regressions. Wide and subnormal sums normalize before
rounding. All published fields, presence, and final-overflow rejection are checked.

`SeededForceIndex` supplies Skender's SMA-seeded EMA of volume-weighted price
changes. The seed requires Period changes after the first candle. Raw products
and seed sums remain exact even when individually unrepresentable, and each
published recurrence is rounded once. Skender's staged recurrence can lose a small
change even at period one; its checked period+1 also overflows at int.MaxValue.
Ooples uses a wide denominator and constant storage, with boundary regressions.

`VolumeConditionedIndex` supplies Trady's positive/negative volume indices, seeded
at 100. Only strict volume changes select a price-ratio update. The previous-price
denominator retains its sign, and held readings still advance the previous price.
Ooples rounds the complete index-times-price ratio once; Trady's decimal quote
conversion can turn strict volume changes into ties, and its price difference
can overflow before a finite index. A selected zero previous close throws in
Trady and makes the Ooples index absent until reset. Native object/generic/tuple
routes, exact signed/subnormal/overflow cases, and both arms' corruption checks
are covered.

`ReverseWilderAverage` is the explicit counterpart of QuanTAlib 1.0.0 Rma's
implemented recurrence. Despite the native Wilder-smoothing documentation, its
post-seed update is previous+(previous-close)/period: it extrapolates away from
the new price and can diverge even on bounded inputs. Conventional smoothing is
available through `WilderMovingAverage`. The reverse indicator's seed uses an
expanding mean of the previously rounded value, rather than an exact prefix sum.
Each complete owned update rounds once; the native staged arithmetic has a
separate oracle. Constant storage, extreme/subnormal values, period-one behavior,
source subscription, last-value revision/reset, and overflow rejection are tested.
Native period*2 warmup metadata wraps at maximum periods; the owned implementation
avoids that arithmetic.

`CandleArithmetic` covers TA-Lib Add, Sub, Mult, and Div with independent selection
of each OHLC operand. Comparisons use close on the left and open on the right.
`PriceRoundingTransform` covers Ceil, Floor, and Sqrt on close. These stateless
operations have no warmup. Rational production references and independent
integer-grid arithmetic, integer quotient rounding, and certified square-root
midpoints check complete outputs, including subnormal values.

Division by zero and the real square root of a negative input are absent in
Ooples and recover on the next admissible input. TA-Lib publishes infinity or
NaN for those requests; the adapter preserves these as invalid present values,
so the comparison verifier cannot silently accept them as missing. Final
arithmetic overflow is rejected by the owned runtime. Timings use finite,
defined inputs. TA-Lib rejects empty or one-element spans, whereas the owned
indicators support a single candle. Float/double and in-place routes, inclusive
requested range ends versus exclusive returned ends, short output buffers,
field selection, chaining, and corrupted values/presence have direct regressions.

`FixedPeriodWma` matches QuanTAlib's available-prefix weights: the newest close
has weight Period, its predecessor Period-1, and so on. Ordinary expanding WMA
uses different startup weights. The updated Quan WMA comparison retains all
startup values; its earlier warmup-skipping timing report is replaced. DWMA
composes two `FixedPeriodWma` instances through `Of`, rounding the intermediate
series once. Exact running sums support constant-work updates and lazy storage.
Native rounded kernels, prefix normalization, and dot products have a separate
reference, including the unchecked period-product wrap at 65,536. Quan Wma hides
`Init`, leaving convolution history intact through the public reset route and
inside Dwma; source subscription, revision, and reset regressions pin the behavior.

`WindowMode` matches QuanTAlib Mode's expanding-mean startup and full-window mean
of the most frequent distinct values. Exact ties keep adjacent doubles distinct.
Owned means round once and remain finite across overflowing native sums. Native
startup uses SIMD lane sums; mature tied modes are summed in ascending scalar
order. Independent references model both. Quan's inherited `Init` retains its
circular buffer even while resetting the index; the owned reset clears all
history. Maximum periods grow lazily. Tests cover ties and their expiration,
startup transitions, source/revision/reset routes, and corrupted outputs.

## Logarithmic, exponential, and hyperbolic transforms

`PriceTranscendentalTransform` supplies natural/common logarithms, exponential,
and hyperbolic sine/cosine/tangent. Each close is transformed independently and
both `Value` and `IsDefined` are published. Nonpositive logarithm arguments are
absent for that bar and recover on the next positive close. Other finite
arguments are mathematically defined; the runtime rejects final overflow.

Production uses runtime intrinsics. Validation uses independent 192-bit series;
the benchmark oracle uses a separate 256-bit Horner polynomial and logarithm
series. Every comparison uses zero absolute tolerance, relative tolerance
4e-15, and an exact sign check. This prevents tiny nonzero answers from being
accepted as zero. The budget is a test criterion, not a formal platform-wide
proof of libm accuracy.

The boundary fixtures cover adjacent values around one, subnormal logarithm
arguments, exponential underflow at -744/-745/-746, tiny signed hyperbolic
signals, and finite sinh/cosh at +/-710 even though exp(710) overflows. Native
nonfinite answers remain present in the adapter and cannot pass as missing
values. TA-Lib's empty/single-element span restriction, inclusive request versus
exclusive output range ends, float/double overloads, in-place aliases, invalid
ranges/lengths, and corrupted value/presence results have direct checks.

Performance inputs use a bounded varying walk, so tangent is not measured on an
artificially saturated constant. Both libraries return the complete output
trajectory. Undefined native requests are documented and tested separately from
the finite comparable timing workloads.

Validation: 42 focused checks passed (including shared elementary-math and inventory
regressions), followed by 71,713 complete-trajectory values across the six families.
All 24 new timing cases passed setup correctness and the archive verifier.

## Zero-seeded Laguerre and Elder-ray details

`ZeroSeedLaguerreFilter` supplies QuanTAlib Ltma's four-stage, zero-initialized
Laguerre recurrence. Gamma defaults to .1 and accepts finite values in [0,1].
Gamma zero gives the [1,2,2,1]/6 impulse response; gamma one remains zero. All
startup values are included. Complete stages round once with an extended upper
exponent; the final weighted mean rounds once. Seven gamma configurations,
including subnormal gamma and the adjacent value below one, are compared exactly
to separate owned and native rounding references. Native NaN gamma acceptance,
source subscription, revision, and reset behavior have explicit tests.

`ElderRayWithDetails` supplies Skender's EMA, BullPower, and BearPower, with a
shared presence flag. A complete SMA seeds the close EMA. The two powers subtract
the published EMA from high and low and retain their signs. Exact seed and
complete updates avoid intermediate overflow, use constant storage even for the
maximum period, and preserve subnormals. Quote conversion and native sequential
rounding have a separate exact oracle. Distinct OHLC fields, startup, period one,
all output/presence corruption, the native reusable combined value, and genuine
final power overflow are covered.

Validation: 21 focused checks, 52,624 full-trajectory values, and eight paired
timing cases passed. All 924 archived timing cases across 231 pairs pass the
permanent evidence verifier. This does not complete the remaining API families
or the final complete correctness union.

## Expanding regularized and zero-lag EMA

`ExpandingRegularizedEma` publishes two direct seed prices, then applies
alpha=2/(min(observations,period)+1) with lambda regularization. Period one
therefore retains regularization when lambda is nonzero. The complete update is
rounded once, including the normalization by 1+lambda. Finite lambda may exceed
one; nonfinite or negative values are rejected. State uses constant storage.

`ExpandingZeroLagEma` uses 2*close-laggedClose with lag floor((period-1)/2),
clamped to available history, and the same expanding alpha. The synthetic value
is not prematurely rounded or allowed to overflow before the final update.
History grows lazily even for maximum periods. Native QuanTAlib allocates a
full-period buffer, so maximum-period construction is intentionally not run.

Both comparisons require exact agreement with separate full-ratio and native
operator-stage references. Five lambda configurations, odd/even periods, all
startup values, available-lag transitions, wide/subnormal inputs, true final
overflow, and source/revision/reset behavior are covered. Native Rema accepts
NaN and positive-infinite lambda; direct checks retain that limitation.

Validation: 21 focused checks, 34,612 full-trajectory values, and eight timing
cases passed. The archive verifier accepts 932 cases across all 233 currently
paired families. The final complete correctness union remains outstanding.

## Curvature through public composition

QuanTAlib Curvature is compared with `WindowRegressionStatistics(period)` reading
`WindowLinearRegression(period, WindowRegressionOutput.Slope)` through `Of`.
Each first-stage slope is rounded before entering the second regression. Selecting
only the slope avoids evaluating unused first-stage outputs in the production
runtime. Both timing arms use prepared input data.

The comparison includes curvature, intercept, population standard deviation,
R-squared, the fitted endpoint, and all presence flags. Independent integer moments
and midpoint-certified roots verify our composed outputs exactly. A separate
reference models every native operator in both fits, including underflow and
retained R-squared values on flat windows. Our flat-window correlation is absent.

Native `Init` clears the outer history but retains the nested slope's history;
source and revision routes are tested separately. Native `IsHot` becomes true
after period samples although its warmup metadata is 2*period-1. Empty, single,
adjacent-double, subnormal, wide finite, and lazy maximum-period cases are covered.
All five outputs and their presence flags reject deliberate corruption.

Validation: ten focused checks, 63,145 complete-output values, and four timing
cases passed. No production files changed for this batch; verification reused
those binaries. The archive verifier accepts 936 cases across 234 pairs.

## Holt-Winter forecast

`HoltWinterForecast` maintains level, velocity, and acceleration, seeded with the
first close and two zeros. The published forecast is level+velocity+acceleration/2.
Each complete state update rounds once with an extended upper exponent, and the
forecast rounds once. A targeted test makes level and velocity exceed binary64
range and cancel to a finite zero; the native calculation becomes nonfinite.

Constructors support period defaults, three explicit finite factors, and the
native coefficient-only convention that truncates (2-levelFactor)/levelFactor to
a positive signed 32-bit period. Period one overrides factors with (1,0,0).
Factors outside [0,1] remain supported and may diverge. Nonfinite factors and
invalid derived periods are rejected. The default period divisor is evaluated
widely; pinned native int.MaxValue wraps its integer period+1 denominator.

Separate exact references model complete owned stages and individual native
operators. Six factor settings, three derived-period settings, maximum periods,
subnormal half-terms, extreme finite inputs, genuine final overflow, source,
revision, reset, and corrupted outputs are covered. Native nonfinite parameter
acceptance and overflowing period-one differences are retained as limitations.

Validation: 16 grouped focused checks plus one targeted extended-stage check,
28,112 complete-trajectory values, and four timing cases passed. The targeted
check reused unchanged production and benchmark binaries; timings were not
repeated. All 940 archived cases across 235 pairs pass the evidence verifier.

## Normalized window entropy

`NormalizedWindowEntropy` computes normalized Shannon entropy from exact close
frequencies over available rolling history. Its explicitly documented QuanTAlib
convention returns one for a single distinct value, including the first bar.
Signed zeros share a group; adjacent doubles remain distinct. Window storage grows
lazily, and the calculation is independent of close magnitudes. Compensated
logarithms preserve rare-event entropy in nearly constant windows.

Independent fixed-point logarithms and sorted frequency runs verify complete
outputs. Seven focused checks cover unequal frequencies and expiration, signed
zero/subnormal/maximum values, maximum-period lazy storage, native source,
revision, reset and hot flags, and corrupted values/presence on both arms.
The trajectory run checked 13,808 values. Four timing cases are archived; all
944 cases across 236 paired families pass the performance evidence verifier.

## Gaussian and sine weighted windows

`GaussianWeightedAverage` and `SineWeightedAverage` pair QuanTAlib Gma and
Sinema with fixed newest-first kernels and available-prefix normalization.
Gaussian here means Gaussian coefficients, not the geometric mean. Coefficients
use documented binary64 elementary operations; each complete weighted ratio
rounds once. Owned weights and history grow lazily, including maximum periods.
Gaussian rejects its undefined period-one case and zero/nonfinite width; finite
negative widths are equivalent to positive widths. Tiny widths avoid the native
zero-denominator NaNs; prefixes whose weights all underflow publish zero.

The independent oracle models the native full-kernel normalization followed by
prefix normalization separately from owned unnormalized coefficients. Fixed-point
sine and exponential series check the coefficients; integer-grid arithmetic checks
weighted responses. Both native wrappers retain their nested convolution history
through public Init, which is explicitly tested and documented.

All 32 focused/shared convolution checks passed, including lifecycle, source,
revision/reset, odd/even and unit periods, width variants, extreme/subnormal
prices, lazy maximum periods and corrupted coefficients/outputs. The two complete
trajectory runs checked 32,906 values. Eight timing cases are archived; all 952
cases across 238 pairs pass the evidence verifier.

## Arnaud Legoux startup conventions

`ArnaudLegouxWindow` pairs Skender's full-window-only ALMA and QuanTAlib's
available-length ALMA. Exact squared-distance differences shift Gaussian
logweights to a zero maximum before rounding exponents and applying binary64
exp; the final weighted ratio rounds once. At least one weight remains one,
avoiding all-weight underflow. Exact center ties are preserved even at extreme
widths. Finite out-of-range offsets and zero/negative sigma cover Quan's broader
parameter conventions; zero sigma gives the uniform mean. History grows lazily.

Independent references model native unshifted coefficients and staged arithmetic
separately. Skender's final NaN is absent; Quan's remains present and is rejected
by the correctness verifier, with that rejection explicitly regression-tested.
This native extreme-width limitation is not treated as a successful finite pair.
Quote conversion, tuple/reusable routes, source, revision/reset, both startup
rules, tiny/wide prices and lazy maximum periods are covered. Sixteen focused
checks passed and complete trajectories checked 44,457 values. Eight timing
cases are archived; all 960 cases across 240 pairs pass the evidence verifier.

## Hull period and startup conventions

`HullWindow` pairs QuanTAlib's immediate fixed-period prefixes, Skender's
full-window floor periods, and Trady's full-window nearest periods (including
even half-period ties). Each stage rounds once. Unpublished synthetic values
keep binary64 precision with an extended upper exponent, preserving finite final
outputs after oversized intermediate values. Final overflow remains an error.
Lazy rolling moments give constant update work without eager period allocation.

Native stage rounding is modeled independently, including Quan's twice-normalized
convolution weights, Skender's per-term binary64 division and Trady's decimal
per-term division. Quan's reset retains nested histories; overflowing synthetic
input invokes its convolution fallback. Trady's degenerate periods zero/one
produce zero/negative-price outputs, while the owned indicator requires period
at least two. Trady nullable closes contribute zero with fixed weight divisors;
callers comparing such inputs must map those missing closes to zero explicitly.

Twenty-five focused/shared checks passed, covering lifecycle, all three period
rules, quote/tuple/reusable/mapper/range routes, source/revision/reset, extreme and
subnormal prices, lazy maximum periods, and value/presence/convention corruption.
Complete trajectories checked 41,170 values. Twelve timing cases are archived;
all 972 cases across 243 pairs pass the performance evidence verifier.

## Moving-average percentage envelopes

`WindowAverageEnvelope` covers all nine Skender modes: ALMA, DEMA, EMA, EPMA,
HMA, SMA, SMMA, TEMA and WMA. Existing average states feed centerline, upper and
lower outputs with shared explicit presence. Upper and lower formulas round once
from the published centerline, preserving near-100-percent residuals. Negative
centers retain the native labels without sorting. Histories grow lazily and
chaining feeds the selected average. Nonfinite percentages are rejected; native
NaN/infinity validation holes remain documented limitations.

Independent references model each centerline and native staged percentage
arithmetic. Sixty-three focused/shared checks passed, including lifecycle,
quote/tuple/reusable routes, minimum periods, tiny/wide values, large percentages,
lazy maximum periods and every output's value/presence corruption. Complete
trajectories across all nine modes and width variants checked 184,914 values.
Four envelope timing cases use the default SMA mode and 2.5 percent; individual
averaging families have separate timing evidence. All 976 archived cases across
244 pairs pass the performance evidence verifier.

## Circular and inverse circular transforms

`PriceCircularTransform` covers sine, cosine, tangent and their inverse functions
in radians. It exposes value and domain presence; inverse sine/cosine outside
[-1,1] produce absent values and recover on the next defined input. Native NaNs
remain present and fail comparison rather than being silently masked.

Validation uses a 2,560-bit Machin pi identity, nearest-quadrant reduction and
forward series. The benchmark oracle independently uses a 3,072-bit two-angle
identity, modular reduction and backwards polynomials. Both retain tiny phase
residuals and verify zero absolute / 4e-15 relative tolerance with exact sign.
Eleven focused checks passed, including maximum finite magnitudes, exponent
sweeps, binary64 pole neighbors, subnormals, inverse endpoints, lifecycle,
chaining, float/double aliases and ranges, and value/presence corruption.
Complete trajectories checked 71,734 values. All 24 new timing arms passed;
the archived union now contains 1,000 measured cases across 250 pairs.

## Awesome oscillator with normalized output

`AwesomeWithDetails` publishes the full-window midpoint SMA difference and
100*oscillator/currentMidpoint, with independent presence flags. Both wait for
the slow window; a zero midpoint leaves only the normalized field absent.
Midpoints, averages, differences and normalized ratios round separately without
unpublished overflow. Chaining explicitly uses the upstream value as the price,
because the builder replaces close while preserving the original high/low.
History grows lazily, including maximum-period startup.

Independent exact references distinguish owned arithmetic from native decimal
midpoint conversion and staged operators. Nine focused checks passed, including
quote/tuple/reusable routes, startup/domain flags, source chaining, lifecycle,
subnormal and maximum values, genuine final overflow and every output's corruption.
Four period configurations checked 28,082 trajectory values. Four new timing
cases passed; the archived union is 1,004 cases across 251 pairs. Native decimal
HL2 addition can overflow before averaging; that limitation is explicitly tested.

## Delayed Alligator lines and Gator distances

`AlligatorWithDetails` uses three SMA-seeded Wilder midpoint averages, each
published after its positive delay with independent presence. No future rows are
appended. `GatorWithDetails` reports absolute jaw/teeth distance, negative absolute
teeth/lips distance, and both expansion decisions with independent presence.
Expansion uses strict published-value comparisons. Each decision waits for a
present previous distance; a missing current distance then reports false.
`GatorWithDetails.FromLines` also accepts supplied nullable Alligator samples,
retains their enumeration order and timestamps, and starts fresh on enumeration.

Midpoints and complete recurrence updates round once without intermediate
overflow. Delay queues grow lazily. Chaining uses upstream prices as midpoints.
Parameter-order checks use wide sums; Skender instead throws OverflowException
when its int period-plus-offset addition overflows. Native custom Gator can also
publish infinite distances, which the owned API rejects.

Sixteen focused and ten shared Wilder checks passed. Independent exact native
and owned references checked 95,069 trajectory values across three configurations.
Coverage includes quote/tuple/reusable/custom-line routes, staggered startup,
missing values, exact ties, tiny and maximum values, maximum periods/delays,
chaining/lifecycle and every output/presence corruption. Eight timing cases
passed; all 1,012 archived cases across 253 pairs pass the evidence verifier.

## Heikin-Ashi complete OHLCV candles

`HeikinAshiCandles` publishes open, high, low, close and unchanged volume from the
first candle. Close is the once-rounded full OHLC mean; initial open averages raw
open/close, then follows the mean of the previous published open and close.
High and low include both transformed values. Close is the primary output for
chaining. Incoming chaining replaces raw close while retaining other fields.
All means remain finite for finite inputs without overflowing intermediate sums.

Six focused checks and 64,065 full-output values passed independent grid and
native decimal-stage references. Regressions include exact seed/recurrence,
subnormal rounding, maximum magnitudes, lifecycle, incoming/outgoing chaining,
sorted generic quotes, repeated candle transforms and every field/presence
corruption. Native decimal quote conversion erases binary64 subnormals and native
decimal sums can overflow before averaging; neither limitation is hidden.
Four timing cases use distinct OHLCV fields. The archive now has 1,016 verified
cases across 254 pairs.

## Rolling Chande momentum and efficiency ratio

`WindowPathRatio` covers Skender CMO as 100 times signed net movement divided by
rolling path length, and Trady EfficiencyRatio as absolute net movement divided
by the same path length. It waits for a full period of changes. Flat paths are
absent; zero net movement along a nonflat path is a present zero. Exact differences,
path sums and a once-rounded ratio preserve subnormals and movements wider than
binary64. History grows lazily, including maximum-period startup.

`FromValues` handles nullable Trady inputs: both window endpoints must exist,
and unknown adjacent steps contribute nothing to path length. This can produce
an efficiency above one. Enumeration is independent and repeatable; nonfinite
inputs and unrepresentable final ratios are rejected. Native period zero yields
null, while negative periods fail on evaluation; the owned API requires a positive
period. Trady's params-index overload returns one value; the nullable start/end
and IEnumerable-index overloads return sequences, all explicitly checked.

Fifteen focused checks passed, including quote/tuple/reusable/generic routes,
nullable gaps, window expiration, flat recovery, exact tiny movements, extremes,
source chaining, lifecycle and value/presence corruption. Independent references
checked 23,200 full-output values. Native decimal arithmetic and staged binary64
CMO arithmetic have separate references; native Skender overflowing ratios can
become absent. Eight paired timing cases passed. All 1,024 archived cases across
256 pairs pass the performance evidence verifier.

## Wilder RSI and Chande strength

`WilderStrengthOscillator` covers TA-Lib default RSI/CMO and Skender RSI. It seeds
from a full period of exact changes, then rounds each complete gain/loss update
to 53 significant binary digits. The unpublished means have extended exponents
in both directions, preserving tiny ratios and avoiding overflowing movements.
The bounded final percentage rounds once to binary64. TA flat values are zero;
Skender RSI flat values are 100. Optional unstable periods suppress initial
outputs without changing the recurrence. State and alignment storage are bounded.

Positive dyadic updates retain up to 4,096 alignment bits. The dominant term is
always exact within that bound; a discarded subordinate positive tail is retained
as a sticky bit for midpoint rounding. Independent rational and normalized-ratio
references verify this arithmetic. Fourteen focused tests passed, including
maximum periods/suppression, tiny and huge movements, lifecycle/source routes,
5,000-bar decay, and a 12,000-bar decay followed by a midpoint-sensitive update.
Native double/float, aliases, ranges, invalid buffers, settings and output/presence
corruption are checked with isolated global settings. Complete comparisons
checked 35,830 values; all 12 new timing cases passed.

TA-Lib 0.5.0 Metastock RSI/CMO has a verified compatibility defect: at exactly one
period of input it reads beyond the array; on longer inputs it labels a value one
bar before the data used and returns a shortened range. Independent native
references and direct boundary/future-input regressions pin this behavior. The
owned streaming contract is causal and does not claim parity with this defective
mode. Native gain/loss means can also underflow, and overflowing differences can
produce NaN. These remain explicit limitations. All 1,036 archived timing cases
across 259 pairs pass the performance evidence verifier.

## Nullable strength and lagged momentum

`NullableStrengthOscillator` covers Trady RelativeStrength, RelativeStrengthIndex,
NetMomentumOscillator, RelativeMomentum and RelativeMomentumIndex. Strength uses
Wilder smoothing of one-bar changes; momentum uses EMA smoothing of lagged changes.
All modes omit zero-loss results; Relative Momentum Index also omits zero gain.
The public nullable route skips missing changes while seeding; an empty seed or
any missing change after seeding leaves subsequent results absent. Lag storage
grows lazily, including maximum-period startup.

Unpublished averages retain 53 binary digits with extended exponents. Bounded
indices compute their final ratio directly, avoiding intermediate quotient
overflow; direct strength ratios reject genuinely unrepresentable outputs.
Native decimal seed arithmetic can underflow or overflow. Its rounded decay
can stall both averages at the same floor and alter their ratio, which is pinned
by a 5,000-bar regression. These native limitations remain explicit.

Thirty focused/shared tests passed, including all five conventions, nullable
seeds/gaps, tuple/generic/index/range routes, repeat enumeration, chaining,
lifecycle, extremes, long decay and value/presence corruption. Independent scaled
and decimal-stage references checked 57,596 full-output values. Twenty new timing
cases passed; the complete archive contains 1,056 verified cases across 264 pairs.

## Simple and exponential average differences

`MovingAverageDifference` covers Trady SimpleMovingAverageOscillator and
ExponentialMovingAverageOscillator. It subtracts two close averages, with
independent rounding of each average and their final difference. SMA waits for
both complete windows; EMA seeds both averages from the first price. Positive
equal and reversed periods are supported. SMA history grows lazily and EMA
storage is constant, including maximum-period startup.

Seventeen focused tests passed, covering independent lifecycle references,
tuple/generic/index/range routes, repeated reads, startup, chaining, extreme
prices, genuine final overflow and value/presence corruption. Native SMA with
period zero returns absent values; native EMA permits period zero and fails
during evaluation for period minus one. Our API requires positive periods.
Native decimal conversion can erase tiny prices, and decimal sums, recurrences
or differences can overflow. Separate decimal-stage and integer-grid references
checked 26,398 full-output values. Eight timing cases passed. All 1,064 archived
cases across 266 pairs pass the performance evidence verifier.

## Retrospective DPO and confirmed fractals

`DetrendedPriceSnapshot` covers Skender DPO with both published fields: row i
subtracts the SMA ending at i + floor(period/2) + 1 from close[i]. Startup and
unconfirmed trailing rows remain absent. Appending prices can fill trailing
values; this batch API explicitly exposes retrospective alignment. Owned means
and differences round once and reject genuine final overflow. Native staged
SMA sums can overflow, as pinned by a maximum-magnitude tuple regression.

`FractalSnapshot.Calculate` covers Williams Fractal with independent left/right
spans and either high/low or close endpoints. A selected price must strictly
exceed every wing price; any tie invalidates that extremum. Both extrema can be
present on the same candle. Linear-time monotonic deques preserve exact binary64
comparisons. `ChaosBands` covers Fractal Chaos Bands: symmetric high/low fractals
update independent carried lines only after the right wing is complete.
Span arithmetic is widened, avoiding native FCB's checked 2*span overflow.

Twenty-one focused checks passed: exhaustive independent window/confirmed-center
references, future-data alignment, unchanged confirmed prefixes, all fields and
presence corruption, odd/even and asymmetric periods, native generic/tuple/chain
and sorted routes, empty/short/maximum periods, ties, adjacent prices, subnormals
and extremes. Explicit decimal-conversion regressions show native prices losing
strict fractals retained by our binary64 API. Complete trajectory verification
checked 48,176 values; twelve timing cases passed. The archive now contains
1,076 verified cases across 269 pairs.

## Fifty-seeded stochastic KDJ and differences

`FiftySeedStochastic` covers Trady RawStochasticsValue. It returns fifty before
the complete price window and for exactly flat ranges, then computes the
high/low stochastic percentage. `StochasticSmaKdj` covers Fast, Slow and Full
with configurable K/D smoothing and J=3*K-2*D. Fast uses K period one; Slow uses
three. Full D averages the known K values in its complete calendar window,
skipping startup nulls. This can produce D before a full window of present K
values. Each output has independent presence. `StochasticSmaDifference` covers
the three corresponding K-D oscillators without publishing unselected K/D/J.

All ratios, means and combinations round independently. Unpublished values
retain binary64 precision with an extended upper exponent, using existing
scaled arithmetic, so oversized raw ratios can later average or cancel into
finite results. Only unrepresentable published outputs are rejected. Storage
grows lazily for maximum periods; chaining replaces close and retains high/low.
Native decimal products/sums can overflow, and its difference implementation
evaluates an unselected J that can overflow even when K-D is zero. Decimal price
conversion can turn a tiny range into a flat window. These cases are pinned.

Twenty-seven focused/inventory checks passed, including independent lifecycle
and extended arithmetic references, startup and nullable smoothing, all native
tuple/generic/index/range routes, repeated reads, chaining, maximum periods,
finite cancellation, genuine overflow, and every field/presence corruption.
Full trajectory references checked 160,828 values; 28 timing cases passed.
All 1,104 archived cases across 276 pairs pass the performance evidence verifier.

Reflection verifies that the outer Trady Stochastics and StochasticsOscillator
classes only contain named nested types and expose no calculation methods.
They are classified as utilities; their six callable nested families are
individually paired. This leaves 126 pending families in the 421-family inventory.
The prior fractal equality warning is documented as intentional: exact ties
invalidate strict extrema, while adjacent floating-point prices remain distinct.

## Window stochastic momentum and double-smoothed index

`WindowStochasticMomentum` covers Trady midpoint displacement with full-window
startup. `DoubleSmoothedStochasticMomentum` covers Trady StochasticsMomentumIndex
and Skender SMI, including Skender's signal. Both EMA stages seed at the first
complete price window. Each displacement, range and convex update rounds once;
unpublished stages retain an extended upper exponent using existing arithmetic.
The final index evaluates 200*smoothedDisplacement/smoothedRange without
prematurely overflowing a midpoint, displacement or range. Histories grow lazily.

A zero smoothed range leaves index and signal absent; the next defined index
seeds a new signal. Native Skender instead publishes present NaN on a flat range,
and its signal remains NaN even after the index recovers. Direct regressions keep
these nonfinite native values visible and require the comparison verifier to
reject them. Trady decimal half-range rounding can erase tiny outputs, while
midpoint/range/product arithmetic can overflow before a finite index is computed.
Native zero/negative smoothing behavior and Skender period-addition overflow
are separately pinned; the owned API requires positive periods.

Eighteen focused checks passed, covering independent lifecycle and rational
references, quote/tuple/generic/index/range routes, sorting, seed/signal presence,
source chaining, tiny/wide values, oversized unpublished displacement, long decay,
flat recovery, genuine output overflow and every field/presence corruption.
Complete integer-grid and native decimal/binary64-stage references checked
47,964 values. Twelve timing cases passed. All 1,116 archived cases across
279 pairs pass the performance evidence verifier.

## Nullable and smoothed stochastic RSI

`NullableStochasticRsi` covers Trady's unit-scale nullable RSI range. After a
complete calendar window, flat or entirely missing RSI windows yield 0.5;
otherwise a missing current RSI remains absent. `SmoothedStochasticRsi` covers
Skender's 0�100 range, full-window smoothing and signal, with a zero flat range.
Both compose the verified RSI conventions, use exact range ratios and means,
and grow histories lazily without overflowing combined startup periods.

Seventeen focused checks passed, covering independent references, lifecycle,
nullable gaps, native routes and sorting, repeated enumeration, chaining,
maximum periods, tiny/wide prices and every value/presence corruption.
Full trajectories checked 36,126 values. Eight timing cases passed; the complete
archive verifies 1,124 cases across 281 pairs. Native decimal rounding, invalid
period behavior and checked period-sum overflow remain explicitly tested.

## Rolling and decaying close extrema

`DecayingWindowExtreme` covers Quan minimum and maximum with optional decay.
New or equal extremes reset elapsed age. Each step moves toward the available
window mean by 1-exp(-0.1*decay*age/period), then caps the result at the actual
window extreme. Zero decay uses a monotonic deque directly. Positive decay uses
an exact window sum and once-rounded convex update, preserving finite results
when native intermediate sums or differences overflow. Storage grows lazily.

Fifteen focused tests passed: independent rational lifecycle contracts, integer-grid
and native SIMD-stage references, ties, eviction, source chaining/subscription,
revision, lazy maximum periods, tiny/wide prices and corrupted values/startup.
Native reset retains old window prices and native nonfinite decay is accepted;
these differences and present nonfinite outputs are explicitly pinned.
Full trajectories checked 42,760 values across decay and period configurations.
Eight timing cases passed; the archive verifies 1,132 cases across 283 pairs.

## Compensated double and triple exponential averages

`CompensatedExponentialAverage` covers Quan DEMA/TEMA with zero seeds and
shared residual-mass compensation. Alpha, residual and reciprocal are binary64
stages; each convex recurrence, compensated stage and final weighted combination
rounds once. Unpublished stages retain an extended upper exponent; subnormal
stages retain ordinary binary64 rounding. State uses constant storage.
The compensation cutoff checks the updated residual against 1e-10. This differs
from single EMA's extra compensated update and remains a separate convention.

Thirteen focused checks passed, covering independent rational lifecycle/grid
references, native staged references, startup/cutoff transitions, source
subscription, revision/reset, chaining, maximum periods, tiny/wide inputs,
finite cancellation, genuine final overflow and output/alignment corruption.
Native period+1 integer overflow and nonfinite staged products are exposed.
Full trajectories checked 64,504 values. Eight timing cases passed; the complete
archive verifies 1,140 cases across 285 pairs. Explicit-alpha/compensated single
EMA and QEMA use the separate convention described below.

## Mass-normalized single and quadruple EMA

`MassNormalizedEma` covers Quan's compensated integer-period and explicit-alpha
constructors; `MassNormalizedQuadrupleEma` covers four independently configured
stages combined as 4*E1-6*E2+4*E3-E4. Each stage retains a normalized mean and
rounded mass. Exact weights alpha and (1-alpha)*previousMass form a once-rounded
weighted ratio, followed by rounded mass. The separate binary64 residual preserves
single EMA's extra compensated update at the 1e-10 cutoff. When compensation ends,
the unnormalized sum is published and mass resets to one. QEMA's final combination
rounds once, rejecting genuine overflow. State is constant-sized for all periods.

This equivalent mass representation has an explicit rounding convention distinct
from native unnormalized stages. It avoids subtraction cancellation for tiny alpha:
native 1-residual can become zero even for positive alpha. Owned alpha is finite
and in (0,1]; native parameter holes and nonfinite outputs remain directly tested.
Native QEMA reset retains child EMA state; owned reset clears every stage.

Nineteen focused and existing EMA tests passed, including independent rational
lifecycle and grid references, native staged references, both EMA constructors,
distinct QEMA weights, cutoff transitions, source subscription, revision/reset,
chaining, maximum periods, tiny alpha/subnormal/extreme prices, final overflow,
and output/startup/stage corruption. Complete trajectories checked 122,384 values.
The outstanding EMA configuration list is now empty. Its unchanged SMA-seeded
benchmark retains valid archived timings; four new QEMA timing cases passed.
The complete archive verifies 1,144 cases across 286 pairs.

## Adjusted skewness and excess kurtosis

`WindowSampleShape` covers Quan's adjusted Fisher-Pearson sample skewness and
Sheskin excess kurtosis. Available-history startup returns defined zero until
three/four observations respectively. Flat skewness returns zero; mature flat
kurtosis is absent with an explicit presence output and recovers as soon as its
window has nonzero variance. Rolling exact moments and once-rounded rational
or square-root ratios avoid overflowing powers and preserve tiny differences.
History grows lazily for maximum periods.

Eleven focused checks passed, including independent centered rational lifecycle
and integer-grid references, staged native references, known coefficients,
startup, flat recovery, subnormal and adjacent extreme prices, maximum periods,
source subscription/revision/reset, chaining and value/presence corruption.
Native flat kurtosis publishes present NaN; a direct test requires the verifier
to reject it. Native extreme powers and tiny underflow failures remain visible.
Paired kurtosis trajectories/timings use nonflat windows; flat behavior has
separate independent owned and native defect checks, not a masked native value.

Full trajectories checked 25,730 values. Eight timing cases passed. The complete
archive verifies 1,152 cases across 288 pairs.

## Correlation and paired population statistics

`WindowCorrelation` covers TA-Lib Pearson correlation with explicit zero-flat
behavior. `WindowPairStatistics` covers all five Skender outputs: correlation,
R squared, covariance and both population variances, with individual presence
outputs. The two series use independently selected candle fields. Full-window
startup is absent; Skender-style zero variance leaves correlation/R squared
absent while covariance and variances stay defined. Each result rounds once
from exact rolling moments; R squared rounds the exact squared correlation.
Scalar correlation remains finite when variances exceed binary64. Publishing
all statistics rejects genuine variance/covariance overflow. History grows lazily.

Thirteen focused checks passed: independent centered rational lifecycle/grid
references, native recomputed/rolling moments, known coefficients, all output
presence, tiny/adjacent/wide prices, maximum periods, field selection/chaining,
Skender quote/tuple/reusable/sorted routes and date/length validation, TA-Lib
ranges/in-place output, native cancellation/overflow and every field corruption.
TA-Lib rejects one-element ranges, including period one; direct native tests
and independent owned one-bar checks cover that boundary separately. Concrete
TA-Lib input range ends are inclusive, while output range ends are exclusive.
Native Skender converts NaN to absent but retains infinity; these behaviors
remain visible in defect tests.

Full trajectories checked 74,348 values. Eight timing cases passed; the complete
archive verifies 1,160 cases across 290 pairs.

## Return beta and directional beta statistics

`WindowReturnBeta` covers TA-Lib's first-input market denominator and zero-flat
slope. `WindowBetaStatistics` covers Skender's evaluation/market orientation,
all four subset modes, and all seven outputs: standard/up/down beta, ratio,
convexity and both return series, with individual presence flags. Startup needs
period returns, or period+1 prices. A zero previous price gives zero return;
zero market returns enter only standard beta. Empty/flat Skender subsets are
absent, and zero down beta makes the ratio absent.

Each owned return rounds once with an extended upper exponent. Rolling exact
moments produce once-rounded beta; derived ratios/convexity also round once.
This preserves finite scalar beta across oversized unpublished returns. Full
statistics reject genuinely overflowing published returns or final outputs.
Histories grow lazily. Native Skender divides prices then subtracts one;
TA-Lib subtracts prices then divides. Their separate staged references retain
these numerical differences.

Thirteen focused tests passed, including independent centered rational lifecycle
and integer-grid references, known slopes/directional coefficients, all modes,
zero prices, flat subsets, tiny/wide values, maximum periods, true overflow,
field selection/chaining, Skender quote/tuple/reusable/sorted routes and date
validation, TA-Lib ranges/aliasing/one-element failure, native parameter holes,
and every output/presence corruption. Full trajectories checked 116,112 values.
Eight timing cases passed; the complete archive verifies 1,168 cases across
292 pairs.

Three annotation-only fixes move existing exact-flat RSI justifications onto
the comparisons flagged by Sonar. The formulas and their verified behavior
are unchanged; the current-head quality gate still requires confirmation.


## Commodity channel index conventions

Skender, TA-Lib, and Trady CCI now have complete comparisons through
`WindowCommodityChannelIndex`. Standard CCI uses the current window mean;
Trady averages distances from each historical price's own trailing mean and
starts after 2*period-1 bars. Skender flat windows are absent; TA-Lib and Trady
return zero. Trady's conditional default literal resolves to decimal zero.

Typical prices and trailing means round once. Absolute deviations remain exact,
and the final ratio uses the exact constant 0.015 before rounding once. Lazy
histories avoid eager maximum-period allocation and summed-period overflow.
Independent rational/grid references cover the owned formula; staged binary64
and decimal references retain each native convention, including TA-Lib's
physical ring order. Tests retain native tiny-value collapse, nonfinite overflow,
Trady decimal underflow/division failure, invalid periods, and one-element
TA-Lib range rejection. Sorting, generic/tuple/index/range routes, close chaining,
reset, full presence masks, and value/presence/formula mutations are covered.

Verification: 16 focused tests and 37,302 trajectory values passed. Twelve timing
arms passed; the full archive verifies 1,180 arms across 295 pairs.


## Window-relative Ulcer Index

Skender Ulcer Index is paired with `WindowUlcerIndex`. Each complete window
restarts its running maximum at zero; any prefix lacking a positive peak makes
the result absent. This differs from averaging previously calculated trailing
window drawdowns. Percentage drawdowns round once with an extended upper
exponent, exact squared sums avoid intermediate overflow, and the final root
rounds once. Genuinely overflowing outputs are rejected. Histories grow lazily.

Independent rational/grid and staged native references cover complete values
and presence. Regression tests cover window restarts, missing-prefix recovery,
flat/tiny/adjacent/maximum inputs, lazy maximum periods, genuine output overflow,
reset and close chaining. All quote, tuple and reusable-result routes, sorting,
default parameters and startup alignment are checked. Native squared-percentage
overflow can produce infinity despite a finite owned result; decimal quote
conversion can erase tiny positive peaks. Dedicated tests preserve those limits.

Verification: 9 focused tests, 12,201 trajectory values and 4 timing arms passed.
The archive now verifies 1,184 arms across 296 pairs, with no unmeasured pair.


## True-interval Choppiness Index

Skender Choppiness Index is paired with `WindowChoppinessIndex`. Each interval
includes the preceding close, and period intervals require period+1 input bars.
A zero full-window span is absent. Exact range differences and rolling sums
avoid intermediate overflow. Stable logarithms preserve ratios near one;
subsequent division by log(period) and multiplication by 100 use binary64 stages.
Histories grow lazily. Inconsistent candles that produce zero summed ranges but
a nonzero span imply a nonfinite output, which normal output validation rejects.

Independent rational log1p/fixed-point references cover near-unit ratios and
large/tiny prices, while a staged native oracle retains its summation order.
Tests cover known 0/100 values, gaps, startup, flat-window recovery, default and
maximum periods, sorting, close chaining, reset, and output/presence mutations.
Native decimal quote conversion can erase tiny ranges and excludes binary64
extremes. Native division can round a non-unit ratio to one, losing a small
positive Choppiness value; this limitation remains a dedicated regression.

Verification: 9 focused tests, 12,475 trajectory values and 4 timing arms passed.
The archive verifies 1,188 arms across 297 pairs, with no unmeasured pair.


## Directional Money Flow Index conventions

Skender and TA-Lib MFI are paired with `WindowMoneyFlowIndex`. Typical HLC/3
rounds once; exact comparisons assign flow direction and equal prices contribute
neither flow. Exact volume products and rolling sums preserve the final ratio
through underflow, overflow and cancellation. The final percentage rounds once.
Skender's zero-negative-flow case returns 100; TA-Lib's total-below-one case
returns zero. Startup requires period changes after the first typical price.
Optional additional suppression reproduces TA-Lib unstable-period alignment.
Histories grow lazily and startup arithmetic does not overflow at maximum periods.

Independent rational/grid references check owned values and presence. Separate
native references retain Skender's chronological sums and subtractive formula,
and TA-Lib's rolling sums and total-flow threshold. Tests cover known coefficients,
exact threshold boundaries, tiny/adjacent/maximum inputs, flat ties, lifecycle,
chaining with unchanged high/low/volume, sorting, defaults, ranges, aliasing,
float inputs and restored global unstable settings. Native decimal collapse,
underflowed/overflowed products, cancellation to zero, rejected one-element TA
ranges and overflowing TA lookbacks remain visible. Undefined or overflowing
owned final outputs are rejected.

Verification: 12 focused tests, 24,950 trajectory values and 8 timing arms passed.
The archive verifies 1,196 arms across 299 pairs, with no unmeasured pair.


## Chandelier Exit selections and conventions

Skender and Trady Chandelier Exit are paired with `WindowChandelierExit`, reusing
the verified mean-seeded Wilder ATR engine. Skender long floors the rolling high
at zero; short uses the actual low. Trady emits both actual-extreme exits and
permits zero and negative multipliers. Output begins at index period. Selected
levels evaluate independently, so an overflowing unselected exit cannot reject
a finite selected exit. Extended ATR stages and exact complete offset expressions
preserve finite cancellation; histories grow lazily.

Independent rational/grid and native binary64/decimal references cover values,
presence, selections and multipliers. Trady's 1.0m smoothing constant is retained:
decimal scale can affect conversion to double even for numerically equal decimals.
Tests cover both conversions, generic/tuple/index/range/repeated routes, defaults,
sorting, chaining, reset, tiny/maximum inputs and selected-output overflow.
Skender period one is rejected through ATR; unknown selections are only rejected
when mature, while NaN/infinite multipliers pass its validation. Owned constructors
reject invalid selections and nonfinite multipliers. Native decimal overflow and
quote conversion that erases tiny ranges remain explicit regressions.

Verification: 12 focused tests, 47,644 trajectory values and 8 timing arms passed.
The archive verifies 1,204 arms across 301 pairs, with no unmeasured pair.


## Keltner and STARC envelope conventions

`WindowAtrBands` now pairs Skender Keltner, Skender STARC and Trady Keltner.
Skender Keltner uses an SMA-seeded EMA; Trady uses a first-close EMA; STARC uses
a rolling SMA. Keltner centers start at max(centerPeriod,atrPeriod)-1, while STARC
centers publish independently at centerPeriod-1. Bands require both a center and
mature mean-seeded Wilder ATR. Every center, band and presence flag is checked;
Skender Keltner also includes normalized width, absent for an exactly zero center.

Center recurrences and complete band expressions round once. Extended ATR stages
preserve finite offsets across oversized ranges. Width uses the exact difference
of the published bands, avoiding intermediate subtraction overflow. Width is
optional, so its genuine overflow cannot reject Trady/STARC's finite bands.
Histories grow lazily and periods do not overflow during startup arithmetic.

Independent rational/grid, native binary64 and scale-preserving decimal references
cover unequal periods, early centers, multipliers, all outputs and masks. Tests
include lifecycle, generic/tuple/index/range routes, decimal-to-double conversion,
defaults, sorting, chaining, and value/presence/seed mutations. Native nonfinite
multiplier holes, decimal overflow and loss of tiny quote ranges remain explicit.

Verification: 14 focused tests, 160,232 trajectory values and 12 timing arms passed.
The archive verifies 1,216 arms across 304 pairs, with no unmeasured pair.


## Vortex and Ultimate true-range ratios

`WindowVortex` pairs both Skender positive/negative movement ratios and their
presence flags. Output begins after period movements; a zero total true range
is absent. `WindowUltimateOscillator` pairs Skender and TA-Lib's 4:2:1 pressure
ratios. Skender requires strictly increasing periods and makes any zero-range
contribution absent. TA-Lib sorts positive periods, permits duplicates and uses
zero for zero-range contributions. All differences, sums and weighted ratios
remain exact until final rounding. Histories grow lazily.

Independent rational/grid references and staged chronological/rolling native
oracles cover all outputs and period conventions. Tests cover known ratios,
startup, flat recovery, tiny/maximum prices, lazy maximum periods, lifecycle,
sorting/defaults, chaining, value/presence/policy mutations, and TA ranges,
aliasing and float inputs. Native decimal quotes can erase tiny ranges; TA
intermediate overflow remains visible. TA all-one-period lookback returns zero
and its full-range call attempts an invalid previous-close access. That native
failure and one-element range rejection are directly tested; the owned all-one
configuration has independently verified values from index one.

Verification: 15 focused tests, 50,020 trajectory values and 12 timing arms passed.
The archive verifies 1,228 arms across 307 pairs, with no unmeasured pair.


## Trady directional movement and early ADX

`WindowDirectionalMeasure` supplies seven selected counterparts: signed raw
positive/negative movement, dominant positive/negative Wilder-mean DI, DX,
early-seeded ADX, and delayed ADXR. Differences are compared exactly before
rounding selected movement; smoothed stages retain extended upper exponents.
Published raw overflow is rejected without rejecting finite normalized results.

Trady's DX returns zero before DI is available, but null when mature positive
and negative DI are both zero. Early ADX averages DX at indices 1 through period,
including startup zeroes. Later missing DX permanently propagates absence.
ADXR requires both aligned ADX endpoints; zero lag is valid and maximum lag uses
lazy storage. These conventions differ from Skender and TA-Lib.

Independent rational validation, integer-grid comparison, and staged decimal
native references verify every output and presence flag. Native smoothing keeps
its decimal scale (`1.0m / period`). Decimal overflow and collapse of tiny prices
remain explicit native limitations. Generic, candle, tuple, index, range,
repeated, chained and lifecycle routes have regression coverage, as do value,
presence and formula mutations.

Verification: 21 focused tests, 91,355 full-output trajectory comparisons and
28 timing arms passed. The complete timing archive now verifies 1,256 arms
across 314 pairs, with no unmeasured pair. Final complete correctness union and
current-head gates remain required before readiness.


## Skender sum-seeded directional index

`SumSeededDirectionalIndex` covers Skender ADX's five outputs: Pdi, Mdi, Dx,
Adx and Adxr, each with an explicit presence flag. It selects dominant movement
using exact differences, rounds selected movement/range and each sum stage once,
and retains extended upper exponents for finite normalized results. DI/DX start
at index period; ADX starts at 2*period-1 and ADXR lags by a full period.

Zero smoothed range skips the ADX update and publishes no outputs. A skipped
seed never recovers; a later range gap retains the prior ADX state, while ADXR
requires both published endpoints. Lazy queues and 64-bit derived lookbacks
support maximum periods. Independent rational/grid references cover these gaps,
tiny distinct movements, exact ties and oversized ranges. The native reference
retains Skender's chronological binary64 arithmetic; decimal input conversion
can erase tiny movements. Sorting, defaults, chaining, lifecycle, all values and
presence, and altered-period mutations have regression coverage.

Verification: 10 new focused tests plus all 21 Trady directional tests passed;
64,564 trajectory comparisons and four timing arms passed. The timing archive
now verifies 1,260 arms across 315 pairs, with no unmeasured pair. Final complete
correctness union and current-head gates remain required before readiness.


## TA directional measures and unstable startup

`PriorSeededDirectionalMeasure` covers PlusDM, MinusDM, PlusDI, MinusDI, DX,
ADX and ADXR. Exact differences choose strictly dominant movement; selected
movement/range, period-minus-one sum seeds and Wilder stages round once with
extended upper exponents. Period one returns raw dominant movement or DI as a
ratio without times100, and ignores unstable suppression. Longer-period DI uses
percentages. DX retains its previous published value at zero denominators;
ADX retains its previous average when direction is absent. ADXR uses a
period-minus-one lag and inherits ADX's unstable setting; this package has no
separate ADXR unstable setting. Wide lookbacks and lazy rating history support
maximum owned periods and suppression without eager allocation.

The installed native DX API reports packed offsets starting at zero, not input
bar indices. Its adapter validates that exact returned range before placing the
unchanged values on the bars implied by the lookback; full and subrange API tests
preserve direct evidence of the defect. Native DX also updates true range twice
after its first published output; the counterpart preserves that recurrence.
Native lookbacks can overflow and raw movement can become infinite; direct
boundary tests retain these limitations, while owned final overflow is rejected.

Independent rational/grid and chronological native references cover complete
values and presence. Regressions include settings, period one, subranges, aliasing,
float input, exact ties, subnormal retention, extended ranges, chaining, lifecycle,
and value/presence/seed/suppression mutations.

Verification: 16 focused tests, 97,939 trajectory comparisons and 28 timing arms
passed. The full timing archive verifies 1,288 arms across 322 pairs, with no
unmeasured pair. Final complete correctness union and current-head gates remain
required before readiness.


## Tillson T3 seed conventions

`TillsonAverage` covers Skender, TA-Lib and both QuanTAlib startup options.
Skender seeds all six stages from the first price. TA-Lib seeds six full means
in sequence and supports unstable suppression. QuanTAlib's default publishes
the first value, then builds prefix means excluding that first price; its
alternate option uses first-price EMA stages. Each owned stage uses exact
2/(period+1) weights and rounds once. The final cubic volume-factor polynomial
is evaluated exactly before publication, retaining finite cancellation even
when individual coefficient products exceed binary64. Period storage is constant
and derived lookbacks use 64 bits. Any finite volume factor is supported.

Independent rational/grid references cover owned outputs and staged native
references preserve the native coefficient arithmetic, EMA operation order and
Quan prefix SIMD lane sums. Quote/tuple/reusable/sorted/default/chained routes,
TA subranges/aliasing/float/settings, Quan subscription/revision/hot-state routes,
lifecycle and value/presence/seed/factor mutations have regression coverage.
Skender throws on maximum-period addition overflow; TA lookbacks can wrap.
Quan allocates six period-sized buffers and its Init retains the sample index,
so its reset output differs from a fresh instance. Native NaN factors and
nonfinite coefficient arithmetic remain visible. Owned nonfinite factors and
true final overflow are rejected; exact finite cancellations are retained.

Verification: 10 focused tests, 104,164 output comparisons and 12 timing arms
passed. The full timing archive verifies 1,300 arms across 325 pairs with no
unmeasured pair. Final complete correctness union and current-head gates remain
required before readiness.


## Triple EMA rate and optional signal

`TripleExponentialRate` covers Skender TRIX, EMA3 and optional SMA signal,
and TA-Lib TRIX under default and Metastock compatibility with per-stage EMA
unstable suppression. Skender starts all three stages from one price mean;
TA starts each stage separately. Owned EMA stages and percentage changes round
once using exact arithmetic. Signals require a complete window of present rates.
Shared zero/zero yields absence; unbounded change is rejected as final overflow.
Cascaded zero denominators return zero. Lazy signal queues and 64-bit derived
lookbacks support maximum owned periods without eager allocation.

Independent rational/grid and staged native references verify every value and
presence flag. Tests cover quote/tuple/reusable/sorted/chained routes, optional
signal lengths, TA subranges/aliasing/float/settings, lifecycle, oversized finite
price differences, subnormals, and output/presence/signal mutations. The native
TA subrange pipeline reseeds its later EMA stages from a truncated first-stage
buffer, so it is tested independently of a full-history slice. TA accepts period
one but gives a negative lookback and throws; the owned definition remains valid.
Skender accepts zero/negative signal lengths with NaN/zero results, while owned
signals require positive lengths. Native checked period/signal sums and unchecked
TA lookbacks have explicit overflow regressions.

Verification: 10 focused tests, 76,517 output comparisons and eight timing arms
passed. The full archive verifies 1,308 arms across 327 pairs with no unmeasured
pair. Final complete correctness union and current-head gates remain required
before readiness.


## Mean-seeded true strength and price momentum

`SeededTrueStrength` and `SeededPriceMomentum` cover Skender TSI and PMO,
including complete value and signal presence. Independent rational/grid and
staged native references cover mean seeds, all smoothing/signal combinations,
reset, chaining, route equivalence, missing inputs and extended intermediate
exponents. TSI preserves the late smoothing-one startup and shortened signal
seed; signal one remains absent. Native present-NaN startup is checked explicitly
and represented as absent by the finite counterpart. PMO retains 2/period
smoothing, separate times-ten scaling, smoothing-one overshoot and permanently
missing recurrences after zero prior prices. Published overflow is rejected.

Validation: 9 focused tests and 151,450 complete trajectory comparisons passed.
Eight timing arms passed; the cumulative archive contains 1,316 arms across
329 pairs, with 73 families remaining. Native checked parameter sums, quote
conversion, nonfinite startup and overflowing intermediate returns are explicit
regressions. Final complete correctness union and current-head gates remain
required before readiness.


## Price relative strength

`PriceRelativeStrength` covers Skender PRS, optional SMA of its ratios, and
optional difference of fractional returns. Evaluation/base fields provide
synchronized series. Complete ratio and return expressions round once; ratio
means accumulate published values exactly. Zero base prices produce absence,
missing ratios invalidate only affected mean windows, and histories grow lazily.
Native quote/tuple/reusable routes, date/length rejection, optional parameters,
sorting, chaining and every output/presence mutation have independent checks.
Oversized native return terms can become null after intermediate overflow while
the owned complete expression retains finite cancellation. Genuine published
overflow is rejected.

Validation: 9 focused tests, 85,652 trajectory comparisons and 4 timing arms
passed. The archive now contains 1,320 arms across 330 pairs, with 72 families
remaining. Final complete correctness union and current-head gates remain
required before readiness.


## McGinley dynamic conventions

`QuarticDynamicAverage` covers Skender Dynamic and QuanTAlib Mgdi. Its complete
quartic recurrence rounds once, retaining finite results through overflowing
differences, powers and scale products. Immediate startup publishes the first
close and substitutes ratio one at zero previous state. Reset-delay startup
omits the first result and restarts a period-long delay at zero previous state.
Singular zero-price transitions and overflowing recurrence states are rejected,
including during restarted delays. State storage is independent of period.

Independent rational/grid and staged native references cover complete values,
presence, factors, periods, lifecycle, chaining and mutations. Native checked
restart-index overflow, nonfinite factors/results, Quan subscriptions, revisions,
hot flags and reset behavior have explicit regressions. Rejection checks verify
the finite prefix and exact failure point; native nonfinite results stay visible.

Validation: 8 focused tests, 41,639 trajectory/boundary checks and 8 timing arms
passed. The cumulative archive contains 1,328 arms across 332 pairs, with 70
families remaining. Exact arithmetic is substantially slower than the native
binary64 recurrences in these measurements; the archive records this cost.
Final complete correctness union and current-head gates remain required.


## Historical and realized log-return volatility

`WindowLogVolatility` covers QuanTAlib Historical sample deviation and Realized
root mean square, with optional 252-day annualization. Directed fixed-point
log intervals certify each rounded return before exact moment accumulation;
the complete root including annualization rounds once. This preserves tiny
dispersion between nearly equal returns and finite logs of extreme price ratios.
Independent rational and fixed-point references cover the complete outputs.

Zero prior prices skip returns. Historical continues reading the existing
window; Realized emits zero on the skipped bar. Startup zero is present.
Nonpositive ratios consume an undefined return: Historical recovers when the
return expires, while Realized preserves its permanently poisoned sum as absent
output. Native nonfinite results are checked without masking them.

Native revised bars fail to restore prior state, and bar-only calls consume
stale value input. Tests cover both defects, subscription, paired-value input,
reset/hot flags, invalid input, period validation and the historical maximum-
period capacity overflow. Owned histories grow lazily.

Validation: 10 focused tests, 37,232 trajectory checks and 8 timing arms passed.
The archive contains 1,336 arms across 334 pairs; 68 families remain. The Sonar
exact-equality finding in the native infinity verifier has an explicit inline
justification; current-head gate confirmation remains required, as does the
final complete correctness union.


## EMA differences, MACD signals and volume percentages

`EmaDifferenceSignal` covers Skender MACD's oscillator, signal, histogram and
fast/slow averages; Skender PVO's volume-based percentage oscillator, signal and
histogram; and Trady MACD and histogram. Each EMA uses an explicit complete-mean
or first-price seed. The signal begins at the first available oscillator; a
missing percentage at zero slow EMA contributes zero to signal while leaving
the histogram absent. Period state uses constant storage.

Every stage rounds once with extended upper exponents. Output selection lets a
histogram retain finite cancellation when its hidden oscillator and signal are
unrepresentable; only selected published overflow is rejected. Independent
rational/grid and staged binary64/decimal references verify all public outputs,
presence, source choice, period configurations, chaining, lifecycle and mutations.
Quote/tuple/reusable/sorted Skender routes and Trady tuple/generic/index/range/
repeated routes are covered. Native zero-signal rejection, checked Skender
period overflow, Trady invalid smoothing periods and decimal overflow have
explicit regressions.

Validation: 10 focused tests, 182,998 trajectory comparisons and 16 timing arms
passed. The archive contains 1,352 arms across 338 pairs, with 64 families
remaining. Final complete correctness union and current-head gates are still
required before readiness.


## TA-Lib aligned and fixed-coefficient MACD

`AlignedEmaMacd` covers TA-Lib MACD and MACDFIX with all three outputs. Mean
seeds share the slow-period endpoint; first-price compatibility processes both
averages from bar zero. Startup suppression applies before oscillator startup
and again before signal publication. Fixed mode uses binary64 0.15/0.075
coefficients with periods 12/26. Periods are sorted and state size is constant.

Every complete stage rounds once with extended upper exponents; selected
histograms retain cancellation across unrepresentable hidden differences and
signals. Independent rational/grid and generic native float/double references
cover full trajectories, shifted subrange seeds, each input/output alias route,
compatibility, suppression, period configurations, chaining and mutations.
Native signal one passes the wrapper but its negative EMA lookback triggers a
slice exception; the counterpart provides its mathematical zero histogram.
One-element ranges and overflowing lookback/allocation arithmetic are explicit
regressions.

Validation: 10 focused tests, 117,696 trajectory comparisons and 8 timing arms
passed. Two Sonar findings about repeated startup assignments in the volatility
references were removed by initializing their arrays once; all 10 affected
volatility tests passed again. The cumulative archive contains 1,360 arms across
340 pairs, with 62 families remaining. Final correctness union and current-head
gates remain required before readiness.


## Smoothed accumulation oscillators

`SmoothedAccumulationOscillator` covers Skender Chaikin Oscillator (all four
outputs) and TA-Lib ADOSC. Skender independently mean-seeds its two EMAs;
TA seeds both from the first cumulative flow, preserves period order, ignores
compatibility, skips nonpositive candle ranges, and honors EMA unstable
suppression. TA subranges restart the cumulative line at start minus lookback.
The counterpart computes complete flow expressions before rounding, accumulates
rounded flows exactly, and rounds each EMA stage with extended upper exponents.
Hidden overflow can cancel into a finite oscillator; publishing an overflowing
selected detail is rejected. Period state is constant-space.

Independent rational and integer-grid references cover lifecycle, every output
and presence flag, startup, negative candle ranges, reversal, large periods,
overflow, chaining, and mutation detection. Native quote ordering, binary64
stages, generic floats, shifted ranges, and four input/output alias routes are
verified separately. Eight focused tests and 73,321 full-trajectory comparisons
passed. Eight timing arms bring the archive to 1,368 arms across 342 pairs;
60 families remain before final union verification and current-head gates.


## Range acceleration bands

`RangeAccelerationBands` covers all three TA-Lib Accbands outputs, averaging
transformed highs, closes, and transformed lows over a complete window. Exact
zero high+low leaves both candle extrema unchanged. Complete transformed inputs
round once with extended exponents, and each mean rounds once; hidden overflow
can cancel before publication. The public counterpart supports period one and
lazy histories for very large periods. TA requires periods of at least two.

Independent rational, integer-grid, and native generic references verify all
outputs, lifecycle, startup, zero denominators, extreme periods, negative values,
subnormal inputs, finite results after native overflow, chaining, mutations,
subranges, float arithmetic, and all nine input/output alias combinations.
Nine focused tests and 43,251 trajectory comparisons passed. Four archived timing
arms bring coverage to 1,372 arms across 343 paired families, with 59 pending.
Final union verification and current-head gates remain required.


## Ichimoku cloud snapshot

`IchimokuCloudSnapshot` covers Skender's three overloads and all five nullable
outputs with independently configurable forward/backward offsets. Leading A
retains the native extra startup gate. Lagging close uses future input; appending
bars can fill trailing missing rows. Input order is retained by the snapshot,
while native quote routes sort. Exact rounded midpoints preserve negative and
subnormal extrema and avoid overflow in finite averages. Native decimal highs
are incorrectly clamped to zero and decimal.MaxValue lows appear missing;
independent native references and explicit regressions preserve those findings.
Large native offsets throw OverflowException; counterpart indexing uses Int64.

Six focused tests and 105,783 trajectory comparisons passed, including all
nullable masks, native overloads/custom quote mapping, offsets, append behavior,
large periods and corruption detection. Four archived timing arms bring coverage
to 1,376 arms across 344 paired families, with 58 pending. Trady's extended
Ichimoku output range remains a separate pending family. Final union verification
and current-head gates remain required.


## Extended Ichimoku cloud

`IchimokuCloudSnapshot.Extended` covers Trady's full output range from index
1-basePeriod through count-1+basePeriod. All five nullable fields are verified,
including leading projections outside the input and the lagging shift of
basePeriod-1. Output rows are lazy, use Int64 coordinates, and capture their input
values before enumeration. Explicit output-coordinate ranges are supported.
Native Compute(start,end) expands both endpoints; explicit native index requests
and tuple/generic/candle mappings are separate regressions.

Eleven focused checks (including the Skender regressions), 99,197 trajectory
comparisons and four timing arms passed. Empty input, negative values, extended
startup, future projection, captured input isolation, subnormals, large shifts,
Int64 endpoint indexes and every output/mask mutation are covered. The archive
now contains 1,380 arms across 345 pairs; 57 families remain pending before final
union verification and current-head gates.


## Seeded adaptive averages

`SeededAdaptiveAverage` covers Skender and QuanTAlib KAMA. Both seed from the
close at period-1. Skender publishes KAMA and efficiency ratio and resets to the
current close on a flat window; Quan also publishes earlier closes, caps the
fast period at the efficiency period, and uses slow smoothing when efficiency
is zero. Exact differences and sums prevent overflow, and each specified logical
stage rounds once. Startup metadata matches actual average presence. History
grows lazily; maximum-period startup is checked without allocating that period.

Independent rational and grid/native-stage references cover lifecycle, seed and
flat transitions, coefficient configurations, all outputs and masks, extreme
prices, subnormals, chaining, quote/tuple/reusable/sorted routes, native event
updates, revisions, reset and mutation detection. Native slow-period addition
can overflow. Eight focused tests, 46,879 trajectory comparisons and eight timing
arms passed. The archive contains 1,388 arms across 347 paired families, leaving
55 pending; final union and current-head gates remain required.


## Trady adaptive average

Trady KaufmanAdaptiveMovingAverage is paired with `SeededAdaptiveAverage` using
last-price seeding, full-window startup and slow smoothing for flat windows.
The native implementation instead dereferences its missing flat efficiency and
throws InvalidOperationException. Dedicated checks require that exact native
failure while independently verifying the finite counterpart; successful native
trajectories retain decimal arithmetic and literal scaling. Missing nullable
inputs, tuple/generic/candle routes, index requests and expanded seed behavior
are verified separately.

Thirteen focused tests, including existing adaptive regressions, 15,285 trajectory
comparisons and four timing arms passed. Corrupt values/masks, wrong coefficients,
wrong flat continuation and suppressing the native exception are detected. The
archive contains 1,392 timing arms across 348 pairs; 54 families remain pending.
Final complete union and current-head gates remain required.


## TA-Lib adaptive average

TA KAMA is paired with `SeededAdaptiveAverage` using fixed fast2/slow30,
`resetFlat:false`, `fastFlat:true` and `outputDelay:1+suppression`. Average
publication starts at period plus the KAMA unstable setting; efficiency remains
available from its ordinary startup. Compatibility has no effect. Native
references preserve sliding volatility rounding and the signed-change saturation
condition, range-local restarts, generic float arithmetic and aliased input.
The counterpart uses exact differences, convex recurrence stages and overflow-safe
Int64 publication delays. Native overflowing lookbacks are explicit regressions.

All 18 adaptive-average tests passed. The exact-zero oracle spelling change
(`T.IsZero`, preserving the formula) also passed 14 affected checks including
Acceleration Bands. 18,646 trajectory comparisons and four new timing arms
passed. The archive has 1,396 arms across 349 paired families; 53 remain pending.
Final union verification and current-head gates remain required.


## Six-price-seeded MAMA and FAMA

`SeededPhaseAdaptiveAverage` covers Skender and QuanTAlib MAMA/FAMA with bounded
Hilbert history. Skender quotes use HL2; tuple/reusable routes use their supplied
series. Skender publishes a six-price mean seed, while Quan publishes expanding
means during startup. Quan's zero-input phase/period branches reuse values from
two bars earlier. Both output values and masks are compared.

Exact grid and rational references verify rounding of complete FIR sums,
correction products, phasor differences, homodyne products, smoothing and convex
output stages with extended upper exponents. Period/phase controls explicitly
use binary64 operations and Math.Atan of a rounded exact ratio. Native references
retain their staged binary64 operations and nonfinite results. Finite extreme
prices can overflow the native seed/filter arithmetic; direct regressions retain
those infinities/NaNs while the counterpart stays finite. Quan Init only resets
its exposed Fama value; a regression demonstrates retained averaging state.

Eight focused tests, 56,238 trajectory comparisons and eight timing arms passed,
covering lifecycle, coefficients, startup, HL2/tuple/reusable/sorted input routes,
events, revisions, reset, chaining, subnormals and output/mask mutations. The
archive contains 1,404 arms across 351 pairs, with 51 pending families. Final
complete union verification and current-head gates remain required.

## Delayed zero-seeded MAMA and FAMA

`DelayedPhaseAdaptiveAverage` covers TA-Lib MAMA with zero filter/average seeds,
Hilbert stages starting at index twelve, and publication at 32 + suppression.
Fast and slow limits independently span [0.01,0.99], including reversed limits.
Storage is bounded independently of suppression; metadata saturates safely.
Independent rational and grid references cover both outputs and presence flags.
The native reference preserves staged arithmetic, float, shifted subranges and
both alias routes. Compatibility and unrelated EMA suppression do not affect
MAMA. Native NaN limits and overflowing lookback behavior remain explicit tests.

Fifteen focused tests (including shared seeded-MAMA regressions), 34,492 output
comparisons and four timing arms passed. The archived union now has 1,408 arms
across 352 pairs, with no unmeasured pairs and 50 families pending. The final
complete correctness union and current-head gates remain required.

## Deviation-ratio adaptive average

`DeviationRatioAdaptiveAverage` covers QuanTAlib VIDYA: short-window means for
the first long-period observations, then gain = alpha times the population
deviation ratio. Independent positive periods and finite signed alpha are
supported; omitted long period is four times short period with checked range.
Exact rolling moments, lazily growing history and separately rounded ratio,
gain and complete recurrence stages with extended upper exponents preserve
finite results even when the intermediate ratio exceeds binary64 range.

A zero long variance leaves the recurrence undefined until reset. Our value
and presence outputs represent that explicitly. Native NaNs are retained in
direct regressions and represented as missing in the paired adapter; infinities
are never hidden. Native Init retains both price buffers. Rational lifecycle,
independent centered-grid and native staged references verify startup, window
order, signed/zero gain, source events, revisions, reset, chaining, extremes,
subnormals and output/presence/gain mutations. Eight focused tests and 19,175
output comparisons passed. Four updated timing arms passed; the archive union
contains 1,412 arms across 353 pairs. There are 49 pending families; final full
correctness union and current-head gates remain required.

## Complete-window deviation bands and width

`WindowDeviationBands` covers Skender Bollinger Bands, Trady Bollinger Bands
and Trady Bollinger Band Width. The public counterpart has six values and
matching presence flags: center, upper, lower, band position, exact-distribution
Z-score and relative width. Trady width is percentage-scaled; Skender width is
unscaled. Native object, generic, tuple, reusable-result and sorting routes
are verified. Trady supports period one and signed/zero factors; Skender
requires periods above one and positive factors, but admits NaN factors.

Mean and population deviation round once, followed by the scaled deviation
with extended upper exponents. Ratios use this center and offset before final
band rounding, preserving position when published bands coincide. Zero offset
makes position absent; zero center makes width absent; zero variance makes
Z-score absent. Independent rational, centered-grid and native binary64/decimal
references retain each contract. Native Skender overflow/nonfinite outputs and
Trady decimal variance overflow are explicit boundary regressions.

Ten focused tests plus one expanded overflow-boundary check, 194,368 output
comparisons across three shards and twelve timing arms passed. Production
binaries were reused for adapter/test corrections. All timings retain the
public counterpart's complete six-output calculation, including when a native
API returns fewer outputs. The archive union has 1,424 arms across 356 pairs,
with no unmeasured pairs and 43 pending families. Final complete correctness
union and current-head gates remain required.

## Generic classical moving average routing

`ClassicMovingAverage` covers all nine TA-Lib MA methods. Period one is identity
regardless of method or startup settings. EMA/DEMA/TEMA use full-mean or
first-price seeds and per-stage suppression. KAMA/MAMA/T3 retain their own
startup rules; MAMA ignores requested periods above one. Exact exponential
recurrences and final extrapolation use extended upper exponents. Lookbacks
use Int64, metadata saturates, and window storage grows with observed data.

Independent rational lifecycle and integer-grid references cover every method.
A separate native full-history model verifies float/double stages, shifted
ranges, both compatibility modes, suppression and aliased buffers. Native
period-one identity even accepts undefined MAType values; our enum validation
rejects these. Native WMA's Int32 divisor overflows: a 65,536-bar constant-one
window returns 65,537; the counterpart returns one. These discrepancies and
one-element rejection, extreme/subnormal data, chaining, reset and output,
mask, method and seed mutations have executable regressions.

Fifteen focused tests, 89,002 output comparisons and four timing arms passed.
The generic API's default SMA is timed here; constituent methods also retain
their separately archived direct-API timings. The archive union contains
1,428 arms across 357 pairs; 45 families remain pending. Final complete
correctness union and current-head gates remain required.

## Classical absolute and percentage price oscillators

`ClassicPriceOscillator` covers TA-Lib APO and PPO across all nine average
methods, both compatibility seeds, suppression, reversed/equal periods and
independent startup alignment. Periods are ordered before calculation. APO
rounds the complete difference; PPO rounds 100 times that difference divided
by the slow average, returning zero for an exactly zero denominator. Ooples
supports positive periods including one; native APIs require at least two.

DEMA/TEMA/T3 components retain their rounded values with extended upper
exponents until oscillator publication. Their standalone outputs are unchanged.
A lifecycle regression exposed that an unrepresentable component average can
still produce a finite PPO; retaining the wide value fixes this without
misclassifying intermediate overflow as final output overflow. Independent
rational and grid references verify these cases, ordinary/subnormal inputs,
source chaining, masks, formula/period mutations and lifecycle. Separate native
references retain staged subtraction/division/multiplication, float/double,
shifted ranges, aliases and nonfinite extreme arithmetic.

The grouped oscillator/generic-average/T3 suite passed 36 tests. APO/PPO passed
696,188 output comparisons; four directly affected averaging pairs passed
193,166 further comparisons. Eight timing arms passed. Sonar S1244 in the
Bollinger native reference was corrected with exact nullable equality plus
explicit NaN handling; its ten tests and 109,292 comparisons passed without
introducing a tolerance. Current-head Sonar confirmation remains required.
The archive union contains 1,436 arms across 359 pairs, with 43 families pending.
Final complete correctness union and current-head gates remain required.


## Classical deviation bands

`ClassicDeviationBands` supplies TA-Lib BBANDS across all nine classical averages,
independent nonnegative upper/lower factors, both exponential seeds and suppression.
It aligns the center and population deviation with the current bar and publishes
three values plus presence flags. Wide intermediate arithmetic preserves finite
results; true published overflow is rejected.

The native reference separately preserves TA-Lib's uncentered variance cancellation
and overflow, nonfinite factor behavior, and MAMA misalignment when the deviation
window starts after the average. The period-80 regression proves that native middle
values retain the older packed prefix. Float/double, shifted ranges, aliases,
all methods, reset/chaining, extreme values and mutations have executable checks.

Ten focused tests and 1,589,676 output comparisons passed. Four timing arms were
recorded and the archive verifier passed 1,440 arms across 360 pairs. There are
42 pending families. Final complete correctness union and current-head gates
remain required before readiness.


## Variable-period classical averages

`VariablePeriodClassicAverage` covers TA-Lib MAVP for all nine classical average
methods. Its pure per-bar selector is truncated and clamped to ordered positive
limits. The maximum-period lookback fixes the publication boundary. Each selected
average aligns its seed to that boundary; first-price EMA begins at input zero,
while later DEMA/TEMA stages consume only their aligned predecessors. The same
internal alignment supports the existing classical average without changing its
default startup. History and engines are allocated only as observations and
selected periods arrive; revisiting a period replays its missed observations.

The selector sees a chained close and is called once per eligible update. Nonfinite
selections are rejected before advancing state. The owner defines period-one
identity and accepts arbitrary finite selections without narrowing overflow.
TA-Lib requires both limits at least two, rejects a periods array shorter than the
maximum lookback even for an empty result, and converts nonfinite selections
through `Int32.CreateTruncating`. Native shifted ranges, float/double arithmetic,
price/period aliases and overflow are preserved in independent regressions.

The grouped variable-period/classical-average suite passed 31 tests. MAVP passed
81,564 output comparisons; generic MA, APO, PPO and BBANDS passed 2,374,866 affected
comparisons in parallel. Four new timing arms passed. The full archived timing
union is 1,444 arms across 361 pairs, with no unmeasured paired family. The local
MAVP measurements retain the competitor's speed/allocation advantage; they do not
claim a speedup. Forty-one families, final correctness union and current-head
gates remain before readiness.


## Classical stochastic RSI

`ClassicStochasticRsi` covers TA-Lib STOCHRSI with all nine signal average methods,
independent RSI and signal suppression, and explicit exponential signal seeding.
It uses the verified zero-flat Wilder RSI and a complete RSI range, maps that range
to [0,100] with exact differences, and withholds both K and D until signal startup.
RSI remains causal when first-price exponential signal seeding is requested.
All period arithmetic is wide and histories grow lazily, including huge requests.

Separate native references preserve float/double rounding, shifted ranges and the
Metastock RSI defect: later seed observations and the unfilled zero tail affect
stochastic outputs. Direct regressions prove the final zero-tail output, native
failures for single-element intermediate buffers, underflow/overflow behavior,
and input/output aliases. Aliasing K and D together overwrites D with K in the
native API. The owner defines positive period-one requests and rejects invalid
configuration. Chaining, reset/lifecycle and value/mask/parameter mutations are
covered independently.

Fifteen focused tests and 256,928 output comparisons passed. Four new timing arms
passed; the archived union now verifies 1,448 arms across 362 pairs. Forty families,
the final complete correctness union and current-head gates remain before readiness.


## Window stochastic K/D/J

`WindowStochasticKdj` covers Skender's standard and extended stochastic APIs.
SMA smoothing waits for complete windows, while SMMA uses the first available
value to seed each Wilder recurrence. K publishes independently; D/J wait for the
signal. Configurable finite positive J factors combine in one exact expression.
Unpublished raw ratios and averages retain binary64 precision with extended upper
exponents so cancellation can produce finite published values. Period arithmetic
uses wide sums and history grows lazily.

The raw-range helper now accepts an internal flat value and exposes readiness;
its existing Trady default remains fifty. Independent native regressions retain
Skender's short-history SMMA exception, checked period-sum overflow, decimal
conversion, nonfinite J factors and staged product overflow. Both API overloads,
result aliases and reusable K, chaining, startup flags, true published overflow,
wide unpublished cancellation and mutations are checked.

The grouped suite passed 33 tests initially; its remaining test used a constant
close fixture with a nonzero range. Correcting that fixture to equal high/low/close
made the zero-range test pass, with unchanged production binaries. The new family
passed 99,067 output comparisons, and seven affected Trady pairs passed 160,828
further comparisons in parallel. Four new timing arms passed; the archive union
verifies 1,452 arms across 363 pairs. Thirty-nine families, the final correctness
union and current-head gates remain before readiness.


## Classical MACD with independent averages

`ClassicMacd` covers TA-Lib MACDEXT with independent fast, slow and signal
methods, aligned starts, compatibility seeds and suppression. Reversing periods
also reverses their associated methods. All three outputs publish together.
Wide internal rounded values preserve finite published results after hidden
overflow, including all nine signal methods. True published overflow is rejected.
The shared classical window backend retains exact rolling integer moments and
lazy histories. Public period-one identities remain supported.

Independent rational, integer-grid and native references cover all 729 method
combinations, reversed/equal periods, subranges, float/double buffers, aliases,
extreme/subnormal inputs, lifecycle, chaining and mutations. Native one-element
intermediate failures and nonfinite arithmetic remain explicit regressions.

All 121 grouped regression tests passed in 4 minutes 39 seconds. Sixteen
trajectory checks passed 3,549,354 values, including 575,577 for MACDEXT.
Four new timing arms and 24 refreshed arms for MA/MAVP/APO/PPO/BBANDS/STOCHRSI
passed the permanent performance verifier. The archive union now verifies
1,456 arms across 364 pairs, with none unmeasured. These short measurements
show substantial MACDEXT disadvantages against TA-Lib; no universal speedup
is claimed. Thirty-eight families, the final correctness union and current-head
gates remain before readiness.


## Classical fast and slow stochastic

`ClassicStochastic` supplies TA-Lib STOCH/STOCHF counterparts with independent
K/D classical averages; K period one selects fast stochastic. Complete high/low
windows use exact differences and zero for a flat range. Both outputs wait for
D startup. Wide rounded intermediates preserve finite cancellation even when
raw ratios exceed binary64; genuinely unrepresentable published values fail.
Compatibility seeds, suppression, period-one identities, lazy huge periods,
chaining and independent output presence are explicit.

Independent rational/grid/native references cover every K/D method combination,
float/double subranges, input aliases, extreme/subnormal ranges and mutations.
TA-Lib divides the range by 100 first, so underflow and overflowing differences
can produce zero or NaN where the owned exact ratio remains finite. Native
single-element intermediate buffers fail. If D aliases an input, native D
overwrites the shared temporary buffer before K is copied: K then contains a
mixture of shifted D and surviving intermediate values. In-place DEMA also
leaves an EMA1 tail. Dedicated alias regressions preserve those native defects;
sharing the two output arrays leaves K in their final shared buffer.

The initial grouped run passed 30 tests and failed one alias fixture that used
an exclusive endpoint instead of TA-Lib's inclusive input endpoint. Correcting
it exposed the native D/input overwrite and DEMA tail; the final alias test
passed after both were independently modeled. The 15 affected stochastic-RSI
tests also passed. Production binaries were unchanged during these corrections.
New trajectories passed 542,840 values, with 256,928 affected stochastic-RSI
values. Eight timing arms passed; the complete archived performance union now
verifies 1,464 arms across 366 pairs, with none unmeasured. Thirty-six families,
the final correctness union and current-head gates remain before readiness.


## Hilbert cycle period and phasor

`HilbertCyclePeriod` and `HilbertPhasor` cover TA-Lib HT_DCPERIOD and HT_PHASOR.
Their zero-seeded filters start at index twelve and publish at 32+suppression.
They share the extracted, bounded Hilbert stages with the existing adaptive
averages, retaining their rounding and zero-input conventions. Exact rounded
wide filter and homodyne stages preserve finite period controls after hidden
overflow. Only the selected published readings are subject to overflow rejection.

Rational and full-history integer-grid references independently cover the owned
formula. The native array model retains IEEE operation order, subrange reseeding,
float/double behavior, aliases and NaNs. Native lookbacks wrap with oversized
suppression; owned lookbacks use Int64 and fixed storage. Reset/lifecycle,
startup, chaining and value/presence/suppression mutations are checked.

All 123 grouped regression tests passed, including affected adaptive and
classical-average callers. Fourteen trajectory checks passed 3,979,103 values,
including 56,598 for the new families. Eight new timing arms and twelve refreshed
MAMA arms passed; the archived union verifies 1,472 arms across 368 pairs with
none unmeasured. Thirty-four families, the final correctness union and
current-head gates remain before readiness.


## Delayed Hilbert trendline

`DelayedHilbertTrendline` covers TA-Lib HT_TRENDLINE. Its Hilbert filters start
at index 37 and outputs publish at 63+suppression. The rounded smoothed period
selects a mean of closes, followed by a 4/3/2/1 weighted mean. Each mean rounds
once; wide filter intermediates keep final values finite at extreme prices.
History is bounded to fifty prices and three previous means.

Independent rational, integer-grid and native references verify the complete
formula. Native overflowing WMA/filter arithmetic can produce a NaN period,
which converts to a zero cycle length and yields zero trend means. Dedicated
regressions preserve that native result while the owned trendline retains a
constant maximum price. Float/double subranges, aliases, startup, huge
suppression, lifecycle, chaining and mutations are covered.

All 32 grouped tests passed. Six trajectory checks passed 167,104 values,
including 19,776 for the new pair. Four timing arms passed; the archived union
verifies 1,476 arms across 369 pairs, with none unmeasured. Thirty-three families,
the final correctness union and current-head gates remain before readiness.

## Delayed Hilbert phase, sine, and trend mode

`DelayedHilbertPhase`, `DelayedHilbertSine`, and `DelayedHilbertTrendMode`
complete TA-Lib's phase/sine/trend-mode families. They share the existing
index-37 Hilbert engine and publish from index 63 plus optional suppression.
Phase projections use rounded wide intermediates; trend mode retains the
sine crossing, phase-change, and 1.5% deviation rules with exact comparisons.
A fixed-size mean history is shared with `DelayedHilbertTrendline`.

Independent rational, integer-grid, and native IEEE references cover full outputs,
float/double subranges, aliases, zero and extreme inputs, lifecycle, chaining,
and output/configuration mutations. Native overflow and nonfinite results remain
explicit limitations in the manifest.

Verification: 35 grouped contract cases and seven affected trajectory pairs
passed (189,970 output comparisons). The permanent contract runner also passed
a real compiled-test smoke check and 17 script tests. Twelve new timing arms
and four refreshed trendline arms passed after correctness finished. The archive
verifies 1,488 arms across all 372 paired families, with none unmeasured. Thirty
families and final union/current-head gates remain before readiness.

## Early-start Hilbert trendline

`SeededHilbertTrendline` supplies Skender's early-start Hilbert trendline, smooth
price, and nullable rounded cycle length. The default input is the high/low
midpoint; an explicit close mode supports tuple/chained comparisons. Raw input
publishes through index 10, smoothing starts at index 6, and the weighted cycle
mean starts at index 11. The implementation reuses the bounded Hilbert filter
and weighted-mean histories. Independent rational and integer-grid references
cover rounded wide stages; a separate native IEEE reference preserves overflow
and null behavior.

Verification: 24 grouped contract cases passed, including reset/lifecycle,
wide/subnormal inputs, tuple/reusable routes, chaining, and all output/presence
mutations. Six affected trajectory pairs passed 193,354 output comparisons.
Four timing arms passed. The archive verifies 1,492 arms across all 373 paired
families, with none unmeasured. Twenty-nine families and the final correctness
union/current-head gates remain before readiness.

## Range and filtered-deviation adaptive averages

`RangeFractalAverage` compares with QuanTAlib FRAMA using prefix means, an
oldest-first split at floor(period/2), and the epsilon-adjusted range ratio.
The exact ratio rounds before Math.Log/Exp; gain is clamped to [0.01,1].
`FilteredDeviationAverage` compares with DSMA: first-price seeding, two-pole
filtering of price minus previous average, fixed-period RMS during startup,
and gain clamped to [0.1,1]. Its normalized magnitude rounds the complete
square-root ratio, retaining meaningful controls at extreme and subnormal prices.
Both round wide stages and convex updates and grow history lazily.

Independent rational/grid/native models cover every output. Native SIMD sum
order, IEEE overflow, DSMA NaN scale acceptance, and DSMA retaining RMS samples
after Init are explicitly verified. Ten grouped tests cover lifecycle/reset,
huge periods, extreme values, revisions, chaining, and mutations. Two trajectory
pairs checked 66,532 values; eight timing arms passed. The archive now verifies
1,500 arms across all 375 pairs. Twenty-seven families and final gates remain.

## ATR trailing stop and SuperTrend

`SeededAtrTrailingStop` shares the mean-seeded ATR engine across close, high/low,
and midpoint candidate bands. It supplies Skender ATR Stop and SuperTrend,
including all active-band outputs and presence flags. Midpoint initializes
direction against midpoint; the other bases compare against previous close.
Exact close equality to the active band selects the upper/bearish result.
Wide rounded candidates preserve finite selected stops even when an unpublished
band exceeds binary64. Native binary64 stages and final decimal casts are
modeled separately, including decimal overflow and NaN multiplier acceptance.

Ten grouped tests passed, including rational lifecycle checks, all bases, ties,
wide cancellation, lazy periods, chaining, and every output/mask/configuration
mutation. Two full-trajectory pairs checked 136,504 values.
Eight timing arms passed; all 377 paired families now have archived timings
(1,508 arms). Twenty-five families and final gates remain.

## Retrospective volatility stop and window Fisher transform

`VolatilityStopSnapshot` preserves prior-bar ATR, favorable-close extremes,
strict reversal comparisons, and all four nullable outputs. Its first guessed
trend is erased through the first reversal, but is retained when no reversal
occurs. Appending data can erase earlier rows; this behavior is explicitly a
snapshot in supplied order. Wide internal stops are validated only after
retrospective removal, so discarded rows do not cause false overflow rejection.

`WindowFisherTransform` publishes Fisher and its one-bar trigger from available
windows, with initial Fisher zero and absent trigger. Flat windows reset the
position state; values beyond +/-0.99 clamp to +/-0.999. Midpoints and range
ratios are safe at extreme prices; binary64 smoothing and Math.Log define the
transform. Both midpoint and close routes have independent references.

Eight contract tests cover lifecycle, exact snapshot goldens and prefix erasure,
huge periods, extreme/subnormal values, native NaN/null propagation, chaining,
and every output/mask mutation. Two trajectory pairs checked 131,890 values.
Eight timing arms passed; the archive verifies all 379 paired families
(1,516 arms). Twenty-three families and final gates remain before readiness.

## Retrospective regression channels

`RegressionChannelSnapshot` reuses the verified exact regression engine to fit
nonoverlapping blocks backward from the final observation. A leading incomplete
block is absent, while BreakPoint is always defined. Null period fits the entire
input and requires at least two observations. Appending input realigns blocks.
Centers round once from exact fitted positions; rounded population deviation
and wide channel arithmetic determine both bands.

Independent integer-moment and native binary64 references cover all outputs.
Skender's unused decimal final-line conversion is retained as a native limit: it
can reject large finite channels. Quote, tuple and reusable-result routes are
checked against their actual inputs, preserving decimal conversion differences.
Four tests passed after correcting route inputs and adding invalid whole-input
cases to the harness. Full trajectories checked 205,055 values; four timing arms
passed. The archive verifies 1,520 arms across all 380 paired families, with
none unmeasured. Twenty-two families and final gates remain.

## Rolling and calendar pivot levels

`PivotLevelSnapshots` publishes nine nullable levels for Standard, Camarilla,
DeMark, Fibonacci and Woodie formulas. Rolling windows support offsets;
calendar snapshots support hour, day, invariant week and month windows.
Full calendar identities prevent sparse dates from merging distinct windows.
Woodie uses the current window open; rolling DeMark uses current bar open,
while calendar DeMark uses the previous window open.

Independent rational formulas verify once-rounded owned results; a separate
staged decimal reference retains native conversion, scale and overflow behavior.
Five contract tests cover formula goldens, sparse dates, huge lookbacks,
wide/subnormal prices, rejected inputs and every output/presence mutation.
Both trajectories passed 542,247 comparisons. Eight timing arms are archived,
bringing the verified archive to 1,528 arms across all 382 paired families.

## Single-stochastic Schaff trend cycle

`SchaffTrendCycleSnapshot` implements the Skender single-stochastic convention:
independently mean-seeded fast/slow EMAs, a complete stochastic window on their
difference, then a three-value simple average. Flat windows contribute zero.
Wide unpublished MACD values remain representable internally; histories grow
with observed input. Native zero-cycle requests fail in the nested stochastic.

The batch passed 15 contract/shared-EMA tests, 18,827 trajectory comparisons,
and all four timing arms. Independent rational and staged-native references,
startup goldens, quote/tuple routes, prefixes, maximum periods, wide/subnormal
prices and output/mask mutations are covered. Archive: 1,532 arms /383 pairs.

## Connors strength and return ranking

`ConnorsStrengthSnapshot` publishes RSI, delayed streak RSI, percent rank and
Connors RSI with independent masks. The inclusive rank window includes the
initial zero return, excludes returns with nonpositive previous prices and
retains integer-truncated percentages. Exact return ordering distinguishes
values that native overflowing divisions collapse into ties. Existing Wilder
strength arithmetic supplies both RSI legs; the final component mean rounds once.

Five contract tests and 83,634 trajectory comparisons passed. All four timing
arms are archived (1,536 total arms /384 pairs). Goldens, startup, quote/tuple
routes, huge periods, wide/tiny/negative prices, repeated calls, causal prefixes
and every output/presence mutation are verified.

## Oldest-first Hilbert trendline convention

`OldestFirstHilbertTrendline` retains QuanTAlib Htit's oldest-first indexing.
Its current phasor is multiplied against itself, making the finite imaginary
discriminator zero. A full native phasor reference independently verifies the
price-independent period reduction used by the owned implementation. The first
ten outputs are input prices; subsequent means and 4/3/2/1 oldest-first weighting
round once. Unused native phasor overflow cannot poison the owned output.

Five contract tests and 15,428 trajectory comparisons passed, including lifecycle,
source selection, extreme/subnormal arithmetic, native retained-history reset
behavior and corruption. Four timing arms are archived (1,540 arms /385 pairs).

## Sample-deviation relative volatility

`RelativeVolatilitySnapshot` starts the previous close at zero, computes sample
deviations of up/down changes, averages each deviation over available history,
and publishes their bounded percentage ratio. Exact changes and wide rounded
intermediates prevent unpublished overflow. A pairwise-distance/root oracle
independently verifies production rolling moments; a separate native oracle
models scalar deviations and SIMD summation. Native underflow-to-zero and
retained nested buffers after reset are explicit regression cases.

Five tests, 15,689 trajectory comparisons and all four timing arms passed.
The archive contains 1,544 arms across all 386 currently paired families.

## Median-adaptive signed-threshold convention

`MedianAdaptiveSnapshot` preserves three raw prices, 1/2/2/1 price smoothing,
upper medians over descending-by-two windows, the signed median error denominator
and the minimum final length of three. Complete updates round once and exact
ratio comparisons retain threshold distinctions without intermediate overflow.
Finite signed thresholds and positive periods are supported; history grows lazily.

Five tests, 29,009 trajectory comparisons and four timing arms passed. Independent
order-statistic/rational and staged-native references cover odd/even periods,
negative/zero medians, threshold neighbors, extreme prices, huge periods, fresh
calls, prefixes and output/threshold mutations. Archive: 1,548 arms /387 pairs.

## Exact Fibonacci and oldest-first sinc kernels

`FibonacciWeightedSnapshot` normalizes exact integer Fibonacci weights over the
available newest-first prefix, including period-one identity and kernels beyond
native floating overflow. `WindowedSincSnapshot` implements Afirma's published
response for all five windows, extending startup with the newest price. Its
unused cubic calculation is omitted. Undefined kernels are rejected explicitly;
complete weighted ratios round once. Kernel storage scales with period/taps.

Eight tests, 95,728 trajectory comparisons and eight timing arms passed.
The archive verifier passed 1,556 arms across all 389 paired families.

## Klinger volume and retrospective pivot trends

`KlingerVolumeSnapshot` preserves two-bar force initialization and independent
fast/slow/signal EMA startup. Exact direction/range calculations and wide rounded
force/EMA stages reject only published overflow. `PivotTrendSnapshot` reuses strict
fractals and connects unequal anchors within the maximum distance, preserving
future confirmation, all six masks, equal-anchor resets and retrospective trends.

Ten tests, 76,536 trajectory comparisons and eight timing arms passed, including
native staged references, extreme inputs, maximum periods/spans and mutations.
The archive verifier passed 1,564 arms across all 391 paired families.


## Parabolic stop conventions

Four public snapshot routes cover TA classic/extended, Skender confirmed and Trady five-bar initialization. Exact comparisons and wide once-rounded updates preserve finite stops through overflowing intermediate ranges. The confirmed route removes the guessed trend through its first reversal. Trady 3.2.8 replays and overwrites the preceding cached transition with already advanced state; its native reference models that defect, while our FourBar recurrence evaluates each transition once. Native decimal and double-stage values have separate references.

Verification: 13 contract/regression/mutation tests, 80710 full-output trajectory values and 16 timing arms passed. The archive verifier passed 1,580 arms across 395 pairs. Seven families remain pending.


## Completed competitor inventory

All 421 reflected API families now have an explicit disposition: 401 executable
pairs, 16 unavailable native implementations with independently verified owned
counterparts, and four non-indicator utilities. The contract suite rejects any
pending family. Trady ZigZag is an executable-probed throwing stub and maps to
our Skender-convention counterpart; it has no native performance result.

The final six pairs add nullable volatility-selected fixed-period RSI, both Hurst
exponents, Jurik adaptive smoothing, ZigZag with pivot types and both retracement
lines, and fixed/last-ATR Renko charts. Thirty-eight focused tests passed, along
with 403,589 full-output trajectory comparisons. References separately model
native decimal/binary64 stages and owned numerical conventions. Hurst uses an
explicit 1e-10 absolute/relative comparison budget for its independent gamma
and regression formulations; other final pairs require exact output agreement
with their own references.

Renko output is a lazy sequence of complete bricks, including exact timestamp
words, OHLCV and direction. Multiple bricks share a source candle and its evenly
allocated accumulated volume. Its binary64 brick size and baseline precision are
explicitly documented; native decimal brick-size conversion has a separate
reference. ATR-sized Renko and ZigZag repaint when future bars arrive.


Final correctness verification (2026-10-07): all 401 paired families passed the
20-shard complete-union check, covering 17,338,914 output values. All 3,004
contract cases across 1,042 discovered test methods passed. The Python evidence
validators passed 17 tests. Two inventory registration failures were corrected
and their affected contract shards rerun successfully. No pending family or
unresolved review thread remained in the current review snapshot.

The correctness job retains 20 parallel shards and allows 45 minutes per shard
because a shard can contain multiple quadratic native Trady implementations.
This changes the timeout ceiling, not the checks or measured workload.


Final performance verification (2026-10-07): the archive contains all 1,604
measurement arms across 401 pairs, with no unmeasured paired family. Dynamic
Momentum Index completed last. Builds and correctness workers were stopped
before the timing run. Results are machine-specific observations, with the
BenchmarkDotNet environment, sample errors and allocation measurements retained
in the archive.
