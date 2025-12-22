# OoplesFinance.StockIndicators (High-Precision Fork)

> "Approximation is the enemy of alpha. In finance, 3.14 is not Pi. It is an error."

This release marks the divergence of the **High-Precision Fork** from the original [OoplesFinance.StockIndicators](https://github.com/Ooples/OoplesFinance.StockIndicators).

## The Change Log

The original library treats floating-point precision with the sort of casual disdain usually reserved for airline safety demonstrations. It rounds inputs, intermediates, and outputs as if extra decimal places were a tax liability. It approximates constants (using `3.14` for PI), which is perfectly adequate for government work, but terrifying for finance. It treats trading numbers as a fun game, like Monopoly, but with real consequences.

I reject such violence. I chose rigor.

### 1. Rounding: Deleted

Every instance of `Math.Round` has been excised. If the market gives you data with 8 decimal places, and the indicator math produces 28, you get 28. I promise to never truncate your alpha.

### 2. Constants: Restored

Magic numbers are gone.

- PI is `Math.PI`, not `3.14`.
- Sqrt(2) is `Math.Sqrt(2)`, not `1.414`.

The API surface remains identical to the original. If you know how to use Ooples, you know how to use this—you will just get different (correct) numbers.
