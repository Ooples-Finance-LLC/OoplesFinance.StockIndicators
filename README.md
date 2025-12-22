# OoplesFinance.StockIndicators (High-Precision Fork)

## [List of Available indicators](INDICATORS.md)

> "Approximation is the enemy of alpha. In finance, 3.14 is not Pi. It is an error."

This release marks the divergence of the **High-Precision Fork** from the original [OoplesFinance.StockIndicators](https://github.com/Ooples/OoplesFinance.StockIndicators).

## The Change Log

The original library treats floating-point precision with the sort of casual disdain usually reserved for airline safety demonstrations. It rounds inputs, intermediates, and outputs as if extra decimal places were a tax liability. It approximates constants (using `3.14` for PI), which is perfectly adequate for government work, but terrifying for finance. It treats trading numbers as a fun game, like Monopoly, but with real consequences.

I reject such violence. We chose rigor.

### 1. Rounding: Deleted

Every instance of `Math.Round` has been excised. If the market gives you data with 8 decimal places, and the indicator math produces 28, you get 28. We do not truncate your alpha.

### 2. Constants: Restored

Magic numbers are gone.

- PI is `Math.PI`, not `3.14`.
- Sqrt(2) is `Math.Sqrt(2)`, not `1.414`.

### 3. Logic: Preserved

We changed the physics, not the rules. The algorithms remain identical to the upstream source; they just run with the full fidelity of the .NET `double` type.

## Artifacts

This release includes the raw binaries for those who prefer to manage their own dependencies:

- **`.nupkg`**: The standard package.
- **`.dll`**: The assembly. For when you trust the file system more than NuGet.
- **`.pdb`**: The symbols. Step through the code and witness the absence of rounding errors yourself.

## Philosophy

This fork aligns with the architectural strictness of [QuanTAlib](https://github.com/mihakralj/QuanTAlib). In financial computing, approximation is an error.

If you are the sort of person who thinks `3.14` is close enough, you probably also think "ish" is a valid unit of measurement. We wish you the best of luck with your "money-ish".
