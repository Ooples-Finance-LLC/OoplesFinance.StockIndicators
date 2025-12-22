# v1.0.0: The "No Rounding" Edition

> "We fixed the math. You're welcome."

This release marks the divergence of the **High-Precision Fork** from the original [OoplesFinance.StockIndicators](https://github.com/Ooples/OoplesFinance.StockIndicators).

## The Change Log

The original library treats floating-point precision like a suggestion. It rounds inputs, intermediates, and outputs. It approximates constants (using `3.14` for PI).

We chose violence. We chose rigor.

### 1. Rounding: Deleted

Every instance of `Math.Round` has been excised. If the market gives you data with 8 decimal places, and the indicator math produces 28, you get 28. We do not truncate your alpha.

### 2. Constants: Restored

Magic numbers are gone.

- PI is `Math.PI`, not `3.14`.
- Sqrt(2) is `Math.Sqrt(2)`, not `1.414`.

### 3. Logic: Preserved

We changed the physics, not the rules. The algorithms remain identical to the upstream source; they just run with the full fidelity of the .NET `decimal` type.

## Artifacts

This release includes the raw binaries for those who prefer to manage their own dependencies:

- **`.nupkg`**: The standard package.
- **`.dll`**: The assembly. For when you trust the file system more than NuGet.
- **`.pdb`**: The symbols. Step through the code and witness the absence of rounding errors yourself.

## Philosophy

This fork aligns with the architectural strictness of [QuanTAlib](https://github.com/mihakralj/QuanTAlib). In financial computing, approximation is an error.

*Download the assets below to stop guessing and start calculating.*
