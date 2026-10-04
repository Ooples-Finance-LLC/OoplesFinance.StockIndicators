# V2 formula variants and derivation review

## Simple moving average: finite range and cancellation

For effective prices `x` and positive period `n`, SMA emits zero until `n` observations are available, then emits the arithmetic mean of the last `n` prices. This follows directly from equal weights `1/n`; no publication-specific variant is involved. Its output has price units. Signed finite prices and zero are admissible. Preview evaluates the candidate window without committing it; reset restores the empty window.

The mean of finite inputs is representable within their finite convex hull even when their sum overflows. A floating running sum can also lose a small surviving term when large opposite-signed prices cancel: `[double.MaxValue, 1, -double.MaxValue]` has mean `1/3`, not zero.

Core, legacy SMA, native SMA and its standard smoothing component retain the ordinary running-sum path, but use an exact accumulator after overflow or detected severe cancellation. Every finite binary64 input is an integer multiple of `2^-1074`; the fallback sums those integers with `BigInteger`, divides by the window count, and rounds the quotient once to binary64, nearest with ties to even. This derivation covers normal/subnormal boundaries and signed underflow. The original pairwise cancellation trigger remains as an early fallback, supplemented by outward running-sum error bounds. See the derivation below for the finite-input assumptions and comparison budget. Bounds and trigger state are rebuilt with the actual window.

SMA validation now automatically runs all eleven generated numerical classes, including overflow-adjacent inputs, cancelled spikes and subnormals. Its separate reference recomputes each window using exact rational arithmetic and then converts to double; that reference conversion uses double division/scaling and is not claimed to be correctly rounded for every rational. Its comparison budget is relative-only `1e-9`, with exact sign/zero agreement. Hand vectors separately verify the production fallback's midpoint ties across binary exponent boundaries, overflow, preview, reset and eviction recovery. Other indicators still require their own justified magnitude classes and numerical contracts; this SMA rollout does not complete that work.

This records the three interpretation questions raised in issue #244's initial review. These are explicit Ooples V2 definitions. Agreement with these definitions does not authenticate an original author's publication or establish a universal industry convention. Primary-publication attribution is still required before claiming that equivalence. The executable references are independently structured calculations, not independent evidence for the choice of variant.

## Guppy Count Back Line

Implementation: `CalculateGuppyCountBackLine`; reference: [BuiltInFormulaReferences.GuppyCountBack.cs](../src/Validation/BuiltInFormulaReferences.GuppyCountBack.cs).

For bar `i`, find the most recent highest high and most recent lowest low in the available trailing `length` bars. The later extreme determines direction. If both extremes occur on the same bar, select its high and the rising direction. Equal highs/lows select the most recent occurrence. These tie rules are library conventions.

Starting at that pivot, search backwards at most `length` earlier bars. In the rising direction, count strictly lower record lows; in the falling direction, count strictly higher record highs. Publish the second such record, equivalent to the third record including the pivot. Inside bars and equal levels do not count. If two earlier records are unavailable, publish the current effective close. The search can therefore reach earlier than the initial pivot-selection window.

The reference selects record extrema by ordering historical indices and comparing each candidate with all more-recent levels. Production walks backwards and updates one current level. This gives distinct implementations of the same definition. Every referenced index is at most `i`, so the definition is causal. With finite, ordered candles, an available result is an actually observed low/high; otherwise it is the explicitly chosen close fallback. It is not a forecast or a promise that a stop will execute at that price.

Regressions: `GuppyCountBackUsesActualHistoryAndSuccessiveExtrema` and `GuppyCountBackIgnoresInsideBarsAndMirrorsDirection`. They check history, inside bars, direction and batch/streaming preview/reset parity. Same-bar direction ties break mirror symmetry by convention; do not assert unconditional price-reflection symmetry.

## Mobility Oscillator

Implementation: `CalculateMobilityOscillator`; reference: [BuiltInFormulaReferences.Mobility.cs](../src/Validation/BuiltInFormulaReferences.Mobility.cs).

The legacy parameters select the bin count, candle window and smoothing length. The currently typed variant uses ten bins and two seven-period smoothing stages, with the supplied moving-average kind and candle-window length. Coverage of that typed variant does not establish coverage of every legacy bin/smoothing parameter.

For each full candle window, partition its overall low-to-high range into equal bins. Model each candle as a uniform probability distribution on its low/high interval; a zero-range candle is a point mass. Average those distributions over candles. Bins include their lower boundary and exclude their upper boundary, except the last bin, which includes the window high. Production integrates overlap lengths; the reference subtracts cumulative distribution functions.

For valid `low <= high` candles with a nonzero overall range, bin masses are nonnegative and sum to one in exact arithmetic: each candle's disjoint-bin probabilities telescope to one. A zero overall range produces raw zero. The modal bin is the first bin within `1e-12` probability mass of the global maximum. This is an explicit tie convention, not a proved rounding-error bound.

Compare the close immediately preceding the candle window with the modal bin's midpoint. Raw magnitude is `100 * max(0, 1 - comparisonBinMass / modalMass)`; prices outside the window have zero comparison-bin mass. The sign is positive below the midpoint and negative otherwise. Thus raw values lie in `[-100, 100]` under the distribution assumptions. Apply one moving average for `Mo` and a second for `Signal`. Convex, nonnegative-weight averages preserve this bound, including zero padding; extrapolating averages such as DEMA need not. The initial raw value is zero until the preceding close is available.

Regressions: `MobilityIntegratesACommonProbabilityDistribution` and `MobilityModeTracksTheDensestBinAndPointCandles`. Field finiteness alone does not establish valid candle ordering; complete enforcement of this formula-specific domain remains a rollout requirement.

## MESA Predict V1

Implementation: [MesaPredictionKernel.cs](../src/Streaming/MesaPredictionKernel.cs); reference: [BuiltInFormulaReferences.MesaPrediction.cs](../src/Validation/BuiltInFormulaReferences.MesaPrediction.cs).

`UpperLength` is the fitting/high-pass window (minimum two), `Length2` is Burg order (clamped to `[1, UpperLength - 1]`), `Length1` is forecast horizon (minimum one), and `LowerLength` is low-pass/coefficient smoothing length (minimum one). Missing autoregression samples and coefficient history are zero. High-pass output is zero for the first four bars.

After high-pass and Super Smoother filtering, fit Burg autoregression on the available zero-padded window. At each order, the reflection coefficient is `2 * sum(forward * backward) / sum(forward² + backward²)`, with zero for zero energy. The exact-arithmetic inequality `2 * abs(f*b) <= f² + b²` bounds the coefficient by one; the implementation clamps roundoff to that interval. Production updates prediction coefficients in arrays; the reference builds the prediction-error polynomial in decimal.

Smooth each fitted lag coefficient **through time in its own history**, using normalized Hann weights `1 - cos(2*pi*j/(LowerLength+1))`. Smoothing across different lag positions instead would change the polynomial. Iteratively forecast the requested number of steps with these smoothed coefficients. `PrePredict` is the final step; `Predict` averages the current and previous `PrePredict`; `Ssf` publishes the filtered input. These outputs have input-price units.

A fitted-window scale no larger than `64 * 2^-52 * max(abs(currentPrice), abs(previousPrice))` suppresses a fresh fit to zero coefficients. This conditioning convention is part of this variant, not an analytic bound on the entire forecast error. In particular, it prevents claiming unconditional translation invariance. Smoothing autoregression coefficients also does not prove stability of the resulting polynomial; neither reflection-coefficient bounds nor finite input alone prove that an arbitrary forecast horizon stays finite.

Regressions: `MesaPredictRetainsTheFirstAutoregressionCoefficient`, `MesaPredictorTreatsUnavailableAutoregressionHistoryAsZero`, `MesaPredictionKeepsSeparateLagHistories` and `MesaPredictionMatchesAllRoutesAndCompanionMatrix`. These cover initialization, retained lag coefficients, independent histories, matrix prediction and preview/reset parity. The reference still uses double transcendental/filter intermediates and decimal's limited range; it is not an arbitrary-precision oracle.


## Overshoot Reduction Moving Average (ROMA)

Primary source: alexgrover, *Least Squares Moving Average With Overshoot Reduction*, TradingView, April 26, 2019, version 1.0: https://www.tradingview.com/script/k5rO6xlR-Least-Squares-Moving-Average-With-Overshoot-Reduction/ . The publicly accessible original Pine source defines `b = sma(abs(nz(d[1],close[1])-close),length/2)` and `c = b/highest(b,length)`. Both numerator and denominator therefore use the **smoothed** error. For finite nonnegative errors and a positive maximum, `0 <= c <= 1`; the library defines zero gain when the maximum is zero.

The index z-score times index/price correlation times price population deviation simplifies to `regressionSlope * (index - meanIndex)`. This removes cancellation-prone variance/correlation products. The streaming, legacy and core component routes use this form. The independent reference recomputes centered regression windows and its own entire feedback trajectory; it never reads observed indicator outputs.

Library variant: error averaging uses `ceil(length/2)` and partial windows; simple price/index means are zero until a full window, regression slope is zero before a full window, the initial previous price is zero, and an exactly zero previous output falls back to the previous price. The selected moving-average component controls price/index means. These are explicit library conventions, not a claim of identical Pine NA/startup or odd-length division semantics. Finite arithmetic and representable intermediates are assumptions of the gain bound; extreme-range assurance is still a separate gate.

For period 4 and prices `[1,2,4,8,3,6]`, the library convention yields `[0,0,0,36/5,53/10,8757/1640]`. The final gain is `53/82`. This vector distinguishes the corrected numerator from raw absolute error and covers startup and feedback.


## Basic price means: finite-range arithmetic

Average Price `(O+C)/2`, Median Price `(H+L)/2`, Typical Price `(H+L+C)/3`, Full Typical Price `(O+H+L+C)/4`, and Weighted Close `(H+L+C+C)/4` are arithmetic means of the stated fields. Finite field values have a representable mean even when the intermediate sum overflows. Core, native streaming, cached input selection and legacy chained source routes now use a shared guarded mean. It retains ordinary addition outside detected overflow/severe cancellation and otherwise uses the SMA exact fixed-point accumulator with nearest-even rounding, including subnormals. The cancellation detection remains heuristic; this is not a universal error-bound proof.

Independent references sum binary64 inputs as rational numbers and divide by the number of fields, with a relative-only comparison budget and sign/zero agreement. All five indicator types automatically receive the eleven numerical fixture classes. Mixed-field tests additionally check `mean(MaxValue,1,-MaxValue)=1/3` and `mean(MaxValue,1,-MaxValue,2)=3/4`; flat candles alone cannot expose those residuals. Their definitions do not require an OHLC ordering constraint for derived/synthetic data.

ROMA provenance fingerprint: the public source response identifies `scriptName=ROMA`, `version=1.0`, `created=2019-04-26T21:08:58.602373Z`, and `scriptAccess=open_no_auth`. SHA-256 of its UTF-8 `source` field (original line endings retained): `93dfa99234c3cda5e919d80aeae98646233b3d60f6b816e9808eb25f15da2ca1`. Source endpoint: `https://pine-facade.tradingview.com/pine-facade/get/PUB%3BT7Uux2SrLd1rtpArx8qSVuXbJWeX8ltV/last`.


## Mean roundoff bounds and exact rational conversion

The earlier pairwise cancellation heuristic is insufficient on its own. For the four binary64 inputs `[1e100,1e84,-9.9989e99,-1.0998790000010039e96]`, no individual cancellation meets its `1e-4` trigger, but ordinary summation yields `3.0250000485705324e91` instead of the correctly rounded mean `3.02500002500381e91` (relative error about `7.79e-9`). This is now a regression and the eleventh generated numerical class, `cascaded-cancellation`; previous shape sequences are unchanged.

SMA now also tracks an outward error bound on its running sum. Under binary64 round-to-nearest arithmetic, with `u=2^-53`, the absolute rounding error of an addition is bounded by `u/(1-u)*abs(roundedSum)`. Addition/subtraction producing a subnormal result is exact on the inputs' binary64 grid. Each bound multiplication and accumulation is rounded upward using the next representable nonnegative value. The bound includes eviction operations and is recomputed on each actual window rebuild. Preview uses local bounds and never commits them.

A finite, normal mean uses ordinary arithmetic only while the accumulated bound is at most `1e-10*abs(sum)`. This leaves a factor-ten margin for the declared `1e-9` relative comparison budget, including final division and rounding of the exact reference. Uncertain zero, overflow, or subnormal means use exact fixed-point window accumulation and nearest-even division. The guarantee is conditional on finite binary64 observations, a valid positive period, the documented startup convention and ordinary IEEE rounding; it does not establish correctness of indicators that subsequently transform the mean. Raw `RollingWindowSum.Add/Preview` consumers do not inherit the mean bound.

For the five price means, at most four terms are added. The fast path requires `abs(sum)>1e-4*maxAbsInput` and `maxAbsInput>=1e-300`, excluding overflow. The standard `gamma_4=4u/(1-4u)` bound then gives relative error below `2e-11`, safely within the `1e-9` comparison budget; ill-conditioned/tiny inputs use the exact path. This total-magnitude condition catches cascaded cancellations.

The independent rational reference now converts to binary64 by exact quotient/remainder quantization on the normal/subnormal significand grid, followed by nearest-even rounding. It no longer approximates the rational by truncating numerator and denominator before division. Midpoint tests include a quarter-subnormal residual on either side of ties across exponent boundaries and at overflow. This supersedes the earlier limitation about approximate final reference conversion; it is not a claim that every reference formula has independently reviewed provenance.


## EMA arithmetic initialization

For positive period N, observations 0 through N-1 publish the arithmetic mean of the observations seen so far. The finite binary64 inputs are accumulated as exact integer multiples of 2^-1074, divided by the prefix count and rounded to nearest, ties to even. Thus a representable initialization mean cannot overflow merely because its numerator exceeds binary64's range. The independent regression oracle uses rational arithmetic, including overflowing sums, cancellation, subnormals and adjacent representable values.

From observation N onward the existing recurrence remains `alpha*x + (1-alpha)*previous`, alpha=2/(N+1). The seed correction does **not** establish a library-wide or all-finite-input error guarantee for that rounded recurrence. Startup values may differ from historical sequential summation in low bits; earlier nonfinite startup results for finite inputs become finite means. Preview evaluates a copy of the exact accumulator and does not change committed history.


## EMA correctly rounded recurrence

The initialization contract above is unchanged. After initialization, V2 now evaluates
`RN((2*x + (N-1)*previous)/(N+1))`, where N is the positive integer period and RN is binary64 nearest-even rounding of the complete rational expression. The previous value is the previously committed rounded output. Preview uses that same committed value and does not update it. Period one and equal operands are exact identity cases. The canonical reference uses rational correction form `previous + 2*(x-previous)/(N+1)`, rounding its own prediction at each step; it never consumes observed outputs.

For finite input and previous values, nonnegative weights sum to the denominator, so the exact combination lies between the operands. Rounding cannot leave their representable closed interval or overflow. Constant inputs are preserved, including subnormals; the previous separate-product formula could turn a period-three constant epsilon into zero. Integer weights and N+1 use widened arithmetic, including N=int.MaxValue.

This is a guarantee of local correct rounding, not zero error versus an infinite-precision trajectory. If q=(N-1)/(N+1), the signed trajectory error satisfies e_t=q*e_(t-1)+delta_t, where delta_t is the single final rounding error. Thus |e_t| <= q^k*|e_seed| + sum(q^(k-j)*|delta_j|). Each delta is bounded by half a local binary64 spacing (epsilon/2 in the subnormal interval). No uniform relative or sign guarantee against the real trajectory is claimed under arbitrary cancellation. Exact agreement with the documented rounded recurrence is checked separately from comparisons against the unrounded mathematical trajectory.

Compatibility: prior coefficient/product rounding may differ in low bits or lose small residues. This change uses the mathematical coefficient 2/(N+1) before the final rounding. Exact weighted arithmetic is intentionally retained until a faster implementation can certify the same rounding; competitor performance is not established by this correctness batch.


## Weighted moving average finite-window contract

WMA and LinearWeightedMovingAverage use weights N for the current observation down to 1 for the oldest retained observation, divided by D=N(N+1)/2. Missing startup observations are zero; the denominator is not shortened. The hand vector [3,6,9,12] at N=3 produces [1.5,4,7,10].

The implementation maintains exact unweighted and weighted integer sums in binary64 units. If S is the old window sum and W its weighted sum, the next weighted sum is W-S+N*x; the next ordinary sum adds x and removes the expired observation. Induction gives exactly the direct window formula on every bar, including startup and eviction. Only the final division by D is rounded. Preview copies W and never changes either committed sum or the ring buffer; reset clears all three. Widened N(N+1) fits signed 64-bit for every positive Int32 period (allocation limits still apply to native windows).

For finite inputs this is a convex combination of the retained observations and any startup zeros. The exact result and its correctly rounded binary64 value remain within that finite interval. Cancellation, overflow in the unnormalized sum and old-window arithmetic cannot leave a stale residue. Independent validation recomputes each weighted window with rational arithmetic and demands exact numerical agreement. The guarantee concerns the given input doubles, not pre-rounded vendor data.


### Exact accumulator representation and fast path

The exact accumulator stores an integer times 2^(scale-1074). It aligns operands to the smaller scale before addition/subtraction. Multiplication by an integer weight is exact. A signed 64-bit representation is used only after multiplication, shifting and addition have been checked for overflow; other cases use BigInteger. The wide path removes trailing zero bits while adding the same amount to scale, an identity transformation that lets evicted low-magnitude data stop forcing a wide representation.

Both division paths derive the binary64 output grid from the exact numerator/denominator exponent, then use quotient and remainder to round to nearest-even. The small path checks that grid shifts fit unsigned 64-bit before executing them; otherwise it delegates to arbitrary precision. The half comparison uses remainder versus divisor-minus-remainder to avoid doubling overflow. No floating approximation decides rounding. Mixed-exponent randomized rational comparisons, exponent-boundary/tie vectors, evictions and allocation checks cover both paths.

## Population deviation at extreme magnitudes

`StandardDevation.Std` remains the trailing population deviation, zero until the full window is available. `MaType` controls only `Signal`. The normal path retains translated two-pass arithmetic. If a nonzero squared difference underflows into the subnormal range, or variance becomes nonfinite/subnormal, an exact integer fallback computes `A=N*Q-S*S` in units of 2^-1074 and correctly rounds `sqrt(A)/N` once. This avoids overflowing squares of representable spreads or erasing tiny spreads. It does not make the ordinary fast path correctly rounded.

The independent validation reference uses centered rational differences and binary64 candidate bisection with exact squared-midpoint comparisons. Both published outputs require preserved sign and a relative-only comparison budget. Eleven adversarial numerical classes are now automatic for this indicator. Formula provenance, ordinary-path analytic bounds and downstream consumers remain separate evidence obligations.

## Order statistics and simplified WMA

MedianValue preserves the current close until its window is full, then uses the middle value or the correctly rounded mean of the two middle values. Trimean uses nearest-rank Q1/median/Q3 on the available trailing window, including startup; it averages Q1, median, median, Q3 with the guarded finite-mean primitive. The core registry now follows the same nearest-rank convention as the builder, legacy method and native state. This corrects its former interpolated-quartile discrepancy.

PercentRank excludes the current observation and ties, and publishes zero until N predecessors exist. `100*countBelow` is an exactly representable integer for every Int32 period; dividing that numerator once avoids the previous extra rounding from `countBelow/N*100`. Selection outputs, median and percent rank use exact oracle comparisons; trimean's composite mean retains a relative-only, sign-preserving budget.

For simplified WMA let C_t be the cumulative input sum, with C_t=0 before startup. Its numerator `N*C_t - sum(C_(t-1),...,C_(t-N))` expands to `sum((N-j)*x_(t-j), j=0..N-1)`. Thus it is the same zero-padded linearly weighted window as WMA. The implementation now keeps that exact bounded rolling numerator instead of subtracting unbounded cumulative totals. The core registry's unrelated prior recurrence is corrected to that same contract. The independent reference explicitly sums each rational weighted window.


### Volume-weighted averages: rounding and zero mass

`VolumeWeightedAveragePrice` / `Vwap` is cumulative, not a rolling or session-reset average; its length option does not truncate history. The default price is the correctly rounded binary64 mean of high, low and close. A chained input supplies the price directly. Products of that price and volume and the total volume are accumulated exactly, and the final ratio is rounded once to nearest-even binary64. This avoids premature overflow or underflow of intermediates. Zero total volume returns zero.

`WindowedVolumeWeightedMovingAverage` preserves the periodic Bartlett window: lag `j` has weight `min(j, length-j)` for `0 <= j < length`. Its common normalization cancels in the ratio. Consequently the current bar has zero weight for lengths above one; length one has unit weight. Startup uses available bars. Price-volume-weight products and total weights are exact until the final rounded ratio. Nonnegative volumes give a convex weighted mean whenever total weight is positive; signed weights do not carry that guarantee.

`VolumeWeightedMovingAverage` with simple-average smoothing returns zero until a full window exists, then the correctly rounded ratio of the exact price-volume sum to exact volume sum for that window. Removing an expired bar removes its exact product, so an earlier large product cannot erase later small observations. The other smoothing choices retain their separate numerator-average / smoothed-volume formulas; this exact-ratio guarantee does not apply to them.


### Rounded triangular stages and Donchian midpoint

Triangular moving average applies the selected average twice at the same period. With SMA, each stage returns zero until its own window fills; the second stage consumes those startup zeros. The SMA/WMA/EMA variants round each stage once under the corresponding base-average initialization contract. SMA stages use exact window sums so cancellation in the second stage cannot amplify an avoidable first-stage summation error. This is a staged binary64 contract, not a single unrounded convolution.

Donchian upper/lower channels are the maximum high and minimum low over the available trailing window, including startup. The middle is the correctly rounded mean of those two finite bounds. Their sum need not be representable for the midpoint to be representable.


Ichimoku uses the same rounded midrange contract for Tenkan, Kijun and Senkou B, over their separately configured windows. Senkou A rounds the mean of the already rounded Tenkan/Kijun values. Outputs are published at the calculating bar; chart displacement is external. Chikou publishes the current close.

Welles Wilder moving average retains zero initialization and applies `previous + (input-previous)/length` as an exact rational operation rounded once per bar. This is algebraically evaluated as `(input + (length-1)*previous)/length` with exact intermediate integers. It does not use an SMA seed. The same recurrence is used by the native smoother, component averages and RSI gain/loss core. The contract preserves tiny representable results instead of separately rounding coefficient products.


The finite-output argument for these bounded averages is conditional on finite inputs and positive periods: each exact normalized sum has nonnegative coefficients adding to one, so its result lies between the finite input extrema (including the declared zero seed/startup). Rounding to nearest binary64 cannot leave those representable endpoints. Donchian/Ichimoku midpoint stages have the same property. For volume means, this argument additionally requires nonnegative volume and positive total weight; zero total weight follows the explicit zero-output convention. Exact accumulators prevent intermediate representability limits from invalidating this argument. The rational-oracle and mutation evidence tests the implementation of those operations; this is not a machine-checked proof of the implementation.


SlowSmoothedMovingAverage applies three averages sequentially. The middle width is `ceil(length/3)`, capped to [1,530]; the first and last widths are respectively the ceiling and floor of `(length-middleWidth)/2`, each capped to [1,530]. These are the existing public-library caps. SMA/WMA/EMA/Wilder stages use their declared rounding and startup independently. The core/registry default is WMA, matching the public indicator; the old two-full-length-SMA core formula was inconsistent.

SymmetricallyWeightedMovingAverage and EhlersTriangleMovingAverage both use zero-padded weights `min(j+1, length-j)` at lag j. Their sum is `floor((length+1)^2/4)`, evaluated in Int64 so every positive Int32 period fits. Symmetric WMA's extra common length factor cancels algebraically; removing it avoids overflowing its old integer products. The numerator is accumulated exactly and its normalized result is rounded once. JsaMovingAverage is the rounded midpoint of the current close and the close exactly length bars earlier, with a missing earlier close treated as zero. Its core now follows that same public contract.

Midpoint takes the maximum and minimum selected price over the available trailing window. Midprice instead takes maximum high and minimum low. Both round their exact midpoint once, include startup bars, and use precisely the requested positive period, including one. They inherit the finite convex-mean argument above. Midpoint preserves selected/chained input; Midprice uses OHLC fields. These statements describe the library formula conventions, with rational-window references rather than an authenticated external publication.

Parabolic and cubed weighted averages use `(length-lag)^2` and `(length-lag)^3`, respectively. The full-window integer weight sum is the denominator even during startup; missing observations are zero. Products and normalization remain exact until the final binary64 rounding. The positive-weight finite-mean argument applies including zero startup. Ordinary windows use an explicit rational-weight reference; large-period tests exercise exact integer weights beyond binary64 integer precision. This is the existing library formula with corrected numerical evaluation.

QuickMovingAverage uses taps `j/peak` for `j<=peak` and `(length+1-j)/(length+1-peak)` thereafter, for j=1..length+1, with `peak=clamp(ceil(length/3),2,530)`. Startup is zero padded. Period one retains its historical taps 1/2 and 1, so the normalized current/previous weights are 1/3 and 2/3. For longer periods the trailing tap is zero. Exact common-denominator cancellation yields positive integer taps and a single rounded normalized sum. The old core exponential recurrence did not implement this public formula.

FibonacciWeightedMovingAverage uses exact Fibonacci numbers F(length-lag), with F(1)=F(2)=1, and the full total F(length+2)-1 even during zero-padded startup. Production obtains adjacent Fibonacci integers by doubling identities and steps backward by subtraction. Validation builds the sequence forward as rationals and sums it explicitly. The old Binet approximation introduced rounding differences and eventually infinite weights; exact integer normalization avoids those intermediate limits. Resource feasibility for enormous periods remains a separate contract limitation.

SquareRootWeightedMovingAverage uses nearest-even binary64 coefficients sqrt(length-lag), an exact sum of coefficient-price products and exact coefficient total, followed by one nearest-even ratio rounding. Startup is zero padded. Validation derives each rounded square root by integer square-root iteration and squared-midpoint comparison, independently of production Math.Sqrt. This coefficient-rounding convention is distinct from rounding the ideal irrational weighted mean only at the end. For unit roundoff u=2^-53 and exact ideal mean mu, coefficient rounding changes the unrounded mean by at most u/(1-u) times the input range (including zero startup), since all coefficients are positive; final binary64 rounding adds its own half-ULP error, including subnormal rounding. The bound assumes nearest-even sqrt coefficients; the integer oracle checks those coefficients on the exercised periods and integer grids. It is not a machine-checked proof of every runtime implementation.

KAMA preserves source prices for its initial Length bars and publishes Er=0 there. Subsequently Er is the once-rounded ratio of the exact absolute length-bar displacement to exact total absolute one-bar travel; zero travel means Er=0. Fast/slow constants are rounded 2/(period+1), their convex blend using rounded Er is rounded once, and that gain is squared and rounded. The updated average rounds the exact blend of prior rounded KAMA and current price once. The triangle inequality places Er in [0,1]; positive smoothing periods keep the squared gain in [0,1], proving the exact update lies between the finite prior average and price. Correct rounding preserves those representable endpoints. Exact rolling travel insertion/eviction and copied preview accumulators implement the native path.

QuadraticMovingAverage is the root mean square of the available trailing window, including partial startup. It now rounds sqrt(sum(exact binary64 input squares)/availableCount) once. Production uses exact rolling insertion/eviction, a scaled rational radicand, integer square root and exact squared-midpoint rounding. Reference computes each rational window independently and bisects binary64 encodings. RMS is between zero and the maximum absolute input, so its correctly rounded value is finite for finite inputs. The old core two-SMA extrapolation was a different formula.

GeometricMovingAverage / GeoMa retains max(input,1e-6) for each observation and outputs zero until a full window exists. It now rounds the exact positive Length-th root of the exact product once. The product is stored as an odd integer times a power of two; expired factors are removed exactly. Production uses integer Newton roots and exact power comparisons at the binary64 midpoint. The independent reference multiplies rational factors and bisects representable outputs. The positive geometric mean lies between the clamped input extrema, so correctly rounded results remain finite. GeometricMeanMovingAverage uses the separate positive-only convention below.

GeometricMeanMovingAverage preserves source prices until its window is full. Thereafter it omits zero and negative values, returns zero if none are positive, and rounds the exact positive root using the count actually included. No 1e-6 floor applies to this variant: positive subnormal factors are retained. Exact product eviction tracks both exponent and included count; both geometric variants share the root-rounding implementation. The mean lies between the included positive extrema after startup.

### Fibonacci and square-root stages in composed averages

TMA and TriangularMovingAverage apply two full-period stages; SlowSmoothedMovingAverage applies its three split periods. Fibonacci and square-root choices preserve their individual zero-padded startup and round each stage before passing its output to the next. References calculate each stage independently using rational products and weights; square-root coefficients retain the rounded-coefficient contract described above. Shared discovery includes periods 1, 3 and 14 for both choices on all three types. These promoted cases do not exhaust all available component types or periods.

The same staged contract now covers parabolic, cubed and Quick moving-average components, with 27 further discovered cases at periods 1, 3 and 14 across the three composition types. Quick preserves its two nonzero startup taps at period one; composition must not replace this with identity. Each intermediate result rounds once before the next stage. Native preview calls preserve every stage until commit.

Shared discovery also exercises SMA, WMA, EMA and Wilder at those three periods on all three composition types (36 cases). Their startup and rounded stage semantics remain those of their base contracts. Explicit customer registrations still replace automatically generated cases for the registered type.

JSA and RMS (QuadraticMovingAverage) component choices have 18 additional discovered cases across these compositions. JSA retains its midpoint of current input and input Length bars ago, with zero for missing history. RMS uses the available sample count and nonnegative square root at each stage, so negative inputs do not imply negative results. Its bound is the maximum absolute stage input, rather than the signed input interval. Stage outputs round before the subsequent stage consumes them.

Wwma, Smma and ModifiedMa now expose the same Wilder component type. Their zero-seeded recurrence has unrounded constant-input residual (1 - 1/L)^n <= exp(-n/L). The declared settling horizon is 18L bars, saturated at Int32.MaxValue: this meets the existing price-50/absolute-1e-6 warmup convention, not an absolute-error promise for arbitrarily large prices or a proof that finite precision eliminates the residual. Formula validation still checks every startup output against its rounded reference.

Default KAMA components (fast period 2, slow period 30) add nine composition cases at periods 1/3/14. Every stage computes its own efficiency ratio from its own input trajectory and uses the rounded adaptive gain/convex update contract. These cases do not certify arbitrary custom fast/slow settings supplied to a component. The smoother selects KAMA price output, not its efficiency-ratio output.

### Farey weighted mean

Positive reduced fractions with denominator at most the order form a descending FIR kernel. Each coefficient retains the legacy binary64 quotient followed by Math.Round(quotient, 3); the contract is for these rounded coefficients, not ideal rational fractions. Exact coefficient-price products and the exact sum of coefficients are normalized with one final binary64 rounding. Missing history is zero. Nonnegative weights give a convex mean of available prices and startup zeros, hence a finite result for finite inputs. The oracle enumerates coprime fractions and sorts by integer cross-products independently of the production successor recurrence.

The number of taps is sum(phi(d), d=1..order), not twice the order. Warmup uses the conservative bound order*(order+1)/2 - 1, saturating at Int32.MaxValue without arithmetic overflow. The implementation materializes the Farey kernel; quadratic storage/time growth still limits practical orders. Empty core input returns without constructing the kernel.

### Middle-high-low moving average

The public formula averages the rolling maximum and minimum of the selected scalar input, then applies the selected smoother. Despite its name, it does not use the OHLC high/low fields. Midpoint formation uses an exact finite mean; the SMA stage now rounds an exact window sum, while other promoted components retain their individual rounded-stage contracts. Scalar and OHLC registry calls use the selected input/close trajectory and default EMA smoothing, matching the public formula. Shared discovery adds 52 configurations across 13 promoted components and four equal/unequal/reversed period pairs. Other component types remain outside this numerical enrollment.

### Inverse-distance named moving average

InverseDistanceWeightedMovingAverage preserves the public legacy distance-mass formula: each zero-padded window price x receives weight sum_j(abs(x-x_j)), not a reciprocal distance or a reciprocal lag. When all weights vanish the result is the current price. The repaired core, builder and native routes share this formula. Sorted integer-grid prefix sums produce exact weights; exact weighted accumulation and normalization round once. An independent rational oracle sums pairwise distances. All weights are nonnegative, so the normalized result lies within the padded window extrema and stays finite for finite inputs. Compressed startup zeros let the core evaluate short histories at very large periods without enumerating every missing observation. This does not claim unrestricted memory capacity for a fully materialized native window or competitor performance.

### Reciprocal-distance mean

DistanceWeightedMovingAverage uses reciprocal total absolute distance, unlike the inverse-distance-named indicator above. Exact distance sums D_i are formed on the binary input grid, then normalized coefficients are RN(min(D)/D_i). Products with prices, coefficient totals and the final quotient are accumulated exactly before final rounding. All-equal windows return the current price; startup retains padded zeros.

For a nonconstant window of n >= 2 prices, the triangle inequality gives D_max <= (n-1)*D_min: choosing points x,y, D(x) <= D(y)+(n-2)*abs(x-y) <= (n-1)*D(y). Thus normalized coefficients lie in [1/(n-1),1], so their binary64 rounding cannot overflow or underflow for supported Int32 periods. With u=2^-53, coefficient perturbation changes the ideal real reciprocal-distance mean by at most u/(1-u)*(max(input)-min(input)), plus final rounding. The bound is absolute; it does not promise relative accuracy at cancellation. Randomized exact-rational tests check this bound separately from exact agreement with the rounded-coefficient contract. This is an analytical derivation and finite testing, not a machine-checked proof of the implementation.

### Sequentially filtered moving average

This filter publishes the chosen average only after Length consecutive strictly positive or strictly negative changes in that average. Equal successive values break the run; the first mean is compared with zero. Other bars hold the prior filter output, seeded from the first source price. It is not TEMA. The SMA stage uses exact window sums and one rounding before direction decisions. The gate compares finite values directly, stores integer directions and selects an existing finite value without extrapolation. A separate trajectory reference checks complete sign windows independently of the rolling gate sum. Shared discovery includes 39 cases across 13 promoted components and periods 1/3/14; remaining component types are not enrolled by this batch.

NaturalMovingAverage uses `L(x) = RN(1000 * Math.Log(x))` for positive prices and zero otherwise, with zero prehistory. Movement is `RN(abs(L(x_t)-L(x_(t-1))))`; taps preserve the public formula's `RN(Math.Sqrt(j+1)-Math.Sqrt(j))`. Exact movement-tap products and totals determine a once-rounded ratio. The final blend `previous + ratio * (current-previous)` is accumulated exactly and rounded once, without forming the potentially overflowing price difference. Zero total movement gives ratio zero and returns the previous source price (zero initially). All taps lie in [0,1], so the ratio and blend are convex and the output remains finite for finite inputs. The contract is conditional on the platform's Math.Log/Math.Sqrt results, not a claim of correctly rounded real transcendental functions. Core now follows this public formula rather than its former unrelated log-weighted FIR. Native state uses O(length) storage; the batch path limits allocation to available history.

SineWeightedMovingAverage preserves the public lag order and rounded coefficients `Math.Sin((lag+1d)*Math.PI/(length+1d))`. Binary64 coefficients at symmetric mathematical positions need not be identical; the core no longer reverses them. Exact coefficient-price products and total coefficient mass are divided and rounded once. Missing history is zero padded. Positive weights keep outputs within the padded input extrema, including full maximum-finite windows. Math.Sin is a platform primitive in this contract; no correctly-rounded real sine guarantee is asserted. Coefficient construction and native storage remain O(length), so arbitrary maximum-int periods are not a resource guarantee; empty core input exits before allocation.

EhlersHannMovingAverage retains the public rounded coefficients `1 - Math.Cos(2*Math.PI*((lag+1d)/(length+1d)))`, zero-padded startup and full coefficient denominator. Exact coefficient-price products and coefficient total are divided with one final rounding. Nonnegative coefficients and positive total make this a finite convex mean; coefficients above one no longer overflow intermediate products. The builder now uses selected/chained input. Native smoother components share the same kernel. Math.Cos remains a platform primitive, and coefficient construction/storage are O(length); no arbitrary-size allocation guarantee is made.

ChandeMomentumOscillator computes exact differences between finite binary64 price endpoints (first change zero), exact sums of the last Length signed changes and their absolute values, and `RN(100 * signedSum / absoluteSum)`. A zero denominator returns zero. The triangle inequality proves the primary is in [-100,100]; no intermediate difference, total, or scaled numerator must fit in binary64. Eviction subtracts the original exact endpoint contribution. Signal smooths the rounded primary trajectory under the selected moving-average contract; the exact SMA stage preserves builder customer overrides. Both public aliases and native factories honor their signal periods. This corrects overflow and cancellation artifacts, so ordinary low-order bits may change too. Native storage is O(Length); the core limits storage to available history for short inputs with enormous periods.

VariableIndexDynamicAverage (Vidya) seeds the first price, uses the rounded Chande primary described above, and retains staged binary64 coefficients: alpha = RN(2/(Length+1)), ratio = RN(abs(CMO)/100), gain = RN(alpha*ratio). It then evaluates previous + gain*(current-previous) exactly and rounds once, without forming the price difference or rounding 1-gain. Because gain lies in [0,1], each output is a finite convex combination of finite prices. Core, legacy and native routes use this contract; a selected average affects the unused Chande Signal, not VIDYA's primary. Custom-average substitution is therefore rejected rather than silently ignored. Ordinary low-order bits can change from the former separately rounded products. Native storage is O(Length); the core caps history storage at the available input length.

ChandeMomentumOscillatorAbsolute returns zero for the first Length observations, then RN(100 * abs(current - price[Length bars ago]) / sum(abs(each of the last Length changes))). Zero travel returns zero. All differences, scaling and sums are exact; only the final ratio is rounded. The implementation uses the telescoping signed endpoint sum, while the independent reference uses direct displacement. Both imply [0,100] by the triangle inequality. This fixes the core's incorrect first eviction and overflowing native/legacy intermediates. Preview does not advance the startup counter; reset restarts it. Core short-history allocation is capped at available bars.

ChandeMomentumOscillatorFilter retains its historical gate: discard a change when abs(RN(current-previous)) exceeds the finite nonnegative filter (default 3); equality is retained, and the first change is zero. Accepted changes contribute exact endpoint differences to signed and absolute totals, with one final rounding of their scaled ratio. Overflow in the gate's subtraction is necessarily larger than every supported finite threshold and is discarded. A zero total returns zero; otherwise the result remains in [-100,100]. Signal uses the chosen rounded moving-average trajectory. Native and legacy direct filter arguments reject negative or nonfinite thresholds. This explicit rounded threshold convention differs from comparing unrounded real differences, especially adjacent to the boundary.

ChandeMomentumOscillatorAverage and ChandeMomentumOscillatorAbsoluteAverage include the first price as a change from zero. Each of three windows computes RN(exact signed change sum / exact absolute change sum), returning zero for zero travel. Their signed output is RN(100 * exact sum of those three rounded ratios / 3); the absolute variant takes its magnitude. These give [-100,100] and [0,100] bounds. V2's existing deprecated Length option remains explicitly ineffective: its declared windows are 5, 10 and 20. Legacy/native entry points retain their separate configurable lengths. The former internal core's unrelated EMA-smoothed oscillator is corrected to this three-window formula. Native storage is O(length1+length2+length3); core storage is capped by available history.

### Stochastic dependencies

Stochastic Regular publishes the exact clamped rolling range percentage as `Sco` and the selected moving average of that percentage as `Signal`. Its SMA stage rounds the exact window mean once. Alternate selected input follows the same range-selection contract as the full stochastic oscillator.

DiNapoli Preferred Stochastic starts both smoothing states at zero. Each stage computes `previous + ((input - previous) / period)` with binary64 rounding after subtraction, division, and addition; it does not replace these steps with a fused convex update. The raw percentage uses exact endpoint differences and one final rounded ratio, clamped to [0,100]. V2 exposes the range period and fixes both smoothing periods at three; direct legacy/native entry points retain separate smoothing periods. Independent rational references reproduce the declared rounding stages. This is a library variant contract, not authenticated primary-publication attribution.

Double Stochastic's second range window has a minimum of two observations even when the first range period is one. This matches the historical single-series extrema contract. The subsequent numerical rollout uses exact clamped normalization for this second range and once-rounded exact window means for both SMA stages. Other promoted bounded average kinds retain their own declared contracts. V2 fixes the two smoothing periods at three; direct legacy/native calls retain a configurable smoothing period.

Dynamic Momentum uses the stochastic `FastD` and `SlowD` stages, with the range period also controlling the first smoothing period. It tracks all-history extrema of FastD, takes their staged binary64 midpoint, subtracts the rounded SlowD-minus-FastD gap, and clamps to [0,100]. V2 fixes the second smoothing period at twenty. The independent reference recomputes each prefix extrema and explicitly rounds the arithmetic stages. These formulas remain library variant contracts rather than verified claims about a primary publication.

### Williams %R and representability

Williams %R retains the unclamped formula `-100 * (rollingHigh - selectedPrice) / (rollingHigh - rollingLow)` and the library's -100 result for an equal-endpoint window. Endpoint differences and the scaled ratio are evaluated exactly before a single binary64 rounding. Reversed endpoints preserve the formula's orientation. A supplied close outside the high/low range can produce a value outside [-100,0]; the library does not silently clamp it.

Only a mathematically unrepresentable final result produces signed infinity in the lower-level calculation/state result; the typed runtime rejects it with `IndicatorOutputException`. Independent rational references authorize that rejection through `ReferenceWithOverflowRejection`. They do not excuse intermediate overflow for an otherwise representable result. The builder uses the selected input and established custom-range convention, rather than reconstructing an unrelated ticker series.

### Price and volume rate of change

Price ROC uses `100 * (current - lagged) / lagged`, with zero during the initial lookback and when the lagged value is zero. Volume ROC applies the same formula to volume, independent of any chained close series. The endpoint difference, scale and division now round once at the final binary64 result. This avoids intermediate overflow for changes such as `-MaxValue` to `MaxValue` (which correctly yields -200 percent). An unrepresentable final ratio is rejected at the typed runtime boundary using an independent rational oracle; generated cases exercise both overflow signs for every configured period.

Coppock Curve, Pring Special K and Tick Line Momentum consume the corrected ROC arithmetic in their builder paths; Tick Line's native cumulative-count ROC is aligned too. This dependency repair does not upgrade Pring's complete composite error budget to exact identity. Its non-unit SMA implementations retain their existing bounded rounding behavior.

Stochastic Fast publishes the full stochastic oscillator's FastD as `Sfo` and SlowD as `Signal`. It inherits the exact clamped range normalization and exact SMA stages. Its two smoothing periods remain independently configurable; other promoted bounded moving averages keep their declared numerical contracts.

### Momentum ratio and Market Facilitation Index

Momentum and MomentumOscillator publish `Mo = RN(100 * current / price[Length bars ago])`, with exact intermediates, zero for the initial Length observations, and zero for a zero lagged price. This is a percentage ratio, not a price difference or ROC. Signal smooths the published ratio over Length using the selected average (WMA for the compact Momentum alias). SMA uses an exact partial-window mean; the other promoted averages retain their documented stage contracts. The deprecated SmoothLength option remains ineffective. A nonrepresentable ratio invalidates its dependent signal: lower-level batch output preserves the finite signal prefix and marks the suffix NaN; the typed runtime rejects the first overflowing Mo. Recovery after a rejected streaming observation is not part of this contract.

Market Facilitation Index publishes `Mi = RN((High - Low) / Volume)` with exact intermediates and one final binary64 rounding. Zero volume returns zero. Signed volume and reversed endpoints retain the formula's sign; the input contract does not impose candle ordering. The deprecated Length option remains ineffective. Typed price chaining keeps the original High, Low and Volume. A final unrepresentable ratio is rejected by the typed runtime; an overflowing intermediate range whose final quotient is finite must succeed. These are explicit library variant contracts; registration does not claim verified primary-source provenance.

PriceMomentum and VolumeMomentum publish the once-rounded difference between the selected price or volume and its value Length bars earlier. Initial Length observations return zero. Native binary64 subtraction already implements this final rounding; independent references use rational subtraction. Finite inputs whose difference exceeds binary64 range cause a typed output rejection. PriceMomentum honors the selected input; VolumeMomentum preserves volume under price chaining.

Range publishes RN(High-Low) at every bar. TrueRange publishes the same value on its first bar, then the rounded maximum of High-Low, abs(High-previous selected Close), and abs(Low-previous selected Close). The independent reference compares exact rational differences before rounding. With ordered candles this matches the usual true-range formula; reversed endpoints remain accepted and the first signed range can be negative. A genuinely overflowing range is rejected, including on the first bar. Length remains an ineffective deprecated option. The TrueRange builder reads current columns and the selected close rather than reconstructing stale ticker records.

BalanceOfPower publishes `Bop = RN((selected Close - Open) / (High - Low))`, evaluated with exact differences and division, then one final rounding. Equal High/Low returns zero. The result is unclamped: derived prices may lie outside the candle and reversed endpoints preserve orientation. `BopSignal` smooths the published ratio with the configured average and period, including exact partial-window SMA. Unrepresentable final Bop invalidates its dependent signal; the finite signal prefix is preserved and typed execution rejects the primary overflow. OHLC columns remain original under typed close chaining. Native rejection recovery is not promised. This formula is an explicit library contract, not a primary-source provenance claim.

NetVolume is zero on the first bar, then current Volume multiplied by the exact sign of the change in selected Close. Equal prices yield zero; signed volumes are preserved. The deprecated Length option remains ineffective.

NormalizedVolume returns zero before Length observations exist, then `RN(Length * current Volume / exact sum of last Length volumes)`, with zero for an exactly zero sum. Exact rolling accumulation prevents loss of small remainders and removes expired terms exactly; the moving average is not rounded before division. Signed volumes remain accepted. With three or more terms their cancellation can make the final ratio unrepresentable, which typed execution rejects. With one or two finite terms, a nonzero sum cannot cancel enough to overflow this normalized ratio, so those periods use ordinary finite reference contracts. Native previews operate on a copy of the exact sum and do not insert into the ring until finalization.

SimpleReturns publishes `RN((current - lagged) / lagged)` with exact intermediate arithmetic, zero during the first Length observations and zero for a zero lagged input. The independent oracle evaluates the equivalent exact quotient-minus-one formula. This prevents overflowing subtraction when the final return is finite. Genuinely unrepresentable returns are rejected by typed execution.

HighLowIndex compares the current rolling highest High and lowest Low with their previous values, starting both comparisons from zero. Over the same Length window it counts rising highs and falling lows, publishes their once-rounded percentage `100 * advances / (advances + declines)` (zero when both counts are zero), then applies the configured rounded average. Counts and their multiplication by 100 are exact within supported integer periods. SMA uses its exact partial-window stage. The core now represents the default EMA variant rather than an unrelated count of raw bar moves. This contract specifies the library variant, not verified primary-source provenance.

CumulativeSum publishes the once-rounded exact prefix sum of selected prices. CumulativeVolumeIndex begins at zero and accumulates current Volume with the sign of each selected-price change, publishing the once-rounded exact prefix total. Exact internal state preserves small contributions across later cancellation. Genuine prefix overflow is rejected at its earliest bar by typed execution; low-level arithmetic retains its exact state, but typed recovery after rejection is not claimed. CumulativeVolumeIndex keeps its ineffective deprecated Length option.

VolumeZoneOscillator uses zero signed volume on its first bar, positive current volume when selected price rises, and negative current volume otherwise (including unchanged prices). Both signed and total volumes use the declared EMA contract: rounded prefix means during initialization and the standard rounded recurrence afterward. The final percentage ratio of those published averages is rounded once, with zero for a zero total average. Nonnegative volumes imply outputs in [-100,100]; signed volume inputs may yield unbounded ratios and a genuine output overflow, which typed execution rejects. Period one cannot overflow this ratio.

Obv and OnBalanceVolume compare the first selected price with zero, adding, subtracting or retaining volume accordingly. Subsequent observations compare against the previous selected price. They retain an exact cumulative signed-volume total and round each published Obv once. ObvSignal applies the configured rounded average and period to that published trajectory, with an exact SMA stage. Genuine Obv overflow invalidates the dependent signal and triggers typed rejection; the lower-level batch signal preserves its finite prefix. Both aliases carry their configured average/period into native factories. Modified/disparity direct consumers use the corrected raw total, but their entire composite numerical contracts are not promoted by this dependency repair.

AverageDayRange returns zero before a full Length window exists, then the once-rounded exact mean of High-Low over that window. It does not round or overflow individual ranges before averaging. Signed/reversed endpoints remain accepted. Exact expiry of both endpoints preserves small ranges after a large range leaves the window. The two core entry points, legacy calculation and native state implement the same full-window formula; only genuinely unrepresentable final means cause typed rejection.

MoneyFlowIndex (Mfi and MfiCore) uses once-rounded exact HLC3 unless an explicit selected series is supplied. The first observation contributes no flow; increases enter the positive bucket, decreases enter the negative bucket, and ties enter neither. The last Length observations retain exact price-volume products. With P and N the exact bucket totals, zero N returns 100 (including both totals zero), otherwise zero P or zero P+N returns 0, otherwise the result is clamp(RN(100*P/(P+N)), 0, 100). Signed inputs preserve these declared conventions. No product is rounded before accumulation or expiry. This is an explicit library variant contract, not a primary-source provenance claim. Preview and reset preserve the same recurrence.

MoveTracker and PriceChange publish Mt = RN(selected Close - previous selected Close), with Mt = 0 on the first bar. Signal = RN(current published Mt - previous published Mt), using zero prehistory. The two rounding stages are intentional: the signal is not the single-rounded second difference of original prices. A genuinely overflowing primary or signal is rejected by typed execution. The legacy MoveTracker Length option is ineffective. Exact rational references recompute each declared stage independently; no claim of exact real arithmetic across the stage boundary is made.

LogReturns publishes the natural logarithm of the selected current price divided by the price Length bars earlier. Startup and any nonpositive endpoint return zero, preserving the declared legacy convention. Positive finite endpoints always have finite logarithmic returns, even when their raw ratio overflows or underflows. Production uses a corrected log1p expression near unity and exact binary exponent decomposition for wider ratios. An independent 192-bit fixed-point atanh-series reference checks a zero-absolute, 8e-15-relative, same-sign comparison budget. This is a numerical acceptance budget, not a certified error bound for every platform Math.Log implementation or a correctly-rounded transcendental claim. All execution routes must additionally agree exactly with one another.

PriceChannel uses the selected moving average as MiddleChannel. UpperChannel and LowerChannel are RN(center * (1 +/- Pct)), with exact multiplication/addition before the final binary64 rounding. The coefficient is not pre-rounded. The center remains finite when an outer band overflows; the typed runtime rejects only unrepresentable outputs that the chosen indicator exposes. SMA uses an exact full-window mean and zero startup until Length observations, while the other supported average kinds retain their declared stage contracts. PriceChannelMiddle/Upper/Lower preserve their named outputs and default EMA/Pct settings. No band ordering is promised for negative prices or signed percentages.

MovingAverageEnvelope uses the selected moving average as MiddleBand and once-rounded exact percentage bands `RN(center * (1 +/- pct))`. SMA retains zero startup until a full window and then rounds the exact window mean once. The 17 promoted bounded average variants have exact per-output references; the builder rejects unrepresentable exposed outputs. Custom component-average overrides remain effective.

RangeIdentifier retains the last breakout candle high/low while Close is strictly inside those bounds; equality at either bound replaces the anchor. MiddleBand is the once-rounded exact mean of the retained bounds, including finite extreme values and subnormals. Its deprecated Length remains ineffective. Preview updates do not replace the committed anchor.

WilliamsFractals and its up/down aliases publish exact binary flags for the five supported plateau shapes. The center is reported after `max(2, Length)` bars, requires real preceding observations and two strictly lower confirming highs (or reflected lows), and uses exact price equality for plateaus. Adjacent representable prices are distinct, including subnormals and extreme finite magnitudes.

GannSwingOscillator and GannTrendOscillator use trailing partial-window high maxima and low minima. A strict local minimum across three consecutive rolling highs selects +1; a strict local maximum across the rolling lows selects -1. Simultaneous events select +1, ties retain the preceding state, and absent historical extrema are zero.

TFSTetherLine and TFSTetherLineIndicator publish the once-rounded exact midpoint of the trailing partial-window high maximum and low minimum. Both core overloads implement that midpoint. Typed `.Of()` replaces Close and preserves the bar extrema; legacy `UseInput` applies the documented synthetic-range rule when its selected value is outside the bar range. Both legacy builder aliases now honor that rule.

ALMA uses zero-padded Gaussian weights. For oldest-first tap j, binary64 z = (j / Length - Offset * ((Length - 1) / Length)) * Sigma and w = exp(-0.5*z*z). At Offset = 0.5, z = (j - (Length-1)/2)/Length*Sigma instead uses centered integer or half-integer positions, so mirrored coefficients agree exactly. Products and the full coefficient sum are accumulated exactly, then the weighted quotient is rounded once. This canonical coefficient evaluation replaces independently rounded native/batch/core normalizations, so legacy outputs may change by low bits. Sigma zero retains uniform zero-padded weights; zero total weight retains the legacy zero output. The contract is for these binary64 coefficients, not correctly rounded real-valued exp.

LeoMovingAverage uses independently once-rounded WMA and full-window SMA components, then RN(2*WMA - SMA) with exact final combination. WMA keeps its zero-padded startup; SMA remains zero until the window is full. Thus startup may exceed the finite output range even on constant finite prices; the V2 output boundary must reject that first unrepresentable bar. Intermediate doubling must not overflow when the combined result is finite.

Dema and Zlema retain this library's two-stage definition, RN(2*EMA1 - EMA2); Zlema is not the alternative lag-adjusted-price EMA formula used by some platforms. Tema publishes RN(3*EMA1 - 3*EMA2 + EMA3). Each EMA stage follows the existing rounded EMA recurrence; the final integer-weighted combination is exact before its single rounding. Finite cancellation must survive overflowing individual products, and an unrepresentable final result must be rejected by the V2 output boundary.

Apo, AbsolutePriceOscillator and PriceOscillator publish RN(fast average - slow average). Promoted component types use independently rounded bounded stages, including exact SMA windows; fast and slow periods remain meaningful when reversed. The selected/chained input reaches both windows. References require rejection when their exact difference is unrepresentable.

HammingMa/EhlersHammingMovingAverage retain this library's sine-pedestal window, with default pedestal 3. At lag j, t=min(j, Length-1-j)/(Length-1), the binary64 coefficient is sin(Pedestal*(1-2*t) + PI*t); Length one has coefficient one. The mirrored coordinate enforces the sine window's exact symmetry. Interpolating the phase avoids overflowing 2*Pedestal for finite parameters. Zero-padded products and the full coefficient sum are exact before final quotient rounding. Signed coefficients can produce unrepresentable results; zero total coefficient weight retains the legacy zero result. This specifies binary64 coefficients, not a correctly rounded real-valued sine guarantee.

Elder Ray: BullPower = RN(High - MA(Close)); BearPower = RN(Low - MA(Close)). The selected bounded average is rounded at each stage, and subtraction is rounded once; SMA is zero until a full window. Typed `.Of()` replaces Close while preserving bar High/Low. Legacy selected/chained inputs use the existing synthetic-range rule. Only published outputs participate in overflow rejection.

Detrended Price Oscillator: RN(Close[i-lag] - MA(Close)[i]), with missing lagged values zero and lag = clamp(ceil(length/2 + 1), 2, 530), preserving the published legacy variant. The selected bounded moving average rounds each stage once. In particular, SMA is zero until the full window; startup DPO is the negative mean when the lagged value is unavailable. The span core now follows this same odd-period and startup convention.

LSMA: RN(3 * RN(WMA) - 2 * RN(SMA)), combining rounded stages exactly before the final rounding. WMA has a full zero-padded denominator; SMA is zero until a full window. The span core and public LsmaCore registry now share this published startup convention rather than partial-window linear regression. The six existing regression-based core consumers now call LinearRegression explicitly, preserving their partial-window fit; their separate numerical contracts remain pending. A full-window constant of any finite magnitude cancels to that constant; startup extrapolation can genuinely overflow and must be rejected at the public boundary.

Simplified LSMA: the once-rounded zero-padded OLS endpoint, with exact integer tap numerator `6*(N-lag)-2*(N+1)` and denominator `N*(N+1)`. This is the real-arithmetic finite-window reduction of the legacy double-cumulative formula. The implementation maintains exact rolling numerator and six-times-window sum in constant time, so departed extremes leave no rounding residue and cumulative totals cannot overflow. Unlike LSMA, this variant does not round WMA and SMA separately and has no full-window SMA startup gate.

Variance: once-rounded rolling population variance about the exact window mean, zero until a full window. Exact integer sums and squares give `(N*sum(x*x)-sum(x)^2)/N^2`; no rounded intermediate mean or floating square is formed. This avoids translation bias and intermediate overflow. A variance below the subnormal rounding threshold becomes zero; a genuinely unrepresentable variance is rejected at the public output boundary. This is distinct from taking the square of an already-rounded deviation.

StandardError: once-rounded square root of the exact OLS residual mean square (divisor N), zero until the full window. StandardErrorCore / StandardErrorOfTheMean: once-rounded square root of population variance divided by N, also zero until the full window. Both retain population rather than sample/degrees-of-freedom denominators. Exact centered moments avoid rounding a fitted line, variance, or standard deviation before the final result. Root quantization compares exact squared midpoints; normalization bounds the integer-root operand to 106 bits.


## Edge Preserving Filter

The Ooples variant averages prices over segments. Subtract the selected moving average from each price, round that offset, and fit an ordinary least-squares line to its absolute values over the available trailing `smoothLength` observations. Round the fitted endpoint. A peak is a nonzero trailing maximum whose rounded endpoint/maximum ratio differs from one by at most `1e-12`. Restart the segment when entering a peak from a non-peak and the current offset is nonzero. An uninterrupted peak plateau does not restart it.

Before the first restart, the segment includes an extra copy of the initial price. Thus period-one SMA, which has zero offsets and no restarts, gives `[3, 4, 3]` for `[3, 6, 0]`. This startup weight and the peak tolerance are explicit library conventions, not universal mathematical necessities. This definition does not authenticate an original author's publication. The old core's volatility-threshold EMA was a different formula; the core now uses this segment definition with SMA and regression length 50.

Each segment output is its exact sum divided by its observation count, rounded once to binary64. With finite prices it stays within the segment's extrema, including its seed, even if the sum exceeds binary64 range. Offsets and regression endpoints retain binary64 precision with an extended upper exponent so overflow cannot corrupt edge detection. Windows allocate only observed history. Periods below one are clamped to one consistently across routes.

The independent reference scans each window, computes centered covariance and variance for the fit, and reconstructs each segment explicitly. Production maintains rolling moments, a maximum deque and an exact running sum. Hand tests check the startup weight and peak entry/reset sequence separately from route parity.


## Ehlers Hurst Coefficient

Let `L` be the clamped range period and `h = ceil(L/2)`. The recent half uses the last `h` available prices, the full range uses the last `L`, and the older half uses lags `h` through `L-1`. Missing older-half samples are zero; until lag `h` exists, its initial anchor is the current price. These startup and odd-period rules describe the Ooples variant. They are not an assertion of a universal Hurst estimator, nor is its output constrained to `[0, 1]` during startup.

For positive ranges, compute the exact rational ratio `(recentRange + olderRange) * L / (fullRange * h)` before logarithmic evaluation. This cancels divisors without erasing subnormal ranges. Normalize that ratio to `2^e * m`, with `1 <= m < 2`, round `m` to binary64, and evaluate `e + Math.Log(m)/Math.Log(2)`. Average this result with the preceding dimension, then compute raw Hurst as `2 - dimension`. A zero full range or zero sum of half ranges carries the prior dimension. In particular periods one and two have zero half ranges and raw Hurst two.

Smooth raw Hurst with the existing two-pole form. For platform-rounded `r = exp(-sqrt(2)*pi/s)` and `c = cos(min(sqrt(2)*pi/s, .99))`, retain the exact coefficient polynomial `g = 1 - 2*r*c + r*r`, feedback `2*r*c` and `-r*r`. Round the complete recurrence once per observation, with zero initial Hurst/filter history. This prevents the small DC gain from cancelling at large smoothing periods. Signals compare consecutive output slopes. All three extrema windows and the delay queue grow only with observed history.


## Ehlers Convolution Indicator

The Ooples definition filters prices with a second-order high-pass followed by a two-pole low-pass, then correlates the available trailing roofing values with their one-bar predecessors. The missing first predecessor is zero. For fewer than two pairs or zero variance on either side, correlation is zero. Otherwise Pearson covariance and both variances are formed exactly, and their normalized quotient is rounded through a square root. A rounded correlation `r` maps to `exp(3*r)/(exp(3*r)+1)/2`, retaining the binary64 exponential/add/divide stages. This confines `Eci` to approximately `[0.0237, 0.4763]`; neutral correlation gives `0.25`.

For the high-pass, retain the exact polynomial of `p = cos(a)/(1+sin(a))`, where `a = min(.99, sqrt(2)*pi/highLength)` and `p` is platform-rounded. Its drive is `(1+p)^2/4` times the exact second price difference, and its feedback is `2*p` and `-p*p`. The low-pass uses the exact polynomial of platform-rounded radius `exp(-sqrt(2)*pi/lowLength)` and cosine, with the same two-pole coefficient convention as the Hurst coefficient but without its angle cap. Round each complete filter recurrence to binary64 precision with an extended upper exponent. Keep those unpublished stages through correlation and signal calculations; publishing an overflowing intermediate would destroy a finite normalized result.

`Slope` compares the current roofing value with lag `ceil(n/2)`, where `n` is the available correlation-window size. It is `-1` only when the exact difference exceeds `1e-12 * max(1, abs(current), abs(previous))`, and otherwise `1`. This absolute floor is a library convention, so slope need not be invariant under price rescaling. Trade signals compare consecutive roofing slopes. History grows with observations, retains the active correlation window and its predecessor, and compacts amortized storage; preview updates commit no state.

The reference recomputes centered correlation windows and rational filter recurrences. Production maintains rolling integer moments and indexed history. Separate hand cases establish neutral and two-point correlations; regressions cover exact power-of-two rescaling away from underflow, extended stages, tolerance boundaries and compaction.


## Ehlers Median Average Adaptive Filter: public adaptive-period variant

The public definition first forms `(price[t] + 2*price[t-1] + 2*price[t-2] + price[t-3])/6`, with zero price prehistory. Round that complete convex mean once. Starting at the requested period (clamped to one), consider periods decreasing by two. Each candidate is `(2*smooth[t] + (period-1)*previousCandidate)/(period+1)`, rounded once, using the same previous-bar candidate throughout the search. Compare it with the median of the available trailing smoothed observations; even medians round the complete midpoint once. The relative error comparison `abs(median-candidate) <= threshold*abs(median)` is exact, so neither an overflowing difference nor an underflowing quotient can choose the wrong period.

The inherited initialization and stop conventions are explicit: initial error is binary64 `0.2`; a zero median preserves the prior error; a threshold at least `0.2` skips the search and stores candidate zero. A negative finite threshold exhausts all positive candidate periods. Subtract two after the last candidate, then floor the resulting output-smoothing period at three. Apply the same once-rounded convex recurrence to the previous filter output. Thresholds must be finite. These conventions describe the Ooples public variant; they are not authenticated original-author provenance. The separate core moving-average implementation still differs and awaits its explicit scope decision.

All complete means are convex combinations of finite values, so their correctly rounded results are finite even when naive weighted sums overflow. Signals compare exact current and previous price-minus-filter residuals. Binary64 underflow can still remove a tiny rounded stage; no scale-invariance claim crosses that rounding boundary.

Startup periods at least as large as the available history share one median. Their rounded candidates are monotone as the period decreases. Production binary-searches entry into the acceptable interval, verifies its other endpoint, and then enumerates shorter periods with shrinking trailing medians. This costs logarithmic work in the nominal startup period plus ordered-window work based on observed history, rather than billions of iterations at the maximum integer period. History grows with observations and compacts after expiration. Preview commits no state. The independent rational reference exhaustively scans candidate periods; maximum-period hand cases separately verify finite results without nominal-sized allocation.


## Enhanced Index

The Ooples public variant forms a chosen moving average of selected prices with period `clamp(ceil(length/2), 2, 530)`, then publishes `Ei = 2*(price-mean)/(highestHigh-lowestLow)` over the available trailing range window. The [2,530] mean-period clamp is an inherited library convention, not an authenticated original-author requirement; it is evaluated without integer overflow. Periods are otherwise clamped to at least one. A zero range produces zero. Finite signed or inverted candle ranges retain the signed quotient; selected prices can lie outside the original candle range. No bounded-oscillator claim applies to all those inputs or to the SMA zero-startup convention.

For SMA/WMA/EMA/Wilder, moving averages use the existing rounded extended stages. Form both differences and the factor-two numerator exactly, divide before publishing, and round the complete quotient once with binary64 precision and extended upper exponent. Keep that unpublished line through the signal average, so opposite large line values can cancel to a finite signal. Only mathematically unrepresentable final values require the existing overflow-rejection policy. Trade signals compare exact consecutive line-minus-signal spreads.

Lazy monotone extrema and average histories allocate with observed data. Batch consumes no component-average overrides. The fast main output consumes one requested price-mean component; its signal output consumes that component followed by the line mean. Override/fallback boundaries receive published binary64 arrays, as required by that interface. Selected prices use the existing per-bar custom-range rule; original candle fields remain available. Native validation rejects all nonfinite candle fields before state mutation; preview/reset preserve ranges, averages and signal memory.

The independent reference recomputes each trailing extremum window and rational quotient, then smooths its independently computed line. Tests include hand seed/zero/signed-range cases, wide and subnormal fields, expiring extrema, extended cancellation, extreme periods and clamp boundaries, both output routes, selected inputs, custom-component order and legacy-average parity. This establishes arithmetic for the stated public variant, not original-author provenance.


## Enhanced Williams R: public price/volume variant

Let P be twice the difference between selected price and its chosen mean divided by the trailing selected-price range; let V be the corresponding volume quantity. A zero range defines its ratio as zero. Ranges use at least two observations of capacity, with available startup data. Mean periods retain the Ooples clamp: ceil(length/2), limited to [2,530]. Means use the existing rounded SMA/WMA/EMA/Wilder stages. Let S be twice the current price change divided by its range (zero at startup or zero range), and A be 0.25 for length below ten, otherwise length/32 - 0.0625.

When V is positive, P has the same strict sign as the price change, and S+A is nonzero, the public expression (50*P*(S+A)*V + S+A)/(S+A) equals 1+50*P*V. Otherwise the output is 50+25*P*(V+1). The zero-factor guard remains part of the definition: it selects the other branch and cannot be discarded while cancelling. Keep P, V and S rational through the complete expression, then round the output once with binary64 precision and extended upper exponent. Publishing the separate ratios first could turn a finite product into zero or infinity. The independent reference evaluates the uncancelled rational expression.

Smooth the retained extended output to obtain Signal. Compare exact consecutive output-minus-signal spreads, with strict crossings of -100 and +100 after the stronger slope rules. The first previous output and spread are zero. Flat price/volume ranges give output 50. This public price/volume variant is not conventional bounded Williams R; signed fields, moving-average startup, and selected inputs can produce values outside its familiar bounds. Its inherited period clamps and initialization are explicit library conventions, not authenticated author provenance.

Histories allocate with observations. Batch consumes no component overrides; fast main requests price mean then volume mean, and fast Signal adds the output-mean request. Component/fallback interfaces exchange published binary64 arrays. Native candle validation precedes state changes; preview/reset preserve all four extrema histories, means and signal decisions. Supported legacy fallback routes retain their existing contracts. The distinct legacy core method takes highs/lows/closes but no volume and computes smoothed Williams R; it is unchanged and requires a separate API/formula scope decision.

Hand cases establish the exact zero-factor fallback and strict threshold crossings. Additional tests establish finite 150, 201 and -199 outputs despite individually unrepresentable normalized factors, extended signal cancellation, wide/subnormal signed fields, expiry, extreme periods, callback ordering and both output routes. Final unrepresentable outputs retain the existing overflow-rejection policy.


## Ergodic Commodity Selection Index: price-normalized public variant

Use the existing ADX directional stages: rounded extended true range and directional movement, chosen moving averages, bounded published directional percentages, rounded DX and smoothed ADX. The first candle supplies its own previous high, low and selected close. Let N=max(1,length), M=max(1,smoothLength), P=pointValue, C=selected close, T=current true range and A=current ADX with previous A initially zero. For C>0, Ecsi is the complete quotient 100*P*(A+previous A)*T/(2*sqrt(N)*(150+M)*N*C). Evaluate that quotient exactly using the platform-rounded square root, then round once to binary64 precision with extended upper exponent. Do not round the scale coefficient before multiplying; do not evaluate 150+M with signed 32-bit addition. For C<=0, the existing library variant defines Ecsi as zero. This is an explicit library convention, not a signed-price extension or an authenticated original-author definition.

Signal smooths the retained extended Ecsi with the same average and period M. Trading signals compare successive Signal slopes. Histories grow with observations, including extreme periods. The selected-price candle-range convention is shared across routes. PointValue must be finite, including for empty input; zero and negative finite values remain supported. Native candle validation precedes state mutation, including previews. Main and Signal fast outputs consume four and five component overrides respectively; batch retains four directional overrides and its ordinary signal smoother. Legacy component interfaces exchange published doubles. Values that cannot be published finitely follow the existing output-overflow rejection contract.

Hand cases with N=M=1 and P=151 establish 0,5000,20000 for candles (H,L,C)=(2,0,1),(3,1,2),(2,0,1), and downward/upward gap results 22500/4500. With P and C both equal to the least positive subnormal and M=int.MaxValue, the second output is the rounded quotient 10000/(150+M), despite the standalone scale coefficient underflowing. An extended line above double.MaxValue can still yield a finite period-two Signal.


## FX Sniper: CCI followed by a six-stage Tillson recurrence

The input is rounded typical price (high+low+close)/3 unless an input series is selected. SMA CCI uses the exact current-window mean absolute deviation, with zero until its full clamped period is present; the other averages retain the library's smoothed-absolute-residual variant with constant 0.015. Zero deviation defines CCI as zero. Preserve an extended rounded CCI when a custom component makes the denominator tiny.

For L=max(1,t3Length), each of six stages starts at zero and advances by the complete convex quotient (4*input+(L-1)*previous)/(L+3). Round each stage once with binary64 precision and extended upper exponent; do not independently round the gain and its complement. Evaluate the coefficient polynomial (1+b)^3*e3-3*b*(1+b)^2*e4+3*b^2*(1+b)*e5-b^3*e6 exactly, then round the output once. This preserves period-one identity for any finite b, including values whose cubic coefficients are individually unrepresentable. The equivalent expanded coefficient expression is used by the independent reference. Signal decisions compare the current and previous retained output with initial previous zero.

Periods clamp to one, histories allocate with observations, and b must be finite before any component callback or empty-input processing. Batch consumes no component overrides. Fast SMA also consumes none; other fast averages request price mean then mean absolute residual. Legacy fallback/component boundaries continue to exchange published doubles. Native selected-input mode survives reset; all candle fields are checked before a preview or final update can change state. Formula provenance here establishes the explicit library CCI/Tillson variant and algebraic identity, not authenticated original-author calibration or trading efficacy.

Hand cases with prices 2,4,2 and CCI period 2 give CCI 0,200/3,-200/3. T3 period 1 returns those values even at b=+/-double.MaxValue. With T3 period 5, b=0 selects the third pole (opening nonzero value CCI/8), while b=-1 selects the sixth (CCI/64). A custom CCI beyond the public double range can yield a finite third-pole result at extreme smoothing periods; publishing CCI first would lose that recovery.


## Fast/Slow Degree Oscillator: public time-dependent sine weights

Let N,F,S,M be the positive clamped main, fast, slow and signal periods. At one-based observation n, form d[n]=(sin(pi*n*n/N)-sin(pi*n*(n-1)/N))/n, rounded once to binary64 precision. Phase numerators and the observation counter are integers. Reduce modulo 2N, reflect to the first quadrant, preserve exact zeros and quarter turns, and evaluate other sine values at the once-rounded complete angle Math.PI*residue/N. This explicit platform-trigonometric coefficient contract avoids growing-angle argument error and spurious nonzero values at integer multiples of pi.

The public fast and slow weights have identical quadratic terms. Cancel those terms before rounding: multiply the previous selected price (initially zero) by the exact difference between the trailing F-term and S-term sums of d. Round that product with binary64 precision and extended upper exponent. Fsdo is the rounded trailing N-term sum of those retained products. This remains an absolute-time construction; constant prices do not generally imply zero output. Equal fast/slow periods and main period one do imply exactly zero output. The independent rational reference retains the shared polynomial in both legs and cancels it through complete subtraction.

Signal smooths the retained extended Fsdo over M with the chosen average. Histogram publishes the exact Fsdo-Signal difference, and trading signals compare successive exact spreads. The existing polynomial hand case's histogram is 2-(rounded 2/3), not rounded 4/3: the prior signal stage is already binary64. All three outputs retain the existing unrepresentable-output rejection policy. Histories grow with observed data; preview/reset include the integer counter, all three sums, price lag and signal mean. Native validation checks all candle fields before state mutation.

Batch consumes no component overrides; fast Fsdo consumes none, while fast Signal/Histogram request one signal component. Supported default signal stages retain extended values even when the callback list has no remaining override. Legacy/component interfaces exchange published doubles. All legacy periods remain independently testable, while the typed specification exposes its existing main-period parameter and defaults for the other periods. The separate internal core currently computes atan of a moving-average ratio; it remains unchanged pending its explicit formula/API scope decision. No original-author provenance is claimed for either variant.

With N=2,F=1,S=2,M=2,SMA and prices 2,4,6,8, Fsdo is 0,-2,-2,-2, Signal is 0,-1,-2,-2 and Histogram is 0,-1,0,0. Reversing F/S negates Fsdo. At N=4,F=4,S=1,M=4, four double.MaxValue inputs give an unrepresentable fourth Fsdo but finite Signal and Histogram, demonstrating why the shared stage must not be published prematurely.


## Fear and Greed: directional true-range stages

True range is max(high-low, abs(high-previous selected price), abs(low-previous selected price)). At startup the previous price equals the first selected price. Assign the range to the up leg only on a strict price rise, to the down leg only on a strict fall, and to neither leg on a tie. Smooth each leg independently over the fast and slow periods. Round fastUp-fastDown and slowUp-slowDown independently, then round their difference to obtain Fgi. Signal smooths retained Fgi. Trading signals compare Signal with zero and with its previous value, initially zero; they do not compare successive Signal slopes.

Supported SMA/WMA/EMA/Wilder stages retain binary64 precision and subnormal rounding with an extended upper exponent until publication. Keeping the four mean stages separate preserves their rounding contract; combining them into one signed-range smoother by linearity would generally change floating-point results. Unrepresentable individual ranges or means can cancel into finite outputs. Histories allocate only observed data, and preview/reset cover all five means and directional history. All periods clamp to one. A selected price inside the original candle retains that high/low range; an outside price uses the span between current and previous selected values (the first bar uses its current value). Batch and fast routes apply the same per-bar rule. Native states validate all candle fields before mutation.

Batch consumes no component overrides. Fast Fgi requests fast-up, fast-down, slow-up, slow-down; fast Signal additionally requests the final mean. Exhausted overrides fall back to extended supported means. Actual callbacks and legacy averages exchange published doubles. No original-author provenance is asserted. With SMA fast1/slow2/signal2 and candles (H,L,C)=(2,0,1),(4,2,3),(8,6,7),(5,3,4),(9,1,4), Fgi is 0,1.5,1,-4.5,2 and Signal is 0,.75,1.25,-1.75,-1.25.


## Fibonacci Retrace: complete affine interpolation

For trailing highest H and lowest L over positive-clamped length2, UpperBand is H+factor*(L-H), and LowerBand is L+factor*(H-L). Evaluate each complete expression exactly from the binary64 inputs, then round once to binary64 precision with an extended upper exponent before publication. Do not round the width, product or complement 1-factor first. This preserves finite interpolations across opposite extreme prices, exact factor-zero/one endpoints, midpoint cancellation and flat-range invariance even with extreme finite factors. Factors outside [0,1] retain extrapolation semantics; NaN and infinities are rejected, including empty input.

The selected moving average over positive-clamped length1 controls trading signals only. Retain its supported SMA/WMA/EMA/Wilder stages and the rounded bands when comparing mean-minus-upper and mean-minus-lower margins with their previous values, initially zero. The inherited priority is strengthening bullish margin, strengthening bearish margin, positive bullish margin, negative bearish margin, then None. UpperBand and LowerBand remain independent of mean parameters. Lazy extrema and means allocate only observed data; preview/reset retain no speculative state. Legacy mean kinds retain their existing double-valued mean interfaces.

Selected prices inside the original candle retain its high/low; outside prices use the span between current and previous selected values (the first bar uses its current value). Batch and fast apply this same range rule. Native inputs validate all fields before state mutation. Batch and fast consume no component overrides. With length2=2,factor=.25 and (H,L)=(8,0),(12,-4),(5,1),(6,2), UpperBand is6,8,8,4.75 and LowerBand is2,0,0,2.25. With H=MaxValue,L=-MaxValue,factor=.5 both bands are exactly zero; with H=L=MaxValue both remain MaxValue for any finite factor.


## Firefly Oscillator: variance-normalized weighted price

Weighted price is the once-rounded exact (high+low+2*selectedPrice)/4. Let C be its chosen rounded mean over N and V the exact population variance of the last N rounded weighted prices. Before N observations, or when V is exactly zero, standardized price is round(100*(weighted-C)); otherwise it is the once-rounded signed square root of (100*(weighted-C))^2/V. Compute the complete quotient without publishing a rounded deviation or variance. In particular a subnormal nonzero variance must not select the zero-variance fallback. Production uses exact integer moments and integer-root rounding; the independent reference uses centered rational differences and ordered-binary64 square-root bisection.

Smooth standardized price twice over M and then over N with the chosen mean. Default ZeroLagExponentialMovingAverage is the library's two-EMA extrapolation: round(2*EMA1-EMA2), retaining each EMA stage and its arithmetic-mean startup. SMA/WMA/EMA/Wilder and default zero-lag stages retain binary64 precision and subnormal rounding with extended upper exponent. Fo rounds the complete affine (filtered+100)/2-4; Signal is the trailing M-bar maximum of retained Fo. Signals compare consecutive Fo slopes; the inherited RSI threshold crossings imply those same slope directions. Supported histories allocate only observed data, and preview/reset cover all means, moments, maxima and slope memory.

The previously unrelated core ATR-normalized close formula is intentionally replaced with this public weighted-price/std-dev formula, using the existing length and smoothLength arguments and the public default zero-lag mean. All core candle/output spans are checked before writing; exact in-place close/output replay is supported. Native states validate all candle fields first. Batch and fast use the selected-price per-bar range rule. Batch consumes no overrides; fast Fo and Signal both request four means in order N,M,M,N. Exhausted overrides retain extended defaults; actual callbacks and legacy means exchange published doubles.

For SMA N=2,M=1 and collapsed candles0,2,0,2,2, Fo and Signal are46,71,46,46,71. For collapsed candles0,epsilon, the rounded SMA center is zero but the exact deviation is epsilon/2, so standardized price is200 and the second Fo is96. At N=1 standardized price is zero and both outputs stay46. Sustained MaxValue inputs with WMA can initially publish unrepresentable values but recover to46 after the startup history expires.


## Fisher Least Squares: residual balance and population regression

Let d[t] be the rounded price minus the previous retained estimate, with the first previous estimate equal to the first price. Over the trailing N observations compute z=sum(d)/sum(abs(d)), or zero when the denominator is zero. The equal averaging divisors cancel. Round z to binary64 and evaluate rho=tanh(z), the inverse Fisher transform. Using exp(2*z)-1 loses small nonzero residual imbalances through cancellation.

Let P and I be the selected rounded means of price and absolute bar index. Both population variances use full windows. Before N observations or with zero price/index variance, correction is zero. Otherwise round the complete signed correction (t-I)*rho*sqrt(Vprice/Vindex), then round P+correction. The complete square-root quotient preserves nonzero subnormal price variance without publishing an intermediate variance or deviation. Full consecutive-index variance is exactly (N*N-1)/12; production cancels it algebraically, while the independent reference computes centered rational index differences. Retained residuals, means, corrections and estimates keep binary64 precision with extended upper exponent; native double/subnormal rounding is preserved. Exact signed/absolute residual sums and price moments expire lazily.

Batch, fast, native and core use the same recurrence. Core now follows the public full-window startup instead of computing partial-window variances. Its output span is validated before writes and exact in-place replay is supported. SMA/WMA/EMA/Wilder keep their existing startup means. Native validation rejects invalid candles before changing state; previews and reset cover means, moments, residuals and signal history. Trades compare retained price-minus-estimate margins, including the historical first previous margin of minus the first price. Fast requests price and index mean callbacks in that order; batch requests none. Exhausted overrides retain supported default precision, selected prices are resolved normally, and fallback means restore caller inputs.

For SMA N=2 and prices0,2, estimates are0,1+tanh(1). At N=1 the estimate is the price. Flat price4 with SMA N=3 produces0,0,4. With prices0,1,-BitDecrement(1), externally supplied price means0,0,0 and index means0,1,1, the final estimate is2^-53; the exponential-subtraction transform would incorrectly give zero.


## Fisher Transform Stochastic: regularized rainbow inverse transform

Apply ten successive selected means with period N. Round their complete weighted average with weights5,4,3,2,1,1,1,1,1,1 and divisor20 once. Find its trailing R-bar extrema; retain each rounded rainbow-minus-low numerator and high-minus-low denominator, then sum these over M bars. Percent is the once-rounded100*sum(numerator)/(sum(denominator)+0.0001), bounded to0..100. The positive binary64 regularizer0.0001 is part of the published formula: it intentionally suppresses tiny ranges and is not removed or replaced by a zero-range branch.

The inverse Fisher transform maps percent to100/(1+exp((50-percent)/5)). Round the complete exponent before exp and the complete final ratio after adding one to that rounded exponential. This equivalent logistic form avoids subtracting nearly equal exponential values. Supported SMA/WMA/EMA/Wilder stages preserve ordinary/subnormal binary64 precision with extended upper exponent. Exact integer range differences and sums prevent overflow poisoning; histories allocate only observed bars, and monotone extrema use a nonoverflowing counter. Native validation, previews/reset, period clamping and selected-input routes agree. Signals compare consecutive retained slopes; the inherited30/70 threshold crossings imply the same directions.

Fast consumes ten mean callback slots in sequence, all period N; each callback receives the preceding stage. Batch consumes none. Exhausted overrides preserve supported defaults, and legacy mean fallback restores the caller's selected input. The public builder currently exposes N; public batch/native/fast retain all three existing periods and their defaults N=2,R=30,M=5. The separate logarithmic high/low core is unchanged pending the explicit formula-alignment decision.

With N=1,R=2,M=1 and prices0,0.0001,0,0.0001, output is100/(1+exp(10)),50,100/(1+exp(10)),50. At R=1 or with an all-zero rainbow, output stays at100/(1+exp(10)), approximately0.00454. Flat input need not imply a flat rainbow during a mean's startup; Wilder's ten-stage startup approaches its eventual value gradually.


## Freedom of Movement: two population standardizations and normalized ranges

Relative volume uses full-window exact population variance. SMA uses its exact population center, consistent with the Relative Volume indicator; other selected means and actual component overrides use their rounded center. Round the complete signed standardized quotient, retaining an extended upper exponent when needed. Absolute price movement is the once-rounded absolute (current-previous)/previous, or zero for a zero previous price. It is evaluated without publishing a potentially overflowing difference or quotient.

Over N bars, normalize each retained movement and relative-volume value with the complete affine1+9*(current-min)/(max-min), rounded once. An exactly flat range maps to zero. The ratio is rounded normalizedVolume/normalizedMove, or zero for a zero normalized move. Fom is the complete standardized score of the last N rounded ratios about their exact population center, not a prematurely rounded mean/deviation. Before N observations or at exactly zero variance, the score is zero. Dpl starts at the first selected price and updates to the previous selected price whenever published Fom>=2; otherwise it holds. Signals compare exact retained price-minus-Dpl margins, including margins exceeding binary64.

Supported SMA/WMA/EMA/Wilder keep their startup conventions. Lazy mean, moment and monotone-extrema histories permit extreme periods with short input. Native candle validation occurs before state changes; previews/reset cover all stages. Batch consumes no callbacks; either fast output requests one volume-mean slot with period N. Actual overrides replace only the volume center, while exhausted slots preserve the default exact SMA center. Selected prices do not replace volumes. Legacy means run through their native smoother on the original volume series without changing caller inputs.

Prices1,2,6,6 with volumes1,2,3,6 and N=3 give Fom0,0,sqrt(2),19/sqrt(182), with Dpl1 throughout. At N=5, prices1,2,3,4,8 and volumes1,1,1,1,2 give Fom0,0,0,0,2 and Dpl1,1,1,1,4: equality at the threshold must trigger. The same exact boundary survives volumes0,0,0,0,epsilon. The independent reference uses centered rational samples, explicit extrema scans and ordered-binary64 square-root bisection; production uses integer moments and lazy queues.


## Function to Candles: independent candle RSI transforms

Close, Open, High and Low each publish their own RSI. Selected prices replace Close and follow the existing custom-range rule for High/Low; Open remains the original candle open. Round each signed change and each gain/loss smoothing stage with an extended upper exponent, then round the complete 100*gain/(gain+loss) quotient. A zero total publishes 100. The four supported averages retain their RSI startup conventions; unchanged prices carry the prior recursive ratio only when neither mean is externally supplied. Actual component overrides determine the ratio even on flat prices.

Native histories for SMA/WMA allocate only observed bars. Fast evaluation requests two callbacks (gains then losses) for the requested candle output; exhausted slots use the extended default means. The supported batch route consumes no callback. Batch trading signals compare changes in the once-rounded arithmetic mean of the four published RSI values. The independent rational reference transforms each candle field separately. At length2, candles (O,H,L,C)=(2,5,-2,1),(1,8,-4,4),(3,7,-3,3) produce Close100,100,60; Open100,0,80; High100,100,60; Low100,0,50 under Wilder smoothing.


## Grand Trend Forecasting: lagged recurrence and complete forecast-error bands

Normalize trend lag N and forecast horizon H to at least one. Retain the binary64 .9 and .1 coefficients. Before a lag is available, previous trend and previous change equal the current selected price; prior forecast-horizon trend and forecast equal zero. Round change=.9*trend[N bars ago], then round the complete trend=2*change+.1*price-change[N bars ago]. Gtf is the once-rounded partial rolling mean of trend over N bars. Round forecast=2*trend-trend[H bars ago] and abs(price-forecast[H bars ago]); the latter's once-rounded partial H-bar mean determines the band width. Round each complete forecast +/- multiplier*meanError expression. Rounded internal stages retain an extended upper exponent; only publication may overflow.

The multiplier must be finite and nonnegative, including for empty batch and fast input, matching public option construction. Zero multiplier makes both bands equal MiddleBand even when retained error means exceed binary64. Compare exact price-minus-max/min margins across forecast, current trend and Gtf for trading signals. Lag histories allocate only observed data and compact expired prefixes without losing active lags. No route requests component averages; selected prices are the full numerical input.

At price2 and N=H=1, multiplier2, Gtf=1.8, MiddleBand=3.6, UpperBand=7.6. LowerBand is exactly the rounded binary64 expression3.6-4, not the separately rounded decimal literal-.4. With prices10,20,30,40, N=H=2 and multiplier1, decimal hand values are Gtf9,13.5,14.1,14.3; MiddleBand18,36,11.4,18.8; UpperBand28,51,27.4,26.8; LowerBand8,21,-4.6,10.8. The rational reference stores the full staged trajectory and rebuilds each averaging window; production uses integer units, rolling sums and indexed histories.


## Grover Llorens Activator and Cycle: shared ATR trail, distinct outputs

Round the complete true range using the prior selected price, with the first selected price standing in for itself. Smooth ATR with the selected mean and period. Seed the trail with the first price. Activator alone replaces an exactly zero prior trail with the prior selected price; Cycle retains a zero trail. Compute the exact sign of price minus prior trail, then round the complete priorTrail-sign*ATR*mult expression. Activator publishes this trail and compares the pre-update difference against its previous difference for signals. Finite negative and zero multipliers remain defined; nonfinite multipliers are rejected consistently.

Cycle rounds price-trail, smooths that oscillator over SmoothLength, then applies RSI over the same period. Round complete signed changes and gain/loss averages, then round100*gain/(gain+loss), using100 for a zero total. EMA/Wilder flat-input ratios carry only when neither gain nor loss mean is actually overridden. Signals compare exact changes in the published RSI, with the public80/20 thresholds. Internal stages preserve binary64 precision with an extended upper exponent, so an overflowing ATR/trail/oscillator does not poison the bounded RSI.

Fast Activator requests one ATR callback. Fast Cycle requests ATR, oscillator mean, gain mean and loss mean, in that order and with their respective periods. Exhausted callback slots preserve the extended default stage; actual gain/loss overrides determine ratios even on a flat smoothed oscillator. Supported batch paths consume no callbacks. Four supported means use lazy histories; native input rejection precedes state changes and previews/reset preserve every stage. Selected prices follow the existing custom-range rule. The generated public Cycle wrapper fixes smoothing20 and multiplier10; direct batch/native/fast methods still support their explicit parameters. No new wrapper parameters are introduced. The distinct high-pass/DEMA core remains unchanged pending the separate scope decision.

At ATR period1 and multiplier1, prices2,4,8 with high/low one unit away give Activator2,-1,-6. For Cycle with prices2,4,-2,3, the same ranges and SmoothLength2, the oscillator0,5,-8,-9 becomes the Wilder mean0,2.5,-2.75,-5.875 and RSI100,100,250/13,500/51. Flat candles2,4,3 distinguish the zero-trail rule: Activator2,0,5, while Cycle at period1 remains100. A supplied smoothed oscillator0,2epsilon,0,0 with default length2 Wilder gain/loss means yields100,100,0,0; the final plateau must not reset to100 as both rounded means decay to zero.


### Hawkeye Volume: complete previous-bar threshold quotient

`Up` and `Dn` use the previous selected midpoint plus/minus the previous candle range divided by the signed divisor. Default midpoints are correctly rounded high/low means; selected prices replace that input, retaining the shared causal custom-range rule. The complete offset-plus-midpoint fraction is rounded only at publication, so an overflowing offset may cancel to a finite level. Divisor zero collapses both levels to the previous midpoint; finite negative divisors reverse the offsets. Nonfinite divisors are rejected even for empty input.

The existing bullish/bearish condition order and bullish precedence remain. Partial rolling range and volume means are compared by exact cross products; the small-range comparison is strictly range < mean / 1.5. Lengths normalize to at least one and histories allocate only observed bars. Native previews do not commit history, and candle validation occurs before arithmetic. No component moving-average callback is consumed.


### Hirashima-Sugita RS: residual regressions and complete bands

Supported SMA, WMA, EMA and Wilder routes retain rounded extended residual, mean and partial-window OLS stages. The first residual is price minus EMA; the second is the complete price minus EMA minus first fit expression. The center is the complete EMA plus first fit plus second fit minus previous second fit expression. Each of the five bands is rounded independently from that center plus the selected mean of absolute first residuals times its integer offset. Signal margins compare exact price-minus-center differences.

Histories grow only with observed samples. Batch calculations consume no component overrides; each fast output requests EMA first and residual-width mean second. Supplied widths retain their sign; exhausted slots preserve extended defaults. Other moving-average kinds retain their mean and regression paths; their finite bands also use complete expressions to remove sequential-versus-multiplied rounding differences across routes. The independent OLS reference uses prefix moments in global bar coordinates, separate from the production rolling local-coordinate fit.


### Inertia: paired directional volatility and partial regression

Public Inertia and InertiaIndicator average high- and low-price RVI, each using a ten-bar population deviation and Wilder smoothing at the requested RVI period. The resulting rounded average feeds a partial-window least-squares endpoint by default. SMA, WMA, EMA and Wilder output options retain their established startup rules. Supported output histories allocate only observed samples; periods normalize to at least one. Signal comparisons use exact consecutive differences of published endpoints. The independent reference derives regression from global prefix moments, separately from the rolling production recurrence.

Batch consumes four RVI component overrides when supplied; the fast route also requests an output average as the fifth slot. Exhausted output slots use the selected default smoother. Native candle validation precedes state changes; preview and reset preserve both RVI histories and output history. The internal close-only core remains a separate pending alignment decision.


### JMA: rounded public approximation with extended recursive stages

This is the library's public JMA approximation, not a claim to reproduce a proprietary algorithm. The zero-initialized level, residual and correction stages use beta = 0.45*(length-1)/(0.45*(length-1)+2), alpha = beta^power, and a phase ratio clamped to0.5..2.5. Each complete stage and accumulated output is rounded at binary64 precision with an extended upper exponent. A zero alpha is evaluated as the exact identity transfer function, avoiding cancellation when a tiny price follows a huge one. Exact price-minus-retained-output margins determine signals.

Periods normalize to at least one; constant-sized state supports extreme periods. Phase and power must be finite and power must produce a finite alpha. Finite negative powers remain supported when that pole is finite; unstable outputs may exceed the publication range. Batch, fast and native routes consume no component-average overrides. Native validation precedes preview/commit arithmetic. The approved core alignment uses the same power-2 stages, retaining its length/phase arguments. Moving-average registry/helper bindings explicitly select the public phase50 default; the explicit core phase argument remains available. Output bounds are checked before writes and exact in-place replay is supported.


## Insync Index: retained component votes

The public index is 50 plus eleven votes in {-5,0,5}: CCI, Bollinger percent B, RSI, money flow, smoothed stochastic K and D, EMV direction, ROC direction, MACD direction, and two delayed DPO votes. Unpublished components retain binary64 precision with an extended upper exponent, so an overflowing component can still produce the correct bounded vote. Threshold comparisons are exact comparisons of the rounded components; the former relative 1e-12 tie tolerance is removed.

Direction compares a component with its partial rolling mean, then checks the mean's sign. The ordinary direction gives +5 at positive equality and no negative vote at negative equality; the inverse DPO direction gives -5 at negative equality and no positive vote at positive equality. Both DPO votes enter the index smaLength bars after calculation. Band threshold equality gives zero. The index is not clamped: eleven votes permit the mathematical interval [-5,105].

Price SMA, stochastic K/D and population deviation retain full-window startup; direction means and MFI use available observations. CCI uses the current-window mean absolute deviation and constant .015. MACD uses partial-mean EMA startup. RSI retains the existing Wilder flat-price carry convention. DPO uses the established lag clamp [2,530]. Histories allocate as observations arrive, including when a configured period is int.MaxValue.

A selected input replaces the price and the CCI/MFI typical input. Stochastic uses the established derived-range rule for selected values outside the original candle. EMV continues to use original high, low and volume. maType, signalLength and emoLength do not affect the index: the latter two describe component signal lines that the composite does not consume. The separate internal core formula is outside this approved public correction and remains tracked for alignment.


## JMA RSX clone: rounded cascade

This is the repository's RSX clone, not a claim of equivalence to proprietary Jurik RSX. With g=3/(max(1,length)+2) and q=1-g, each pair computes a=round(q*aPrevious+g*x), b=round(g*a+q*bPrevious), then y=round(1.5*a-.5*b). Three pairs process changes in rounded100*price; three more process their absolute values. Every complete stage rounds once at binary64 precision with an extended upper exponent. Keeping the scaled prices avoids overflowing before the bounded ratio is formed and preserves the existing small-value scaling convention.

The original f88/f90 startup logic is exactly five neutral observations: the stored counter goes1 through6 and then remains6; the threshold is5 after its first observation. Its temporary flat-price reset occurs only during warmup and is not stored. After warmup, a nonpositive absolute-chain result returns50; otherwise the complete ratio50*(signed+absolute)/absolute is rounded and clamped to[0,100]. The twelve recursive values and startup state use constant-size history. All public routes and the internal core use this same formula; the core preserves its output-span guard and exact in-place behavior.


## JRC Fractal Dimension: retained range ratio

Normalize both range parameters and the smoothing period to at least one. The range-aggregation period is clamp((length2-1)*length1,2,530), and the long range period is clamp(length2*length1,2,530), with products formed before clamping in wide integer arithmetic. Each range includes its period-lagged input price, using zero before that lag exists. Available short ranges sum exactly and divide by the complete aggregation period, preserving startup zero padding.

For length2>1 and positive ranges, raw dimension is2-log(bigRange*aggregation/smallRangeSum)/log(length2). Ranges and their ratio remain exact until the logarithm: near unity, a rounded relative change corrects the platform-log argument; elsewhere a normalized rounded mantissa and integer exponent prevent ratio overflow/underflow. Complete logarithm corrections and the final dimension round once. Equal ratios give exact zero logs. A zero range/sum gives dimension2; length2=1 gives zero. The configured average then smooths the dimension and smooths that result again for Signal. Trading signals compare the two published means with reversed direction.

Selected inputs use the existing derived candle-range rule consistently in batch, fast and native/live routes. Typed-fast component overrides receive the raw dimension first and the first smoothed line second, both with the normalized smoothing period. The batch path ignores those overrides. Legacy average kinds keep their existing component implementations. Common convex averages and range histories grow only with observed data rather than allocating configured extreme periods eagerly.


## Kase Convergence Divergence: retained peak and signal stages

This repository variant uses d=((high-laggedLow)-(laggedHigh-low))*sqrt(length1)/ATR, with Wilder ATR(length1), zero lagged extremes before the lookback exists, and zero d for zero ATR. It multiplies by sqrt(length1); it is not presented as the conventional random-walk index with sqrt(length1) in the denominator. The peak is the weighted mean of d over length2; convergence is peak minus its selected mean over length3. All periods normalize to at least one. Weighted and simple means retain their complete denominators and zero-padded startup conventions.

True range, Wilder ATR, the complete signed drive, weighted peak, signal mean and residual each round once at binary64 precision with an extended upper exponent. Exact intermediate sums and differences prevent overflow before cancellation. Histories allocate as observations arrive, and previews do not change them. Selected input derives high/low ranges consistently and supplies the previous price for true range. Fast custom-average slots receive true range, signed drive and peak, in that order, using length1, length2 and length3. Legacy mean types retain their public array implementation; native availability follows the existing smoother factory.


## Kase Indicator: complete volume-scaled ratios and held values

KaseUp is previousHigh/(currentLow*averageVolume*sqrt(length)); KaseDn is currentHigh/(previousLow*averageVolume*sqrt(length)). ATR is a positive-value gate, not part of the divisor. Both volume and true range use the selected moving-average kind and normalized length. A side carries its own previous result when ATR is nonpositive, average volume is zero, or that side's price divisor is zero. Signed prices and volumes remain valid; first-bar previous high/low are zero.

True ranges and moving-average stages retain extended binary64 values. Each complete ratio rounds once, retaining its rounded value beyond the public exponent range for later carries and signal comparisons. This prevents overflow in the price quotient or volume-times-square-root product, and prevents a prematurely underflowed intermediate quotient from becoming zero. Signals compare the exact difference of the two retained readings. Selected inputs derive high/low ranges consistently and supply the prior true-range price. Fast custom-average slots receive original volume and derived true range in that order. Histories grow lazily; legacy mean types retain their array fallback.


## Kase Peak V2: genuine-return deviation and partial pressure means

The deviation window contains only genuine close-to-close observations. Zero or opposite-sign ratios contribute a zero log; same-sign negative prices remain defined. No deviation is emitted until length1 genuine returns exist. Population deviation is the correctly rounded square root of exact centered moments, without first rounding the variance. The selected length2 mean includes the opening zero deviations. SMA/WMA use exact finite-window sums and round the complete mean once. EMA/Wilder retain their existing compensated recurrence state; independent rational recurrences verify their published means through the complete indicator.

For available lags in [fastLength,slowLength), each pressure selects the maximum positive same-sign logarithmic range divided by sqrt(lag), then divides by average deviation (zero divisor yields zero). Each pressure is averaged over the available smoothLength observations, including startup zeros. The complete sensitivity*(upMean-downMean) expression rounds once; retained values support signal comparisons after public overflow. Used periods normalize to at least one; unavailable lags are skipped, and an empty lag interval yields zero. Histories allocate lazily. Nonfinite sensitivity is rejected; finite signed sensitivity is valid. length3 and devFactor affect no published series and remain ignored.

Logarithms use the existing StableLogRatio contract: correctly rounded near-unity log, compensated platform log for moderate ratios, and normalized mantissa/exponent evaluation for extreme ratios. An independent array reference specifies these rounding stages; separate rational-log checks corroborate their mathematical accuracy. Selected prices derive high/low ranges consistently. The fast callback receives the startup-suppressed deviation series and normalized length2. Legacy volatility means retain SpreadAverage behavior. This formula uses sqrt(lag) in the denominator and is separate from the pending Kase Peak V1/Convergence formula investigation.


## Kaufman regression: common adaptive weights and centered moments

The default Kaufman Adaptive Correlation Oscillator and Kaufman Adaptive Least Squares Moving Average use the same price-adaptive weight for all price/time moments. Efficiency is the absolute length-bar displacement divided by the exact sum of length absolute price changes, zero for zero travel. Warmup replaces the observation with unit weight; afterward the binary64 weight is pow(2/31 + efficiency*(2/3 - 2/31), 2). Correlation regresses price against time, not price against its moving average. IndexSt and SrcSt are weighted population deviations. The fitted value evaluates that weighted line at the current bar.

Centered moments retain normalized two-component mantissas with extended exponents at both ends. In particular, squared subnormal prices survive until their square root or correlation quotient. The fitted value uses the algebraically cancelled prediction-error expression: price + (1-gain)*(dx*previousCovariance - dy*previousTimeVariance)/(previousTimeVariance + gain*dx*dx). This avoids cancellation between a large fitted intercept and slope contribution when the correct current value is tiny. Signal comparisons retain the unpublished fitted value. Exact integer price differences prove affine trajectories, whose fitted residual is exactly zero and whose correlation is the slope sign; no tolerance converts small changes into ties. Histories allocate only observed data, and native invalid-input checks precede updates.

The independent reference accumulates exact rational raw powers of absolute time and price, then forms their centered moments. Constant-price least-squares output follows the exact zero-covariance identity directly. Fast custom-average callbacks retain five correlation slots and seven least-squares slots; legacy mean routes keep their existing component formulas. The separately named internal cores still have different formulas; their alignment is a separate pending scope decision.


### Kwan delayed cumulative ratio (batch 658)

The library's existing Kwan variant integrates a delayed value ratio, rather than applying a rolling average to it. For period n and delay d, let H/L be the extrema of the current n-bar candle window, r the selected-price RSI, p the current selected price and q its n-bar predecessor. The contribution is `(p-L)*r*q/((H-L)*p)`, equivalent to stochastic times RSI divided by percentage momentum after cancelling both factors of 100. Missing predecessor, exactly zero current/predecessor price or exactly zero range contributes zero. Signed prices are permitted; nonzero denominators are never replaced by a tolerance. Periods resolve to at least one.

RSI retains the existing exact finite change, rounded strength-average stages and recursive flat-price carry conventions. The complete contribution rounds once to binary64 precision with extended upper exponent; delayed contributions accumulate exactly, and the accumulated total is divided by d only when published. An overflowing published total can recover after later cancellation. Signals compare the signed delayed contributions and their exact changes (the common positive divisor d cancels), avoiding subtraction of overflowing or rounded cumulative outputs. This documents the library variant; it does not establish attribution to an authenticated original Kwan publication.

The independent reference forms a rational stochastic and a separate rational momentum, divides them, and sums delayed contributions. SMA/WMA queues and monotone extrema store only observed history. Batch/fast routes use the same selected-price-derived candle ranges; native streaming is explicitly bound. Existing custom gain/loss component slots and nonstandard average paths remain supported. Tests cover independent hand values, all four ordinary mean kinds, signed/subnormal/extreme prices, overflowing totals and finite recovery, both period extremes, flat and zero ranges, callbacks, guards, previews/reset and selected sources.


### LBR Paint Bars retained ATR bands (batch 659)

LBR Paint Bars retains the library's crossing-band convention: `UpperBand = highest(high, lookback) - multiplier*ATR`, `LowerBand = lowest(low, lookback) + multiplier*ATR`, and primary output `Aatr = multiplier*ATR`. Aatr is a width, not a center; the named bands may cross. Both periods resolve to at least one. Every finite signed multiplier is supported; nonfinite multipliers are rejected before state changes.

True range is the exact maximum of high-minus-low and absolute gaps from the preceding selected price (current price on the first bar), rounded once with an extended upper exponent. ATR retains existing SMA/WMA/EMA/Wilder startup and smoothing conventions with unpublished extended values. The final multiplier product and band subtraction/addition are evaluated completely before binary64 publication, so an overflowing Aatr may coexist with two finite bands. Subnormal half-ulp products likewise survive until each complete band rounds.

Signals compare the selected price against the exact maximum/minimum of the two retained bands and compare those margins with their preceding values. No tolerance collapses adjacent representable prices into ties. Batch and fast paths share selected-price-derived ranges; the single custom ATR-average slot is preserved, and native streaming is explicitly bound. Standard histories allocate only observed data. Legacy average implementations remain selected by their existing kind.

The independent reference scans each candle window, computes rational true ranges and smoothing stages, then forms rational width/bands/margins. Tests include crossed bands, retained three-bar extrema, nonzero SMA startup, signed multipliers, overflowing ATR/width with finite bands, subnormal ties, adjacent-price signals, selected inputs, custom/legacy means, invalid input, preview/reset and extreme periods. This is an explicit library formula contract, not a claim of authenticated original-author provenance.


### Batch 661: MacZ complete standardization and unpublished smoothing

Plain MacZ is `mult * (price - Wilder(length) + MA(fast) - MA(slow)) / populationDeviation(length)`. The deviation requires a full window; a zero deviation yields zero. Wilder starts at zero. SMA uses a full window, WMA keeps its fixed triangular divisor, and EMA uses its growing startup mean. Periods clamp to at least one. Finite signed multipliers are supported; `gamma` remains the existing unused plain-MacZ parameter. The separate MacZ VWAP formula is unchanged.

Finite-window means retain exact rational divisors. Exact square-root cases preserve their rational quotient, including the half-epsilon histogram tie; recursive states and irrational roots use 106 significant bits with extended exponents. Signal and histogram retain unpublished values until their public projections. A period-one mean is exactly the identity. Legacy average kinds retain their existing component calculations. Public signals compare the unpublished histogram and its previous value, preserving distinctions lost by binary64 projection. Native input rejection precedes state changes; streaming factory routing, custom fast component slots, selected inputs, preview/reset and observed-history allocation are covered.


### Batch 662: MacZ VWAP complete products and Laguerre stages

MacZ VWAP adds each observation's deviation from its own rolling VWAP, normalized by the full-window RMS of those residuals, to (fast MA - slow MA) divided by full-window population price deviation. A zero volume sum gives center zero; zero price deviation omits only the MACD term. Signed finite volumes remain supported. Unlike plain MacZ, flat positive prices with zero volumes can have a nonzero output. Four Laguerre stages start at the first raw observation, followed by weights [1,2,2,1]/6. Gamma zero is that finite filter; gamma one retains its initial value. Nonfinite gamma and price/volume inputs are rejected before state changes.

Exact finite-window products, volume quotients and residual energies survive both binary64 exponent boundaries. Irrational root quotients and recursive means use 106 significant bits; the equivalent symmetric numerator followed by four recursive poles uses 160-bit states. Alternating observations cancel before recursion, preserving the sign of a decaying tail. Constant initial values seed each pole at equilibrium. Complete outputs form before state quantization. Gamma zero and one keep exact rational stages. Signals compare unpublished histograms. Legacy means retain their component behavior.


### Batch 663: Market Direction exact crossing differences

Market Direction forms crossing = (fast * sum(slow-1) - slow * sum(fast-1)) / (slow-fast), then MDI = 200 * (previous crossing - crossing) / (price + previous price). Initial price and crossing are zero. Equal periods or a zero price sum yield zero; a zero-length subwindow is empty. Periods clamp to at least one. Typed options continue to expose the fast period as Length and fix the slow period at 55; batch, core and native constructors retain both arguments.

Production keeps finite-window sums and crossing numerators on an exact binary integer grid, retains the common crossing divisor until the final quotient, and projects once to binary64. Signals compare complete rational outputs. Native invalid-bar rejection precedes state changes. Core span length and all input values are checked before writes; exact in-place operation preserves the previous input. Histories allocate only as observations arrive.


### Batch 664: Mass Thrust nested price-volume ratios

For each selected-price change, advances and declines are its positive and negative magnitudes; both are zero on the first bar. Let A and D be their rolling sums over Length. An advancing bar contributes volume/A to the up-volume history; a declining bar contributes volume/D to the down-volume history; other contributions are zero. Let U and V be the rolling sums of those respective histories. Mass Thrust Indicator publishes (A*U - D*V)/1,000,000. Mass Thrust Oscillator publishes 100*(A*U - D*V)/(A*U + D*V), with exactly zero denominator mapped to zero. MassThrust is an alias of MassThrustIndicator.

Finite signed prices and volumes are retained. With signed volumes the oscillator need not lie in [-100,100]; a nonzero subnormal denominator can produce a mathematically overflowing output. Nested finite-window ratios and products remain rational until binary64 publication, allowing subsequent windows to recover. Length resolves to at least one and histories allocate only as observations arrive.

Signal is the requested moving average of the unpublished primary series. SMA and WMA retain rational windows; EMA and Wilder retain 106-bit recursive states with extended exponents. Other moving-average kinds retain their existing smoother behavior. Indicator trade signals compare the retained signal line with its previous value; oscillator trade signals compare retained line-minus-signal margins and the existing +/-50 crossings. Exact comparisons are not replaced by tolerances.

Both internal cores now follow the public price-volume formula. The oscillator has a volume-aware overload; its existing price-only overload explicitly uses unit volume. This replaces the cores' previous signed-volume-average and smoothed-direction formulas. Core input/span validation precedes output writes; native whole-bar validation precedes state changes. The independent reference scans windows directly with rational arithmetic and retains approximately 212 bits in recursive smoothing memory via four separately rounded binary64 residuals. This records the library's formula contract, not authenticated original-author provenance or completed verification evidence.

The shared rational helper used by Mass Thrust, MacZ and MacZ VWAP cancels common factors before multiplication and division and uses a least-common denominator for addition. Addition still fully reduces the result after binary exponent alignment; negation preserves the normalized representation. These operations introduce no rounding or change to the existing recursive precision limits.


### Batch 667: Modified Gann Hilo Activator

The candle body is extended toward the trailing partial-window high and low by the finite multiplier, then both extensions are smoothed with the chosen period. The direction becomes one when close exceeds the upper mean, zero when it exceeds the lower mean, and otherwise retains its previous value. Direction one publishes the lower mean; zero publishes the upper mean. This retains the existing public switching convention, including strict ties. Signals compare consecutive close-minus-line margins. The obsolete lookbackLength option still has no effect.

The numerical implementation retains extensions, SMA/WMA means, EMA/Wilder recursive values, comparisons and signal margins beyond binary64 exponent limits until publication. Recursive production memory uses the existing 106-bit shared average; the independent reference uses rational trajectories. Selecting a band avoids multiplying an unselected infinite projection by zero. Finite signed prices and finite multipliers, including zero and negative multipliers, remain supported. Periods clamp to at least one; observed extrema history grows lazily. The four standard averages retain unpublished arithmetic; other average kinds retain their existing fallback. Fast component callbacks receive the two published extension series in upper/lower order, and short custom outputs are zero-filled. There is no separate internal Modified Gann core formula to align.


### Batch 668: Morphed Sine Wave

The formula is price plus sin(2*pi*index/length) divided by power, starting at index zero. Periods clamp to at least one. Power must be finite and nonzero; negative power reverses the perturbation and remains supported. Zero power is undefined and is rejected consistently. The batch, native, core and selected-input fast routes retain addition/division and signal differences until publication, avoiding the unnecessary price-times-power intermediate. Signals compare consecutive unpublished output differences.

Phase advances only on final bars and wraps by the period, avoiding counter overflow. The existing sine-phase helper reduces the integer phase exactly, preserves exact zeros and quarter-cycle extrema, and uses Math.Sin on the rounded reduced angle elsewhere. The reference evaluates absolute-index phase with independent rational angle and price arithmetic; cardinal hand vectors separately constrain phase signs, zeros and period. This is not a claim of arbitrary-precision transcendental evaluation. The core validates inputs before writing, supports exact in-place use, and preserves the unused output tail.

### Batch 669: Moving Average Adaptive Filter

Efficiency is the absolute price change over Length bars divided by the sum of the intervening absolute one-bar changes; it is zero before the full lag or when travel is zero. Gain is (SlowAlpha + (FastAlpha - SlowAlpha)*efficiency)^2. The adaptive mean starts at the first price and advances by gain*(price-previousMean). Maaf is Filter times the full-window population standard deviation of those increments, with zero deviation before the window fills.

Price differences, efficiency, current increments and finite-window variance remain rational beyond binary64 exponent limits. Recursive adaptive-mean memory retains whichever is smaller in magnitude, the mean or its residual relative to the current price, using the shared 106-bit rounding; the extended square root is multiplied by Filter before publication. The independent reference rescans windows and centered deviations, scales variance before its square root, and retains four binary64 residual components in recursive memory. Finite Filter must be nonnegative, and both finite alpha parameters must lie in [0,1]. Length clamps to one and queues grow only with observed history.

Signals retain the existing public seeded, rounded EMA component. Price-minus-EMA slopes and their changes are compared in extended arithmetic. The volatility gate compares exact variances for positive Filter; zero Filter makes every volatility value tied. Existing batch, bound fast fallback, native and streaming routes share the formula. There is no dedicated typed fast arm, internal core, or component-average callback slot for this indicator. This describes the formula contract; verification is recorded separately.

### Batch 670: Moving Average Adaptive Q public routes

The public recurrence starts at the first selected price. Efficiency is the absolute change over Length bars divided by the sum of the intervening absolute one-bar moves, zero before the full lag or on zero travel. Gain is (FastAlpha*efficiency + SlowAlpha)^2, deliberately different from KAMA's interpolation between slow and fast. The next mean is previousMean + gain*(price-previousMean). Trade signals compare consecutive unpublished price-minus-mean margins.

Direct batch/fast/native alpha arguments may be any finite real values; they are not clamped to a stable or convex gain. The builder options retain their existing length-only surface and default alphas. A finite gain may produce a mathematically overflowing published mean; extended memory allows later unit gain to recover. Periods clamp to one and histories grow only with observations. The recurrence retains the smaller-magnitude mean or price-relative residual at 106 bits; the independent rational reference rescans travel and uses four binary64 residual components for memory. Hand values for prices [0,1,2,3], Length=2, FastAlpha=1/2, SlowAlpha=1/4 are [0,1/16,295/256,8977/4096].

This batch covers public batch, fast, compute-arm, native and streaming routes. The internal core and moving-average registry still use their existing separate full-input-volatility formula; their proposed causal alignment remains a pending scope decision. This records the public formula contract, not authenticated original-author provenance; verification is recorded separately.


### Batch 671: Multi-Depth public pole filters

The public one-, two-, and three-pole filters apply each pole filter to price (alpha), then to price minus alpha (beta), and publish alpha plus beta divided by depth. Md2Pole remains primary. Missing alpha lags equal the current price; missing beta lags are zero. Periods clamp to one, and history uses fixed-size storage. Selected input is respected on the fast route.

Coefficient primitives use binary64 exp, sinh, and half-angle sine. The radius is defined by the stable gap `2*exp(-angle/2)*sinh(angle/2)` and exact subtraction from one. Cosine uses exact algebra around the binary64 half-angle sine. Third-pole c is the exact square of its complex radius. Subsequent coefficient algebra is exact, preserving unity gain and nonzero feed gains at extreme periods; this is not arbitrary-precision transcendental evaluation. One-pole gain is the exact ratio 2/(length+1).

Current arithmetic and signal margins remain extended through publication. Fixed-coefficient recursive state is exact: independently rounding alpha and beta destroys the one-pole impulse zero at length+1 bars. Alpha retains either its mean or its residual from current price, whichever is smaller. Binary64 inputs and primitives are compacted losslessly; the independent rational reference evaluates the uncentered recurrence without intermediate rounding. The number of history slots is fixed, while integer precision grows with stream duration. The reference stores uncentered alpha and beta numerators over known powers of a common coefficient denominator, avoiding repeated fraction reductions without changing exact values. Publication may overflow but finite-input state can recover. Public signals compare successive price-minus-two-pole margins. The separate legacy core and moving-average registry are unchanged pending the existing alignment decision.

All 40 focused regressions passed on net10.0. The 39 prepared behavioral faults still await isolated mutation qualification.


### Batch 672: Negative Volume Disparity

Price and NVI each use their own population deviation and selected moving average. Their normalized coordinate is `1.5 + (current - mean)/(4 * deviation)`; zero deviation gives coordinate 1. Nvdi divides the price coordinate by the NVI coordinate, returning zero for an exactly zero denominator. Rationalizing the negative branch using an exact discriminant and dimensionless root ratio preserves exact zero versus arbitrarily small nonzero values. NVI retains the public signed-price return `(current - previous)/abs(previous)` and extended binary64 compounding. Window moments remain exact; roots and recursive means retain extended precision.

All public routes resolve selected prices, clamp periods to at least one, allocate observed history lazily, and reject nonfinite thresholds. Finite reversed thresholds retain ordered trading-state branches. Fast callbacks retain price mean, NVI mean, then signal mean order; the primary output requests only the first two. There is no separate internal core. All 84 focused regressions passed on net10.0. The 21 prepared behavioral faults await isolated mutation qualification.


### Batch 673: On Balance Volume Disparity

OBV adds or subtracts signed volume according to the selected price's direction; the first price compares with zero, and equal prices leave the total unchanged. The cumulative total and finite-window moments remain exact until output publication. Price and OBV each use their own selected moving average and population deviation. Their normalized coordinate is `1.5 + (current - mean)/(4 * deviation)`, with coordinate 1 at zero deviation. An exactly zero OBV coordinate gives disparity zero. The negative coordinate uses an exact discriminant and a dimensionless root ratio, preserving exact ties and near-zero nonzero values.

The existing builder fallback, batch, native, and streaming routes share these semantics. There is no dedicated compute arm or separate core; the existing no-component-callback contract is preserved. Periods clamp to at least one, history grows as observed, and nonfinite thresholds are rejected. Finite reversed thresholds retain their existing ordered signal branches. Independent reference roots use Newton refinement and four binary64 residuals; the discriminant and window moments remain exact. All 75 focused regressions passed on net10.0. The 25 prepared behavioral faults await isolated mutation qualification.


### Batch 674: On Balance Volume Reflex

The selected price is compared directly with the price `length` bars earlier, or zero until that history exists. The indicator accumulates signed volume in the resulting direction and leaves its total unchanged on equal prices. The signal averages the extended total before publication, allowing both total and signal to recover after a published overflow. Trading signals compare the current total-minus-signal margin with the previous margin.

All five routes use the same lagged direction, exact cumulative arithmetic, and lazy history, with periods clamped to at least one. Native previews do not commit history or totals, and invalid candles are rejected before state changes. The fast primary output requests no moving-average callback; the signal output requests exactly one callback with the cumulative series and signal period. There is no separate reflex core: ordinary OBV uses a different lookback and remains separate. All 83 focused regressions passed on net10.0. The 24 prepared behavioral faults await isolated mutation qualification.


### Batch 675: Oscar Indicator

Oscar takes the rolling highest high and lowest low, forms the exact selected-price position in that range, clamps it to [0,100], and updates `previous/6 + position/3` from zero. A zero range contributes zero. Its existing recurrence has steady-state gain 0.4 and values in [0,40]; this batch preserves those coefficients. Inverted candle bounds retain the existing signed-range arithmetic. Exact rational recursive state preserves changes that publication can round away; trading signals compare the exact slope and slope change.

Batch, fast, typed arm, core, native, and streaming routes use this formula. Periods clamp to one, extrema storage grows with observed history, previews do not commit, and selected prices keep original high/low fields. Core span lengths and finite values are checked before output writes. The independent reference computes a weighted sum with integer powers of six. All 39 focused regressions passed on net10.0. The 24 prepared behavioral faults await isolated mutation qualification.


### Batch 676: Peak Valley Estimation

The selected price minus its selected moving average forms the signed residual. A partial-window ordinary least-squares endpoint fits the absolute residual over `smoothLength`; its trailing maximum uses at least two bars, preserving the public window convention. A zero maximum yields ratio zero; negative maxima remain signed. Sign1 marks a new exact maximum ratio of one, Sign2 marks a ratio strictly below four-fifths, and Sign3 marks departure below one after an exact maximum. Each active event publishes the negative sign of the price residual.

For SMA, WMA, EMA, and Wilder smoothing, all five routes retain exact rational means, residuals, regression endpoints, and ratios until the event comparisons. Other averaging kinds continue through their existing moving-average implementations. Lazy histories handle extreme periods and previews do not commit. The existing SMA core publishes the same Sign1 event, including negative maxima. Fast outputs each request one moving-average callback with the selected prices and period. Independent references solve centered OLS normal equations and compare exact events. All 84 focused regressions passed on net10.0. The 30 prepared behavioral faults await isolated mutation qualification.


### Batch 678: Phase Change Index

The existing gradient formula uses `length >= 2`, an anchor `length` bars back, and zero change until that history exists. For lag 1 through length, the residual is `laggedPrice - anchor - change*lag/(length-1)`. PCI is 100 times the positive residual sum divided by the absolute residual sum, with zero for an exactly zero denominator. Missing startup bars contribute zero. The implementation cancels the common denominator and evaluates residuals as exact integers. History grows only as observed, and previews do not commit.

SMA, WMA, EMA, and Wilder smoothing retain exact rational PCI values until signal publication; other kinds retain their existing moving-average implementations. Trading signals compare the exact signal slope and slope change. All five routes preserve selected prices and the existing formula; there is no separate core. Fast primary output requests no moving-average callback, while fast signal output requests one with raw published PCI and the smoothing period. Independent Fraction hands include a signal of 1825/23 whose value changes by one ULP if raw PCI is rounded before smoothing. All 80 focused regressions passed on net10.0. The 31 prepared behavioral faults await isolated mutation qualification.


### Batch 677: Periodic Channel exact direction

Rolling centered correlations now retain exact integer moments. Their accumulated sign groups rationally equivalent square roots (proved by gcd and integer square roots); distinct remaining square classes cannot sum to zero over rational coefficients. Nonzero sums use certified dyadic bounds with adaptive precision, without an epsilon or fixed precision cutoff. Lookup fingerprints only reject impossible matches; every accepted match is proved exactly. The sequence [0,0,1,2,1,0,0] cancels exactly; changing its final zero to either signed minimum subnormal selects the corresponding direction.

The established channel divides cumulative sums by bar index rather than count. Its sine sample uses Math.Sin; downstream sums, ratios, bands and signal margins remain exact until publication. Lengths clamp to one, history is lazy, and previews preserve committed history. Batch, fast fallback, native and streaming routes share the corrected formula; there is no separate core. The independent reference uses centered fractional moments and rational radical classes. All 64 focused regressions passed on net10.0. The 37 prepared behavioral faults await isolated mutation qualification.


### Batch 679: Parametric Kalman lagged-error weighting

The batch, fast, native streaming, core and moving-average registry use the public lagged-estimate error formula. The core no longer uses a separate covariance/noise filter. Length clamps to one; history grows only as bars arrive. Before the lag is available the baseline is the previous price. A zero sum of errors selects the current measurement. The estimate is an exact affine quotient rounded once to binary64; recursive error is rounded once to binary64 precision with an extended upper exponent. Differences, weights, and signal margin comparisons are exact before those boundaries. This preserves finite convex estimates for opposite-sign extremes without storing an overflowing error. Preview does not commit history or error. Both factories register the native state.

Regressions cover the independent rational recurrence, hand values, extreme values, lag, preview/reset, input selection, signals and core/span/registry behavior. All 39 focused regressions passed on net10.0. The 30 prepared behavioral faults await isolated mutation qualification.


### Batch 680: Polarized Fractal Efficiency and Pfe alias

The signed Euclidean direct/path distance formula is retained across batch, fast, native and EMA core routes. Exact squared differences precede correctly rounded binary64 distance norms, with one extra upper exponent bit for finite-price differences. Path sums and their ratio are exact until ratio publication. The triangle-inequality bound of 100 is enforced after independent norm rounding; a linear slope of five over three bars otherwise rounds to 100.00000000000001. Direction uses the exact ordering of finite prices, including subnormals. Raw efficiency and recursive mean stages round to binary64; finite-window sums and affine smoothing numerators are exact. Periods clamp to one and history is allocated only as bars arrive. Both streaming factories register full and alias specifications. Smoothing callbacks receive the raw efficiency and configured smoothing period, preserving selected input state.

All 111 focused regressions passed on net10.0. The 40 prepared behavioral faults await isolated mutation qualification.


### Batch 681: Pivot Detector public numerical components

The public formula is twice RSI minus 70 above the long moving average, otherwise minus 40. Price differences, finite-window and recursive component means, RSI ratios, price/mean branch decisions and signal differences remain exact until output publication. Zero loss gives RSI 100; the established EMA/Wilder unchanged-price carry rule remains. Periods clamp to one and queues grow only with observed history. Callback slots remain gain, loss and price mean, with respective RSI, RSI and long-average periods. The obsolete typed Length option remains inert, preserving the typed public defaults of 200 and 14. Both streaming factories register the public state.

The separate local-pivot core remains unchanged pending the scope decision. All 47 focused public-route regressions passed on net10.0. The 38 prepared behavioral faults await isolated mutation qualification.


### Batch 682: Premier Stochastic centered arithmetic

The full indicator and alias center the exact clamped range position before rounding, then apply two once-rounded averages and tanh(x/2). Finite sums and recursive affine numerators are exact at each binary64 stage boundary. The final transform resolves subnormal half-input midpoint ties toward smaller magnitude, since tanh(x)<x for positive x. Independent rational subtraction and Taylor bounds verify the 15-epsilon input yielding seven epsilon after transformation. The core now uses the public ceiling square-root smoothing period clamped to 2..530. Extrema allocate history lazily; both factories register full and alias specifications. Two smoothing callbacks retain their inputs and resolved period. Signal comparisons use exact differences of published outputs.

All 113 focused public-route regressions passed on net10.0. The 46 prepared behavioral faults await isolated mutation qualification. The shared reference tanh helper is unchanged; this reference has a local midpoint correction and the existing strict relative transform budget with same-sign enforcement.


### Batch 685: QQE volatility widths

QQE retains its published pair of scaled RSI-movement widths. The RSI component retains the established rounded gain/loss stages and unchanged-price carry rule. Subsequent signal smoothing, absolute movements and both width smoothing stages retain exact rational intermediates until scaling and publication; signal comparisons retain those intermediates as well. Lazy queues avoid period-sized allocation. Derived periods use 64-bit arithmetic (2*length-1), including 4,294,967,293 for the largest input period. Nonnegative finite factors follow the existing typed-option domain. Five callbacks retain gain, loss, RSI signal, first width and second width order; callback/legacy paths reject derived periods outside their int API instead of wrapping. Unsupported native smoothing retains batch fallback. All 82 focused tests passed on net10.0. The 40 prepared behavioral faults await isolated qualification.


### Batch 688: Rainbow public numerical cascade

Ten moving-average stages, their mean and extrema, and the complete rolling-range quotients retain exact intermediates until output publication. SMA full-window startup zeros and weighted startup weights remain defined. Lazy queues avoid period-sized storage. All three output names and ten callback stages remain explicit; both factories register the public state. Core alignment remains a separate pending decision. All 50 focused regressions passed on net10.0. The 35 prepared behavioral faults await isolated mutation qualification.


### Batch 689: Random Walk complete root-scaled ratios

The existing fixed-lag formula retains zero missing history and zero-ATR results. True ranges include opening gaps to the current selected close and later gaps to the previous selected close. Exact rational ATR stages and lagged differences remain unpublished until each complete ratio is rounded using the exact square root of the period. This corrects the length-two opening hand from the lower neighboring double to sqrt(2). Signals cancel the shared positive root factor before comparison. Lazy history supports extreme periods. Both factories register the state. Fast callbacks retain one true-range average; batch legacy ATR callback behavior remains. All 50 focused Random Walk regressions passed on net10.0. The 34 prepared behavioral faults await isolated mutation qualification.


### Batch 690: Recursive RSI exact delayed direction votes

Only the last iteration of the legacy inner loop survives: its midpoint is compared with the midpoint one period ago, producing a 100/0 nondecreasing vote. The published value averages the preceding window of votes, excluding the current vote. Exact lagged differences, component means, Wilder gain/loss ratios, midpoint comparisons and unpublished feedback prevent overflow and loss of tiny directions. Lazy history replaces period-sized buffers and the redundant inner loop. Signal threshold clauses add no behavior because each crossing already implies the corresponding exact slope sign. Fast callbacks retain source/gain/loss order; the legacy batch does not consume standard-average override slots. Both factories register the public state. All 54 focused regressions passed on net10.0. The 34 prepared behavioral faults await isolated mutation qualification.


### Relative Spread Strength: exact spread and bounded RSI component (batch 691)

Fast and slow averages, their difference, successive spread changes, and Wilder gain/loss moments now retain exact rational values. The bounded RSI is rounded to binary64 before the signal average, preserving the existing component boundary. A zero spread change carries the preceding RSI when its period exceeds one; zero loss otherwise yields 100. Signal averages retain exact arithmetic until publication. SMA/WMA windows allocate only observed history; EMA, Wilder, DEMA and TEMA preserve their startup definitions. Other average kinds retain the existing smoother fallback.

Standard batch consumes no component override slots. The fast callback path retains three slots (fast average, slow average, signal average) and computes the intervening spread and RSI without overflowing finite callback results. No independent core exists. Shared SpreadNumber/SpreadAverage implementations are unchanged.


### RSING: complete range-volume quotient and signal component (batch 694)

RSING multiplies length-lagged price change by volume/volume-average and candle-range/population-deviation-of-range. Exact range moments and rational volume averages now feed a single complete root quotient; neither candle subtraction nor the deviation denominator is rounded prematurely. Warmup, zero volume average, and zero range variance produce zero. Observed-history queues avoid period-sized allocation and length+1 overflow.

The raw oscillator remains a binary64 component before its signal average. If its exponent exceeds binary64, retain the same 53 significant bits with an extended upper exponent so a finite signal or cancellation remains computable; public outputs still follow the existing overflow-rejection contract. Signal means and slope/acceleration decisions retain exact arithmetic after that component boundary. Standard batch consumes no override slots; fast overrides retain volume-average then signal-average slots, with finite values required at callback boundaries. Generator metadata preserves both configurable averages. Native selected-price inputs preserve original candles. Other average kinds retain their existing smoother fallback.


### Sell Gravitation: exact candle-body ratio and two averages (batch 696)

The public formula divides selected-price minus original open by original high minus low, returning zero for zero range, then applies two configured averages. Exact rational body/range arithmetic and the first average avoid premature overflow. The first output remains a binary64 component before the second average; overflowing components retain 53 significant bits with an extended upper exponent, allowing finite later cancellation. The second average and spread-based signal decisions remain exact until publication. SMA/WMA queues allocate observed history; EMA/Wilder retain their startup definitions, and other kinds use the established smoother fallback.

Batch consumes no override slots; fast primary consumes one and fast Signal consumes two. Generator metadata preserves both average slots. Native routing now registers the state and preserves original candles when selected prices fall outside their range. The unrelated internal core remains unchanged pending its separate scope decision.


### Squeeze Momentum: exact residual regression (batch 697)

The public formula fits a partial-window least-squares endpoint to selected price minus the average of its configured moving average and rolling high/low midpoint. SMA retains full-window startup zeros; the regression uses observed points. Exact rational means, midpoint, residuals and regression moments prevent premature rounding and overflowing differences. Lazy monotonic deques and queues allocate only observed history. Signals compare exact endpoint sign and change. Standard averages use exact trajectories; other average kinds retain the established smoother fallback.

Original candle highs/lows remain unchanged for selected inputs, including direct-fast calls. Native state validates finite bars before committing any stage. Batch consumes no callback slots; fast consumes one price-average slot, and generator metadata retains that slot. No separate Squeeze Momentum core was found.


### Batch 687: Rahul Mohindar public numerical cascade

Ten SMA stages, their aggregate, the complete range quotient and subsequent EMA stages retain rational intermediates through output publication and signal comparisons. SMA startup zeros and EMA partial-mean seeds remain part of the formula. Lazy queues and monotone extrema handle large periods; range periods retain the public minimum of two. The four outputs stay distinct. Fast callback graphs preserve ten SMA slots and only the EMA slots required for the requested output. Both factories register the public state. Core alignment remains a separate pending decision. All 42 focused regressions passed on net10.0. The 43 prepared behavioral faults await isolated mutation qualification.


### Standard Deviation Volatility: exact residual squares and root component (batch 698)

This indicator smooths each price's squared residual from its own moving average, takes a square root, and smooths that deviation. It remains distinct from true population-window deviation. Exact mean/residual/square/variance arithmetic supports SMA, WMA, EMA and Wilder without premature overflow or underflow. The root is a binary64 component with an extended upper exponent; an approximate root only locates neighbors, while exact squared midpoint comparisons choose the rounded value, including ties and subnormals. The signal average and signal comparisons retain exact intermediates. Variance publication can overflow while deviation and signal stay finite; each output retains its own overflow contract.

Lazy queues allocate observed history. Other average kinds retain their established smoother fallback and representable component boundaries, including the existing nonpositive-root clamp. Batch consumes no overrides; fast StdDev/Variance request two average slots and Signal requests three. Generated metadata preserves all three configurable averages. Native finite validation precedes selected-input evaluation and stage commitment. The unrelated population-deviation core is unchanged.


### Stationary Extrapolated Levels: exact delayed residual projection (batch 699)

The public formula projects residuals from one and two periods ago using their original bar indices, with zero missing history. It returns zero through index<=length and on exact equality of the two lagged residuals. Between the first and second lag the projection is recent*index/[2*(index-length)]; thereafter it is recent-older/2. Those branch decisions now use exact residuals before publication. Exact means, lagged projection, extrema, midpoint and signal comparisons preserve tiny differences and finite midpoints of overflowing bands.

The two trailing extrema filters compose into a single window of length+max(2,length)-1. Long derived widths and lazy queues/deques replace period-sized storage. Other average kinds retain the established smoother fallback. All four output names/order remain unchanged; native primary is Deviation, and batch intentionally has no custom primary series. Batch consumes no override; fast uses one selected-price average. No change is made to the distinct Stationary Extrapolated Levels Oscillator.


### Support and Resistance Oscillator: exact candle ratio

`Sro = clamp((high - open + close - low) / (2 * TR), 0, 1)`, with zero for zero true range. True range is the maximum of `high-low`, `abs(high-previousClose)`, and `abs(low-previousClose)`; the first bar uses its own selected close. Selected prices replace close only; original open/high/low/volume remain candle inputs, including when the selected price lies outside the candle. The legacy length option remains inert.

Batch, explicit fast, native/live and the core span path share exact unpublished differences, range, ratio and signal comparisons; only the public line is rounded. Equal published values may have different exact slopes. Strong buy/sell takes precedence when the exact slope accelerates in its direction; other nonzero slopes select buy/sell. The legacy 0.3/0.7 crossings imply that same slope direction. Preview does not commit price or signal history; reset clears both. Nonfinite input is rejected before native state advances.


### SVAMA public recurrence: exact volume gain

The public SVAMA line starts at the first selected price. Thereafter `H` is the running maximum volume, `g = volume/H` (zero when H is zero), and `y = previous + g*(price-previous)`. The existing length argument does not affect this formula. Finite signed and zero volumes retain their existing algebraic meaning; negative gains or gains above one can produce unbounded outputs. Exact unpublished gain, recurrence and price-minus-average comparisons avoid premature underflow, overflow and false signal ties. Only the published line rounds to binary64; a later gain of one can recover from a prior overflowing output. Preview does not commit running maximum or recurrence state.

This correction covers public batch, explicit fast, native and builder/live routes. The price-only core and moving-average registry still compute a different adaptive average; their proposed volume-aware alignment is a separate pending scope decision. No core/registry alignment is claimed here.


### Swami Stochastics: exact paired recurrences

Normalize each length to at least one, then use `width = max(1, slowLength-fastLength)` for the original candle high/low extrema. Selected input replaces close only. Starting from zero, `N=(price-lowest+previousN)/2`, `D=(highest-lowest+previousD)/2`, and `Ss=clamp((N/D+4*previousSs)/5,0,1)`. When D is exactly zero, Ss is zero; N and D still advance. The fixed smoothing weights are exactly one fifth and four fifths. Clamping occurs before feedback to the next bar.

Exact unpublished differences, ratios and state prevent false zero ranges and signal ties. The reference unrolls the half-decay recurrences into independently weighted sums whose common power of two cancels. Lazy monotone histories avoid allocating the requested period up front; preview leaves histories and recurrences unchanged. Batch, explicit fast, native and builder/live paths share these semantics. There is no separate Swami core method.

### Batch 705: Tops and Bottoms Finder

The published event detects departure of either rise/fall ratio from exactly one, with rising departure taking precedence. For a nonzero mean, that endpoint holds exactly when its selected-value population variance is zero; a zero mean is never at the endpoint. Before a complete variance window, deviation remains zero. The implementation keeps exact SMA/WMA/EMA/Wilder means and tracks window equality without squaring, rooting or publishing an intermediate ratio. Other average kinds retain their existing smoother. Lengths normalize to at least one and histories grow lazily. Public batch, fast, builder and native/live routes share this contract; there is no separate core. One custom-average callback remains supported.

### Batch 706: Trend Analysis Index

Tai is 100 times the rolling range of the Length1 moving average over Length2 bars, divided by the current selected price; zero price produces zero. Signal smooths the retained complete quotient over Length2. SMA/WMA/EMA/Wilder intermediates and extrema are exact, including signed inputs, unpublished subnormal mean differences and cancellation between overflowing published ratios. Other average kinds keep their existing smoother. Trading signals compare exact price-minus-mean slopes and include the Tai-equals-Signal threshold. Lengths normalize to one and histories grow lazily. Batch and Signal fast output preserve two average callback slots; Tai fast output preserves its single slot. There is no separate core.


### Batch 707: Trend Analysis Indicator

Tai is the Length2 population standard deviation of the Length1 moving average. It stays zero until the deviation window is full. Signal averages Tai over Length1; trading direction uses the Length2 price average minus the Length1 price average, with an inclusive Tai-equals-Signal gate. SMA/WMA/EMA/Wilder means, squared moments and slope comparisons remain exact until the existing binary64 square-root component is rounded using exact midpoint comparisons. Other average kinds retain their existing smoothers. This prevents overflowing squares and lost subnormal mean differences without changing the population formula. Lengths normalize to one and histories grow lazily. Batch preserves slow/fast/signal callback order; Tai fast requests slow only, Signal fast requests slow then signal. Short custom-average arrays zero-fill. Batch, explicit fast, builder and native/live routes share the formula; there is no separate core.


### Batch 708: True Range Adjusted EMA

True range uses the original high/low and the previous selected price (the current selected price on the first bar). ATR is the initial available-bar mean followed by EMA. Gain is `2/(length+1) * min(mult*TR/ATR, 2)`, with ratio one when ATR is exactly zero. The first output is the current price; later outputs follow the exact affine feedback recurrence. Finite negative multipliers retain their signed formula. Trading signals compare successive exact output slopes. Exact intermediates preserve subnormal gains, avoid overflowing true ranges, and permit recovery after an overflowing published output. Lengths normalize to at least one; period arithmetic uses a wide integer and no requested-period allocation is required.

Batch, explicit fast, native/live, and the OHLC core and registry routes use this formula. Selected prices preserve original candle fields. The core validates span lengths and finite inputs before output writes. The registry's price-only plain-EMA fallback remains a separate pending scope decision. The exact recurrence can be slow on extreme fixtures; a local representation optimization is also pending scope approval.


### Batch 709: T-Step Least Squares public formula

The step threshold is the lifetime mean absolute distance from the prior step, multiplied by `2-ER`. ER is zero before Length changes are available; thereafter it is exact net distance divided by rolling absolute travel, or zero for zero travel. A step changes only strictly outside the resulting closed interval. The fit is `priceMean + covariance(step,price)/variance(step) * (step-stepMean)`. The slope is zero before a full population window or when step variance is zero. This exact ratio cancels the correlation and two deviations before rounding, avoiding overflow and root-rounding artifacts. SMA/WMA/EMA/Wilder means and signal comparisons remain exact; other kinds retain existing smoothers. The first prior signal comparison is minus the first price, matching public initialization.

Histories grow with observations; preview/reset and two ordered average callbacks (price, then step) apply across batch, explicit fast and native/live routes. Short custom arrays zero-fill. The public `sc` parameter remains inert because its old intermediate was never used. The separate core/registry projection formula is unchanged pending an alignment decision.


### Batch 710: Turbo Scaler public formula

The first blend is `mean + alpha*(price-mean)`; the second substitutes the first and second moving averages. Ts ranges price against rolling extrema of the first blend; Trigger ranges the first mean against extrema of the second blend. Zero exact range produces zero; ratios are not clamped. Published values are raw ratios, while signals compare their selected moving averages. SMA/WMA/EMA/Wilder stages, blends, extrema, ratios and signal comparisons retain exact intermediates, including opposing ratios that overflow only when published. Other average kinds retain existing smoothers.

Histories allocate with observations, including extreme periods. Alpha must be finite; negative and large finite values remain supported. Batch requests four ordered average callbacks and both fast outputs request two; discovery and short-array zero-fill are preserved. Native preview/reset and full-candle validation apply before state changes. Builder alpha remains 0.5 and its obsolete PctMultiplier remains inert. The distinct centered-percentile core is unchanged pending an alignment decision.


### Batch 711: Turbo Stochastics Fast and Slow

Raw K is the clamped 0–100 position of selected price within rolling original high/low extrema, with zero for exactly equal extrema and preserved orientation for reversed endpoints. Fast regresses raw K and its Length1 moving average; Slow regresses its first and second moving averages. Each regression is the trailing partial-window least-squares endpoint. Its period is `max(1, Length2 + clamp(TurboLength,-Length2,Length2))` after normalizing Length2 to at least one. The addition uses a wide integer, so the legal maximum is 4,294,967,294; growing histories avoid eager allocation. Exact range ratios, SMA/WMA/EMA/Wilder stages, regression moments and RSI-style 70/30 signal decisions are retained through publication. Other averages retain existing smoothers.

Batch preserves its nested stochastic callback contract: two preliminary, unused length-3 requests for non-SMA kinds, then one length-1-period request for Fast or two for Slow (here length-1-period means the configured Length1). Explicit fast requests zero/one averages for Fast Tsf/Signal and one/two for Slow Tsf/Signal. Discovery, ordering, short-result zero fill, original candles under selected input, full native candle guards and preview/reset remain covered. No shared stochastic or regression helper was changed.


### Batch 712: Uber Trend Indicator

Uber Trend retains rolling advances A and declines D and rolling directional contributions U and V, where each contribution divides the current volume by that bar's rolling directional price sum. The nested ratio is `(A/D)/(U/V)`, followed by `(ratio-1)/(ratio+1)`. If D, U or V is exactly zero, ratio is defined as zero and the output is -1. A ratio of exactly -1 produces zero. Production cancels the nested fractions to `(A*V-D*U)/(A*V+D*U)` only after preserving those guards; the independent oracle evaluates the nested quotients directly. Signed finite volume remains supported.

Price differences, directional sums, volume contributions, products and two-bar signal comparisons remain exact through final publication. Histories grow with observations. Batch, core, explicit fast and native/live routes share this formula; selected prices retain original volume. The core validates input lengths and finite values before output writes and supports same-start in-place price or volume. Native full-candle guards, preview/reset and empty/extreme-period behavior remain covered.


### Batch 713: Variable Length Moving Average

The selected moving average over MaxLength defines the center. Population variance is zero before a full window; zero variance leaves the adaptive period unchanged. Otherwise the period increases inside the inclusive quarter-deviation band, decreases strictly outside the 1.75-deviation band, and holds between them, clamped to normalized bounds. Production compares `16*(price-center)^2` against variance and `49*variance`, avoiding rounded roots and overflow. The output is a first-price-seeded EMA using gain `2/(period+1)`. SMA/WMA/EMA/Wilder centers, variance, feedback and signal differences retain exact intermediates; other average kinds retain existing smoothers.

The authorized core/registry change removes the former whole-series maximum-volatility normalization and its lookahead. A two-bound core overload uses the public formula. The legacy single-length core/registry maps normalized length to bounds `[length, min(Int32.MaxValue, 2*length)]`, with wide integer arithmetic. Appending future bars leaves historical values unchanged. Core spans validate before writes and allow same-start in-place input. Public routes preserve the single center callback, selected inputs, short-result zero fill, full-candle guards and preview/reset. The adaptive Length remains a secondary output; Vlma is explicitly the primary output. Histories grow with observations rather than allocating the requested maximum upfront.


### Batch 715: Ultimate Volatility numerical correction

Ultimate Volatility remains the rolling sum of absolute selected-close minus original-open differences divided by the full normalized period. Missing warmup bodies are zero; the period is at least one. The local window stores exact binary64 units and grows as bars arrive, so a large intermediate body or period does not force overflow or eager allocation. Publication rounds once; genuine output overflow remains infinity in raw routes and is rejected by the public runtime contract.

The moving-average choice affects batch signals only. This batch and its fast path retain their existing no-component-callback contract. Signals compare exact close-minus-mean differences and the unrounded volatility against one; a value immediately below one must not enable a signal merely because publication rounds it to one. Native preview/reset and invalid-bar rejection preserve state. Fast selected inputs retain the original opens. Typed native and streaming factories are now mapped.

The independent oracle directly sums rational body sizes for each window. The 49 distinct focused checks passed, including the repaired no-callback threshold hand and the existing streaming parity case. The 24 compiled behavioral mutation checks remain queued; enrollment alone is not qualification. This records the repository formula convention, not independently authenticated external attribution.


### Batch 714: Ultimate Moving Average and bands

The public UMA uses the causal Variable Length period, exact typical price `(original high + original low + selected close)/3`, and directional typical-price times volume totals over that period. MFI is `100*positive/(positive+negative)`, clamped to [0,100]; zero negative flow selects 100, while zero positive flow or a signed-flow pole selects 0. The exponent is `acc + abs(2*MFI-100)/25`. The numerator weights available prices by `(period-lag)^exponent`; unavailable warmup prices are zero, and the denominator always contains all period weights. Period bounds normalize to at least one with maximum at least minimum. Finite signed prices and volumes and any finite acceleration remain accepted. The obsolete builder Length option remains inert.

Weights are normalized at the dominant endpoint. Exact integer sums, bounded fractional powers, centered block moments, and bounded omitted tails avoid overflow and loops over billions of missing bars. History grows with received bars. Bands use acceleration 1 and population deviation over the minimum period, with zero width until that window is full; the unrounded center and deviation are combined before publication. A negative multiplier retains signed band orientation. Native selected-input routing preserves original high/low/volume.

Published values refine certified intervals until both endpoints round identically or nonzero relative uncertainty is at most 2^-64. The latter permits at most one representable step at a rounding boundary. Exact rational cases retain exact midpoint handling; signs and subnormal expectations have separate tests. The independent oracle uses a direct logarithm series, a binomial product-limit exponential, Euler-Maclaurin sums with remainder bounds, and integer-search square roots. Its scalar comparison budget is zero absolute error and 5e-16 relative error with equal signs; direct decimal fixtures additionally enforce a one-step budget and exact tiny values.

The focused evidence covers 144 distinct passing cases after the 99-case routing repair rerun; all 24 focused integration checks also passed after incorporating the latest published changes. Both isolated mutation baselines passed, and all 39/39 compiled behavioral faults were caught by complete failing test reports on matching source snapshots.

The internal core and its registry/shared moving-average callers still implement T3-of-T3. Alignment is a separate pending scope decision; this entry does not claim those routes implement public UMA. This bounded qualification does not establish hosted, platform, package, performance, or release readiness.


### Batch 716: Value Chart public numerical contract

Value Chart subtracts the selected input moving average (exact high/low midpoint by default) from each candle coordinate and divides by one twenty-fifth of the five most recent rolling high/low ranges. The range period is ceil(normalized length / 5), clamped to 2..530; missing range history contributes zero. A zero total range publishes zero. The selected-input per-bar range projection and original open are preserved. Batch retains two component-average requests (basis, then coordinate signal mean); each fast output retains one. Short callback replacements are zero-filled. Batch continues to publish four named outputs and an empty custom-values list.

Finite signed candles are supported. Exact fractions preserve the midpoint, range differences, and coordinate division across binary64 overflow and subnormal underflow. Standard finite-window averages are exact; EMA/Wilder feedback uses the existing 106-bit extended shared average. Genuine final binary64 overflow follows the overflow-rejection validation contract. Exact slope and threshold comparisons determine signals. Histories grow with observed data; extreme requested periods do not allocate period-sized arrays. Native previews do not commit state, and invalid bars are rejected before mutation.

The distinct unused core formula is unchanged pending the separately requested alignment decision. 48 distinct focused checks passed; 32 mutation candidates are queued for qualification.


### Batch 717: Variable Adaptive Moving Average public contract

Four component averages are requested in close/open/high/low order. The close is the selected series; open remains original, and high/low retain the shared per-bar selected-range projection. The adaptive gain is zero for an exactly zero averaged range; otherwise it is the absolute averaged candle body divided by the averaged range, clamped to the existing binary64 constants 0.01 and 0.99. Negative ranges therefore choose the lower gain. The first output is seeded with the current price. The recursive blend and signal margins remain extended exact fractions, retaining feedback even when its published value rounds to zero. Finite-window component means are exact; the shared EMA/Wilder component means use 106-bit feedback. Unsupported average kinds retain their existing smoother fallback.

Batch and fast routes preserve four callback slots and zero-fill short replacements. Periods normalize to at least one and standard mean histories grow only with observations. Native previews do not commit state; invalid candles are rejected before state mutation. Batch and fast validate original candle fields even when a selected series projects the ranges. The previous fast implementation used original ranges for selected input and is aligned with batch/native projection in this batch.

The distinct price-only core/registry formula and its SVAMA delegation await the separately requested design decision. 48 distinct focused checks passed; 31 mutation candidates are queued for qualification.


### Batch 719: Varadi Oscillator exact rank contract

Varadi smooths selected price divided by the exact high/low midpoint, then reports 100 times the inclusive rank count among the preceding length means divided by the full period. Unavailable startup history contributes only the established single zero seed. A zero midpoint gives a zero ratio. SMA and WMA retain exact finite-window fractions; EMA and Wilder retain exact recursive fractions because even a subnormal contribution beside the largest finite value can change a later rank. Other smoothing kinds preserve their existing smoother fallback.

Exact comparisons replace the former 1e-12 tie tolerance. Only the final bounded percentage is rounded to binary64; integer rank changes determine signals. A lazy order-statistic tree preserves window expiry, duplicate counts and previews without allocating by requested period. Batch retains its standard-mean callback bypass; fast retains its single callback and zero-filled short replacements. Selected ranges follow the shared projection, with original candle validation before calculation.

The OHLC-aware core now uses this same formula. Its price-only overload treats each price as a flat candle; invalid spans are rejected before output writes. 56 distinct focused checks passed, including independent extreme-rank hands, core/span tests and existing spike-expiry/naive/golden contracts. 34 mutation candidates await qualification.


### Batch 721: Volume Weighted Relative Strength Index

Finite signed price changes are multiplied by finite signed volume before gain/loss classification. Standard SMA, WMA, EMA, and Wilder component means retain exact rational values, including products beyond either binary64 exponent limit. The centered value is 100 times (up minus down) divided by (up plus down); zero down retains the published +100 precedence, including a motionless series. Final smoothing consumes unpublished centered values. WMA includes zero-filled warmup history; period arguments are floored at one and history grows only as observations arrive.

Fast calls preserve three component override slots (gains, losses, centered values); batch calls bypass these hooks. Both retain selected prices and original volumes. Native previews do not commit price or smoother history. Original OHLCV is validated before selected inputs can hide invalid values. The approved internal core alignment uses the public WMA and three-bar final smoothing with its existing length argument/default 14; the public default length remains 10. Span outputs are written only after validation/calculation, including aliased inputs.

Independent references use signed/absolute flow ratios and direct rational windows, separate from the production gain/loss recurrence. All 43 focused checks passed on net10.0. Inventory confirms four additional enrollments (6,701/7,131 total); the 33 behavioral faults in the repaired snapshots await isolated qualification.


### Batch 722: Volume Price Confirmation Indicator

VPCI multiplies (slow VWMA minus slow price MA), (fast VWMA divided by fast price MA), and (fast volume MA divided by slow volume MA). A zero fast price mean or slow volume mean yields zero. VWMA uses full-window warmup and returns zero for exactly zero total signed volume. Exact products, signed volume sums, standard SMA/WMA/EMA/Wilder means, component ratios, and final signal smoothing retain information until publication. True nonrepresentable raw results publish signed infinity, allowing existing v2 overflow-rejection rules to distinguish them from lost finite results. Exact finite history remains usable after such a publication.

The approved core alignment replaces its difference-of-confirmations formula with the public three-factor SMA formula, retaining short/long arguments and atomic/aliased span handling. The typed length controls only signal smoothing. Batch retains its VWMA, volume, price, then signal evaluation order without consuming override hooks. Explicit fast routes retain price-before-volume order, six primary override slots and seven signal override slots. Native previews/reset and extreme periods use only observed history.

All 45 focused checks passed on net10.0, including an independent 13/30 line and 13/60 signal hand, both selected-input fast outputs, exact subnormal signal preservation, signed-volume cancellation, and recovery after true overflow. The 41 targeted mutation candidates in the repaired snapshots await isolated qualification.


### Batch 724: Vortex Bands

The basis is the selected price mean; the half-width is twice the nonnegative part of the mean absolute price-to-basis deviation. Exact standard SMA/WMA/EMA/Wilder means and the default two-stage McNicholl correction retain the unpublished basis, residual, and width. McNicholl uses a period floor of two and (2*n*first - (n+1)*second)/(n-1). Its width can be negative; that width clamps to zero. Signals compare the exact width with zero: Buy for positive width, otherwise None. True unrepresentable final outputs publish signed infinity without poisoning subsequent history.

For period two and prices [1,3,0], the final upper/middle/lower bands are 13/36, -1/6, and -25/36. For [0,epsilon], the second basis is 5/4 epsilon and width 5/16 epsilon, preserving the final rounded bands 2epsilon/epsilon/epsilon. An overflowing middle band can still have a finite lower band. Both original batch and explicit fast standard-mean dispatch bypass component override hooks; that behavior is preserved. Unsupported means retain existing dispatch. No shared McNicholl core is changed.

All 53 focused checks passed on net10.0. The 34 prepared behavioral faults await isolated qualification.


### Batch 725: Waddah Attar Explosion

T1 is sensitivity times the difference between the current MACD and the MACD of the one-bar, zero-padded price lag. T2 applies the same construction to the two-/three-bar lags. Production uses exact EMA filters of price differences, while the independent reference subtracts four separately filtered lagged price series. E1 is four times the full-window population deviation, zero before the window fills. TrendUp and TrendDn are the nonnegative directional parts of T1.

Exact means, products, and moment sums survive intermediate overflow and underflow. The deviation is scaled before its final square-root rounding. Signals retain the published conditions, with exact comparisons of nonnegative squared values replacing comparisons of rounded roots. For prices [1,3,0,4,2], fast=2, slow=3, sensitivity=6, T1 is [0,0,-9,1,-11/3], T2 is [0,0,2,7/3,-61/18], and E1 is [0,4,6,8,4]. For [0,epsilon], E1 is 2epsilon even though the unscaled deviation rounds to zero. At the final bar of [1,3,0,4], sensitivity=48 gives an exact trend/width tie; adjacent binary64 sensitivities select Buy or Sell.

Original batch callback bypass, output order, selected inputs, and empty custom outputs are retained. Native previews/reset use lazy observed history for extreme periods. All 76 focused tests passed on net10.0; 37 mutation candidates await isolated qualification.


### Batch 730: Z Distance from VWAP output normalization

The output follows LazyBear calc_zvwap: each residual is price minus its own trailing volume-weighted mean (or selected moving average), divided by the root mean square of the last length residuals. Width is zero until the full residual window is observed. A zero sum of signed volumes defines a zero mean, and zero-volume observations remain in the residual history.

Exact products, sums, means, and squared residuals survive intermediate overflow and underflow. The final signed root of length*currentResidualSquared/sumResidualSquared is rounded directly to binary64. Observed-history queues handle extreme periods. For prices [1,3,2], volumes [1,1,2], and length 2, means are [1,2,7/3], residuals [0,1,-1/3], and scores [0,sqrt(2),-sqrt(1/5)]. A one-ULP price move and subnormal volume-price products retain their normalized movement.

The independent reference translates each price window by the current price and uses binary64 root bisection, separately from production's weighted sum and integer-root rounding. Registered means retain callback bypass; selected inputs retain original volumes. Signal comparisons still use published scores. Exact signal comparisons are a pending scope decision; these five configurations remain in the numerical backlog.



### Batch 731: Volatility Quality Index

Quality is half the sum of close-change/true-range and candle-body/range. Degenerate ranges carry the preceding quality. Each cumulative contribution is abs(quality)*(close-change+body)/2, and the two signal outputs smooth the unpublished cumulative line. The first previous close is the current close. Exact rational differences, ratios, accumulation and means preserve overflowing and underflowing intermediates; signal margins are compared before rounding.

For OHLC candles (0,2,0,2), (2,4,1,3), (1,3,3,3), the cumulative line is [1/2,5/6,7/6], SMA2 is [0,2/3,1] and SMA3 is [0,0,5/6]. Final margins tie exactly at 1/6, so the third signal is Buy; subtracting separately rounded outputs would produce a false StrongBuy. Opposing MaxValue candles recover a finite line after an intermediate published infinity without contaminating smoothing state.

Selected prices preserve the original open and use the existing per-bar synthetic-range policy; previews and reset preserve that selection. Batch registered mean callbacks remain bypassed. Fast primary consumes no mean callback; each fast signal consumes one, zero-padding short overrides. All 84 distinct focused checks passed; 37 mutation candidates await isolated qualification.



### Batch 735: Volatility Based Momentum

Momentum is the close difference over length1 divided by the selected average of true range over length2, with zero output until the lag is available or when ATR is zero. First-bar true range uses the current close as the previous close. The signal averages the unpublished momentum ratio over length1; trading signals compare exact current and previous line-minus-signal margins. SMA, WMA, EMA and Wilder means retain exact differences, ranges and ratios, avoiding premature overflow and subnormal loss. Unsupported moving-average dispatch remains on its existing path.

For closes [0,2,4,0,2] with each candle extending one unit above/below its close and both periods 2, true ranges are [2,3,3,5,3], momentum is [0,0,4/3,-1/2,-1/2], and its simple-average signal is [0,0,2/3,5/12,-1/2]. Signals are None, None, StrongBuy, StrongSell, None. Selected prices use the existing per-bar synthetic-range policy, and native history grows only with observed bars.

Armed batch and fast primary consume the ATR callback once; fast Signal additionally consumes the signal callback. Unarmed standard batch/primary bypass mean callbacks, while fast Signal requests its one final mean. Short replacements are zero-padded. Extreme signed periods clamp consistently to at least one.



### Batch 736: Volatility Moving Average

The public formula measures 100*(price-selected mean)/population deviation, smooths that score, clamps its magnitude to 100, and rounds magnitude/lookback to an integer using ties-to-even. It then rounds max(1,length*(10-level)/10), again ties-to-even, selects a zero-padded linearly weighted average at that period, and applies final smoothing. The local implementation retains rational-coefficient square-root sums through the score mean. Exact square-class cancellation and adaptive outward integer bounds resolve period thresholds before rounding; price means and trading-signal margins remain rational.

For prices [0,2,4,2,0,0], SMA means with length/lookback/smoothing 10/2/2 select periods [10,1,1,10,1,1] and publish [0,1,3,146/55,36/55,0]. The third and fifth trading signals are Buy and Sell because their respective margins exactly tie the prior margin. Tests also distinguish the threshold displaced by sqrt(2+epsilon)-sqrt(2), beyond fixed binary64 precision, from its exact tie.

The internal core and VolMaCore registry now use the causal public formula. The existing length argument maps to output length, with default lookback 10 and smoothing 3; an additional internal overload accepts all three periods. Overlapping spans are safe. Typed public options keep their existing length/mean-type fields and defaults. SMA, WMA, EMA and Wilder paths use the exact local kernel; unsupported means preserve legacy dispatch. Fast computation requests its three component means in order; batch bypasses registered callbacks. History grows only with observed samples.



### Batch 729: Wilson Relative Price Channel

The four channels retain exact selected-price RSI distances through smoothing and price multiplication: price * (1 - MA(RSI - threshold)/100). Their order is S1=oversold, S2=lower neutral, U1=overbought, U2=upper neutral. Signals compare exact price-minus-upper/lower-channel margins, including signed prices. The independent reference derives RSI from net and absolute changes; production averages separate gains and losses.

For prices [10,12,9], EMA length 2 and smoothing 1, the final RSI is 100/7. Final S1/S2/U1/U2 are 729/70, 1647/140, 981/70 and 1773/140; signals are StrongBuy, StrongBuy, StrongSell. Exact intermediate products retain finite channel results at MaxValue. Arbitrary finite thresholds and signed prices remain supported, with observed-history allocation for extreme periods. Batch mean callbacks remain bypassed; the fast path consumes one distance slot when unarmed and gain/loss/distance slots when armed, padding short replacements with zero.

All 83 focused tests passed on net10.0. The 32 mutation candidates remain queued for isolated qualification.


### Batch 728: WaveTrend price conventions and numerical arithmetic

Public batch, fast and streaming WaveTrend use OHLC4. The OHLC-aware internal core now shares that formula; the retained three-price overload explicitly uses HLC3. Exact candle sums, residuals, deviation normalization and smoothing avoid intermediate overflow and preserve cancellation and subnormal movement. Selected inputs preserve original candle fields and feed the selected price through the shared formula. Core span writes are atomic and support overlapping inputs.

Independent references and hand checks cover normalization, signal smoothing, four supported averages, extreme periods and input values, preview/reset behavior, callbacks and invalid candles. All 44 focused tests passed. The 36 behavioral mutation candidates remain queued; enrollment does not establish release readiness.


### Batch 738: Volume Positive/Negative exact votes and smoothing

The public formula compares HLC3 movement with one tenth of ATR, accumulates positive/negative volume votes, divides by the positive volume average (otherwise one) and the configured period, and smooths the resulting line. Exact rational arithmetic retains threshold ties, subnormal movements and large price/volume products. True range uses the previous original close; the first candle uses its current close. Selected prices retain their custom range policy. Windows allocate only observed history.

The batch and native routes share exact arithmetic with an independent rational reference. Explicit typed-arm dispatch uses the existing batch binding and preserves both published output keys. Component substitutions preserve volume/ATR/signal ordering and short-replacement padding. All 83 indicator cases passed across retained and repaired runs; 31 behavioral mutation candidates remain queued.


### Batch 740: ZigZag exact extrema and repainting paths

ZigZag uses exact midpoint seeds, absolute pivot magnitudes for reversal thresholds, strict reversal comparisons and exact interpolation between final extrema. A threshold equality holds the current leg; the adjacent representable price can reverse it. Signals compare exact unpublished path slopes, preserving subnormal movements and avoiding artificial acceleration from rounded outputs. Extreme finite candles and deviations are validated before output writes; helper spans support overlapping input/output.

ZigZag intentionally redraws prior legs when a later extreme extends them. Its selected-input builder path therefore computes the complete series through the same batch calculation and custom candle-range policy; it does not claim streaming parity or prefix invariance. The internal TrendCore variant remains unchanged pending its separate scope decision.


### Batch 741: Hampel public raw-MAD and EMA variant

Public Hampel uses the median of observed prices, the median of absolute distances from that same median, and a raw-MAD threshold. Equality accepts the current price; larger distances replace it with the median. The published result is a zero-seeded EMA with alpha=2/(length+1). Exact rational medians, deviations, thresholds, EMA memory and signal margins preserve tiny values and avoid intermediate overflow. History grows only with observations, including Int32.MaxValue periods.

For [0,2,100], length 3 and factor 3, outputs are [0,1,1.5]. For [0,1,2], factor 1 gives final 1.25; the immediately smaller binary64 factor gives 0.625 because it also changes the prior two-point decision. Constant [2,2,2] gives signals StrongBuy, Buy, Buy as the positive margins decline. All finite factors remain supported. The internal core/registry retains its distinct Gaussian-scaled unsmoothed variant pending the separate alignment decision.


### Batch 665: McGinley public overflow-safe recurrence

The public recurrence seeds the first output with price, resets to price when the prior output is zero, and floors its adaptive denominator at one. Each step feeds the published binary64 output into the next step, preserving the public feedback convention. For current price x and prior published output p, set q=k*N*x^4 and v=p^4. If p=0 or q<=v, output x; otherwise compute [p*(q-v)+x*v]/q using exact rational intermediates. This is algebraically the original ratio-based update and remains a convex combination for finite inputs, avoiding overflow in price differences and fourth powers.

For prices [-8,8,4], period 2 and k=1, outputs are [-8,0,4]. Zero/negative finite factors reduce to price. Positive factors, including subnormal and MaxValue values, remain supported. Exact signal margins distinguish rising from shrinking positive margins. The internal core/registry retains its separate zero-state convention pending the earlier alignment decision.


### Batch 737: Volatility Wave fractional weights and causal core

The public formula computes a power-weighted price average with zero-filled warmup history. Its exponent lies in [1,4]; clamp decisions use the exact fourth-power relation p^4=10000*kf^4*variance/price^2 before extracting roots. For nonpositive price or factor it uses exponent one. Exact variance and outward bounds on fractional powers and large-period sums preserve normalization without allocating a period-sized buffer. The smoothing period remains clamp(ceil(sqrt(length)),2,530). Two mean stages produce 2*first-second while retaining bounded expressions until publication.

The core/registry now uses the causal public WMA formula with factor 2.5; future bars no longer change prior core outputs. Overlapping spans are supported. A length-two zero-factor impulse [27,0,0,0,0] gives [16,12,0,-1,0]. Finite signal margins retain exact comparisons against published lines; an overflowing line retains its extended expression for signal evaluation and later recovery. Supported numerical means are SMA, WMA, EMA and Wilder; other mean kinds retain their legacy fallback. The fast route retains two ordered smoothing callbacks and zero-pads short replacements.


### Batch 744: Kalman Smoother exact quadratic recurrence

For q=max(1,length)/10000 and g=sqrt(2q), the public formula seeds its level with the first price and velocity with zero. Each step uses d=price-level, velocity+=q*d, level+=g*d+velocity. Values retain exact rational coefficients a+b*sqrt(2q); only published outputs round to binary64. Opposite-sign comparisons reduce to rational squares; publication refines outward root bounds until both endpoints round identically. This preserves cancellation, signal direction and recovery after overflowing intermediate publications. Signals compare the exact price-minus-level margin and its change.

The independent oracle eliminates velocity, using y[n]=(2-q-g)y[n-1]+(g-1)y[n-2]+(q+g)x[n]-g*x[n-1], with independently computed root bounds. Batch, fast and native/streaming routes share the corrected public recurrence. The separate internal core/registry formula remains unchanged pending its distinct scope decision.


### Batch 746: Karobein Oscillator

The public formula smooths selected prices, forms the ratio to the previous smoothed price (zero when that previous value is zero), splits ratios by the exact direction of the mean change, and smooths each half. With ratio r, falling average a and rising average b, c = clamp(r/(r+b),0,1) and Ko = clamp(2r/(r+c*a)-1,0,1). A zero ratio retains the existing zero-output convention. All three moving-average stages, for supported SMA, WMA, EMA and Wilder kinds, retain exact rational intermediate values, with exact mean-direction and signal comparisons; published outputs alone round to binary64. Other average kinds retain their existing fallback.

Exact nonzero-ratio poles in r+b or r+c*a throw ArgumentException, as explicitly approved. The native path previews every stage before committing, so rejected preview or final bars cannot advance a partial state. SMA(2) prices [1,1,3,-7] exercise the rising pole; [-3,2,-1,-1,-1] exercise the falling pole. Adjacent representable inputs remain distinct and valid where their exact denominators are nonzero.

The independent reference uses separate fractions and direct finite-window sums, evaluating the final fold as (r-c*a)/(r+c*a). Length-one [1,2,1] gives [0,1,0]; EMA(2) gives [0,1,385/1169]. Fast routing preserves all three component callback slots and zero-pads short replacements; captured caller input is restored. Period storage grows with consumed history. The distinct internal core remains unchanged pending its separate alignment decision.


### Batch 890: Ultimate Trader six-score normalization and OHLCV core

The public formula combines body/range, close position, rolling close position, momentum/range, signed true-range stochastic and signed volume stochastic as 100*sum(scores)/sum(abs(scores)); zero total magnitude produces zero. Finite inputs can yield unrepresentable differences or individual scores, so the six terms and their normalization now use exact arithmetic, rounding only the bounded raw result. First-bar true range uses the current close; first-bar momentum uses zero. A flat close suppresses rolling position and the two signed stochastics. Selected prices preserve the original OHLCV fields.

The raw result is smoothed by lookback, smoothing, then smoothing again; Uto is the second stage and Signal is the third. Each stage publishes binary64, and recursive feedback retains that publication boundary. SMA/WMA/EMA/Wilder use local lazy history; other average kinds retain their existing fallback. The public length parameter remains inert. Fast callbacks retain their order and zero-pad short replacements. Trading signals compare exact differences of published lines.

The unused three-RSI internal core is replaced by an OHLCV-aware implementation with explicit lookback, smoothing and range periods. It validates span lengths and reads all input before writing overlapping output. Independent references use direct rational candle windows and separate smoothing. The one-bar extreme signed-candle example produces exactly 100; the mixed-score hand -25,-50,100/3,-100/9,-100,0 produces -5500/79 before smoothing.


### Batch 898: Klinger volume oscillator family

KlingerVolumeOscillator, KlingerSignal and Kvo retain the public volume-force formula:
trend follows the exact change in high + low + effective close, with ties retaining the
previous direction; cumulative range spans the current trend and the bar preceding its
reversal. An exactly zero cumulative range retains the existing zero-force convention.
Signed finite prices, volumes and ranges keep their existing domain.

Volume force and SMA/WMA/EMA/Wilder smoothing now retain exact rational intermediates
through the oscillator, signal and histogram. Only public series are rounded to binary64;
an independently proven overflow of a final output uses the validation framework's
existing overflow-rejection contract. A three-bar hand with high=1, low=0, volume=1 and
closes [0, epsilon, 2*epsilon] yields 100/9 for EMA(1)-EMA(2), even for the smallest positive
subnormal epsilon. History allocation grows with observed bars, not an extreme requested
period. Exact recursive state can grow in integer precision as history grows.

Batch/fast/native/live routes and the oscillator/signal cores share this calculation.
Selected prices preserve original high/low/volume fields. The established EMA route
continues to request only its signal component callback; other averages request fast,
slow, then signal. Core output can overlap any input span, and suffix elements are untouched.
Unsupported average kinds retain their prior fallback and reference eligibility.
