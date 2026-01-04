# Modernization and Performance Plan

Generated: 2026-01-03

## Goals
- Preserve the public API where possible while enabling a high performance core.
- Add first class streaming support (tick-by-tick updates).
- Use spans and pooled buffers internally, returning List<double> at the API boundary.
- Eliminate avoidable LINQ allocations in hot paths.
- Reduce StockData memory overhead and duplication.
- Keep net461 support while adding modern TFMs (net8 to net10).

## Constraints
- Must keep net461 support (or lower if feasible).
- Any breaking change must be explicit and justified.
- Performance and allocation measurements are required for every phase.

## Streaming Defaults
- Update granularity: trades and quotes (tick-level) with optional bar aggregation.
- Default timeframes: 1s, 5s, 1m, 5m, 15m, 1h, 1d, plus tick-level.
- Updated bars enabled by default.
- RegisterAllIndicators includes all indicators by default with lazy loading.
- Facade options are nullable and use industry-standard defaults.

## Proposed Architecture
1. Core indicator engine (internal)
   - Span-first APIs: ReadOnlySpan<double> inputs, Span<double> outputs.
   - Optional scratch buffers via ArrayPool<double>.
   - No LINQ in core loops, no per-iteration allocations.

2. Compatibility layer (public)
   - Existing Calculate* methods stay, but become thin adapters.
   - Inputs are sourced from StockData and fed to core spans.
   - Outputs are materialized as List<double> for user ergonomics.

3. Streaming layer
   - Stateful indicator objects (for example: IIndicatorState or IndicatorStateBase).
   - Update(...) accepts raw values or TickerData and returns latest value(s).
   - Reuse the same rolling primitives as batch for exact parity.

4. Data model
   - Column-first internal storage (arrays or ReadOnlyMemory).
   - Row view (TickerData) is optional and lazily generated.
   - Dates stored once and shared by both views.

5. Multi-target strategy
   - net461 + net8.0 + net10.0 (or latest available).
   - net461 uses System.Memory for spans (no CollectionsMarshal).
   - net8+ uses CollectionsMarshal.AsSpan and other modern APIs behind #if.

## Implementation Phases
Phase 1: Foundations
- Introduce span-based core utilities and rolling primitives.
- Add internal buffer pooling and centralized math helpers.
- Add benchmark coverage for representative indicators.

Phase 2: StockData modernization
- Convert StockData to column-first with lazy row view.
- Keep existing constructors, but avoid duplicate storage.
- Add explicit methods to force row or column materialization.

Phase 3: Streaming support
- Add streaming interfaces and stateful indicator implementations.
- Validate streaming parity vs batch for a fixed dataset.

Phase 4: Migrate indicators
- Move indicators to core (span-based) and keep adapter wrappers.
- Replace LINQ in hot loops.
- Apply rolling-window optimizations.

Phase 5: Cleanup and packaging
- Split monolith calculation files for build and navigation.
- Update docs, examples, and benchmark instructions.

## Performance Measurement Standard
- Use BenchmarkDotNet in Release.
- Datasets: 10k and 100k rows, multiple window sizes (14, 50, 200).
- Report mean, error, StdDev, and allocations.
- Compare against baseline (current main/master) using the benchmark harness.

## User Stories (with Acceptance Criteria and Performance Requirements)

US-01 Core span engine
- Story: As a developer, I can call internal indicator functions that accept spans and write to spans.
- Acceptance:
  - Core functions are internal and tested via adapters.
  - Results match existing Calculate* outputs (numerical parity).
  - No LINQ or enumerator allocations in core loops.
- Performance:
  - For SMA(200, 10k), ratio <= 0.10 vs baseline, Alloc Ratio <= 0.25.

US-02 Streaming indicator state
- Story: As a user, I can process new ticks without reprocessing full history.
- Acceptance:
  - Update(...) methods exist for key indicators (SMA, EMA, RSI, MACD, Bollinger, Ulcer, Vortex).
  - Streaming results equal batch results for the same data sequence.
  - Zero allocations per Update call in steady state.
- Performance:
  - Update throughput >= 500k updates/sec for SMA(200) on the benchmark machine.

US-03 StockData column-first storage
- Story: As a user, I can construct StockData without duplicating row and column data.
- Acceptance:
  - Constructing from columns does not build TickerDataList unless requested.
  - Constructing from rows does not duplicate columns until requested (lazy).
  - A new method or property can force materialization of the other view.
- Performance:
  - Memory usage for 100k rows reduced by >= 40% vs baseline.

US-04 Row view access
- Story: As a user, I can still iterate row-based data with dates.
- Acceptance:
  - TickerDataList is available and ordered by input date sequence.
  - Row view generation is deterministic and cached when requested.
- Performance:
  - Row view generation uses a single pass and no LINQ.

US-05 Span-based adapters
- Story: As a user, I can keep the existing Calculate* API while gaining the new core performance.
- Acceptance:
  - All public Calculate* methods remain.
  - Results match previous outputs within tolerance.
- Performance:
  - Adapter overhead is <= 5% of total time vs core (measured for SMA and RSI).

US-06 Rolling window conversion
- Story: As a user, I get O(N) implementations for windowed sums, averages, min/max, and percentiles.
- Acceptance:
  - All previous TakeLastExt + Sum/Average/min/max patterns are replaced.
  - No per-iteration list slicing or LINQ.
  - Rolling min/max uses a deque-based window (or equivalent).
- Performance:
  - For each converted indicator, ratio <= 0.60 vs baseline and Alloc Ratio <= 0.50.

US-07 LINQ removal in hot loops
- Story: As a developer, I can avoid LINQ allocations in tight loops.
- Acceptance:
  - No LastOrDefault, SequenceEqual, Where, Select, or TakeLastExt inside indicator loops.
  - Equivalent loop-based logic passes existing tests.
- Performance:
  - Allocation reduction >= 2x on affected indicators.

US-08 Output dictionary deferral
- Story: As a user, I can avoid allocating OutputValues if I do not need it.
- Acceptance:
  - OutputValues is created lazily or via an options flag.
  - SignalsList and CustomValuesList can be skipped via options.
- Performance:
  - Indicators run with >= 30% less allocation when outputs/signals are disabled.

US-09 Rounding strategy
- Story: As a user, I can keep rounded output while internal math stays full precision.
- Acceptance:
  - Internal calculations do not round.
  - Output rounding is applied only when requested.
- Performance:
  - No additional allocations beyond final output lists.

US-10 Indicator options
- Story: As a user, I can control output detail and speed.
- Acceptance:
  - Options exist for signals on/off, outputs on/off, rounding digits.
  - Options are nullable with industry-standard defaults (facade-friendly).
  - Defaults preserve current behavior.
- Performance:
  - Options provide measurable allocation savings (>= 20% when disabled).       

US-11 Multi-target support
- Story: As a user, I can run the library on net461 and newer runtimes.
- Acceptance:
  - Builds succeed for net461, net8.0, and net10.0 (or latest).
  - net461 uses safe fallback paths (no CollectionsMarshal).
- Performance:
  - net8+ benchmarks show improvements vs baseline; net461 does not regress vs baseline net461.

US-12 File modularization
- Story: As a developer, I can navigate and compile the codebase more efficiently.
- Acceptance:
  - Giant calculation files are split by category without changing public API.
  - Build times improve in incremental builds (measured locally).
- Performance:
  - Incremental rebuild time reduced by >= 20% for small changes.

US-13 Benchmark coverage
- Story: As a developer, I can measure and compare performance for key indicators.
- Acceptance:
  - Benchmark project includes representative indicators and baseline comparisons.
  - Reports are generated in Markdown and CSV.
- Performance:
  - Benchmarks run in < 2 minutes for the default dataset sizes.

US-14 Streaming plus batch parity tests
- Story: As a developer, I can verify streaming equals batch results.
- Acceptance:
  - Automated tests compare streaming output to batch output for fixed datasets.
  - Parity is validated for at least 10 indicators initially.
- Performance:
  - Tests complete in < 30 seconds locally.

US-15 Specialized small-window fast paths
- Story: As a user, I get faster performance for small window sizes.
- Acceptance:
  - Rolling window helpers use linear scan for small lengths and tree/heap for large lengths.
  - Thresholds are documented and configurable in code.
- Performance:
  - For length <= 32, time improves by >= 20% vs tree-only approach.

US-16 Provider streaming adapters (Alpaca first)
- Story: As a user, I can plug in a streaming provider and register indicators that update automatically.
- Acceptance:
  - Define provider-agnostic interfaces (IStreamSource, IStreamSubscription).   
  - Ship an Alpaca adapter that subscribes to bars/trades/quotes.
  - Users can register indicators per symbol/timeframe and receive updates via events or callbacks.
- Performance:
  - Adapter overhead adds <= 10% latency on top of provider message handling.   
  - No extra allocations per message beyond provider deserialization.
  - In-process streaming throughput >= 250k msgs/sec (trade/quote) with 1 timeframe and 5 indicators.
  - End-to-end p95 latency <= 10 ms from message receipt to indicator output.

US-17 Trade/quote to bar aggregation
- Story: As a user, I can compute indicators from raw trade/quote streams by aggregating into bars.
- Acceptance:
  - Provide a bar builder with configurable timeframe and session rules.
  - Emits complete bars and optional updated bars during the interval.
  - Handles out-of-order ticks with a clear, documented policy (drop or reorder buffer).
- Performance:
  - Sustains >= 500k ticks/sec on the benchmark machine with constant memory usage.
  - End-to-end p95 latency <= 5 ms for bar updates on a single timeframe.

US-18 Buffer pooling and ring buffers
- Story: As a developer, I can reuse memory for rolling windows and temp arrays.
- Acceptance:
  - Use ArrayPool<T> for scratch arrays in core math helpers.
  - Provide a pooled ring buffer for rolling windows.
  - No unbounded growth; pooled buffers are returned safely.
- Performance:
  - Allocation reduction >= 2x for large-window indicators vs baseline.

US-19 Intermediate series caching
- Story: As a user, I avoid recomputing derived series like HL2 and HLC3.
- Acceptance:
  - Provide an internal cache for common derived series (HL2, HLC3, OHLC4, TR).
  - Derived series can be reused across indicators within the same calculation.
  - Cache can be disabled to reduce memory if needed.
- Performance:
  - At least 20% time reduction in multi-indicator benchmarks that share inputs.

US-20 Output preallocation
- Story: As a developer, I can pre-size outputs to avoid List growth overhead.
- Acceptance:
  - Output lists are created with Capacity = input.Count or known size.
  - No dynamic growth in hot loops.
- Performance:
  - Long-series indicators show >= 5% time reduction vs baseline allocations.

US-21 Streaming configuration and defaults
- Story: As a user, I can use defaults or fully customize streaming behavior.
- Acceptance:
  - StreamingOptions supports nullable fields and industry-standard defaults.
  - Simple facade accepts just provider credentials, symbols, and timeframes.
  - Default timeframes: 1s, 5s, 1m, 5m, 15m, 1h, 1d, plus tick-level.
  - Updated bars are enabled by default.
  - Advanced config allows indicator selection, backpressure, and update policy.
- Performance:
  - Default path adds no extra per-tick allocations beyond required state.

US-22 Multi-timeframe fanout from ticks
- Story: As a user, I can compute indicators on multiple timeframes from ticks.
- Acceptance:
  - Tick/quote streams can feed multiple bar aggregators (configurable list).
  - Each timeframe has its own indicator state and outputs.
  - Supports updated bars and final bars per timeframe.
- Performance:
  - With 5 timeframes and 10 indicators, sustain >= 100k ticks/sec and p95 latency <= 20 ms.

US-23 Subscribe to all indicators
- Story: As a user, I can subscribe to all indicators or a filtered subset.
- Acceptance:
  - Provide RegisterAllIndicators(...) with optional filters (category/name/cost).
  - RegisterAllIndicators includes all indicators by default, with lazy loading.
  - Default remains explicit registration to avoid accidental heavy usage.
  - No per-tick reflection; indicators are created up front.
- Performance:
  - Registry initialization completes in < 1 second on the benchmark machine.

US-24 Compatibility shims
- Story: As a developer, I can avoid scattered conditional compilation in core code.
- Acceptance:
  - Centralize framework-specific logic in a Compatibility layer (e.g., SpanCompat).
  - Use per-TFM files or a single shim with #if, with no #if outside the Compatibility layer.
  - Core indicator code calls the shim APIs only.
- Performance:
  - net8 path overhead <= 2% vs direct CollectionsMarshal usage.

US-25 Vectorized math paths
- Story: As a developer, I can leverage SIMD/vectorization where it improves throughput.
- Acceptance:
  - Provide vectorized helpers for common operations (sum, diff, scale, clamp).
  - Guarded by runtime support (System.Numerics.Vector and/or hardware intrinsics).
  - Scalar fallback produces identical results.
- Performance:
  - For large arrays (>= 10k), vectorized path is >= 1.5x faster vs scalar baseline on supported hardware.
