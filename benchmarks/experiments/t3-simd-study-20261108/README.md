Diagnostic study only. No production source is changed or qualified.

The scalar comparator is extracted verbatim from `CertifiedTillson.WordSmoother`
at `b2afb7edd`. A separate scalar-inline control adds only an inlining attribute.
Generated AVX2 and AVX512F kernels implement the same exact reciprocal quotient,
one-low correction, and nearest-even rounding. Four 32×32 products retain every
carry in each 64×64 high product. Periods remain restricted to 1–2047, with
positive same-binade normal inputs. Unsupported inputs retain the unchanged
production scalar/fallback routes; this isolated study does not implement them.

Independent BigInteger checks cover every supported period, endpoint and
midpoint cases, unrestricted high products, and six-stage pipeline outputs.
The pipeline staggers six EMA stages across input bars; padded lanes and five
drain ticks are included. It measures only smoothing, excluding T3 polynomial,
actual seed modes, validation, retained history, and public API work.

Windows and Linux CI use default runtime settings, 1,000 unmeasured warmups,
old/new/new/old rounds, ten measured iterations, and twelve quiet CPU samples
below 15%. All raw samples are retained. These timings cannot qualify source or
be imported into the competitor report. Production work requires separately
approved implementation and full affected-consumer verification.
