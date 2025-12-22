# OoplesFinance.StockIndicators (High-Precision Fork)

> "Approximation is the enemy of alpha. In finance, 3.14 is not Pi. It is an error."

**Original Library:** [OoplesFinance.StockIndicators](https://github.com/Ooples/OoplesFinance.StockIndicators)

This library is a strict, high-precision fork of the original codebase.

## The Architecture of Rigor

The original library offers an impressive catalog of 700+ indicators. It also suffers from a catastrophic architectural decision: aggressive, pervasive rounding.

The upstream implementation rounds inputs. It rounds intermediate calculations. It rounds outputs. It truncates mathematical constants (using `3.14` for $\pi$). For visualization, this is acceptable. For algorithmic trading, backtesting, or any system where error propagation matters, it is disqualifying.

This fork exists to correct the math so it can be used for cross-validation of resuts in testing of [QuanTAlib](https://github.com/mihakralj/QuanTAlib) indicators.

### The Intervention

1. **Rounding Removal**: Every instance of `Math.Round` was excised. The code now respects the full precision of the `decimal` type.
2. **Constant Restoration**: Truncated literals were replaced with their full-precision `System.Math` equivalents. $\pi$ is now $\pi$, not a rough guess.
3. **Logic Preservation**: No algorithmic logic was altered. No features were added. No features were removed. This is the original code, stripped of its plague of rounding inaccuracies.

## Usage

The API surface remains identical to the original. If you know how to use Ooples, you know how to use this—you will just get different (correct) numbers.
