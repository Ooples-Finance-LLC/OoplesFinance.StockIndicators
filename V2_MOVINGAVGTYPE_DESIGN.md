# V2 MovingAvgType Infrastructure Design

This document captures the design decisions for implementing MovingAvgType support in the V2 fast path.

## Problem Statement

~156 indicators (~20% of 774 total) remain unimplemented in V2 fast path. The majority depend on `MovingAvgType` parameter which allows selection from 162+ MA variants. The v1 API is being completely replaced by v2, so all indicators must be converted.

## Design Decisions (2026-01-24)

### 1. SpecOptions Should Include MovingAvgType

**Decision**: Yes, include MovingAvgType as a parameter in SpecOptions classes.

**Example**:
```csharp
public sealed class RsiSpecOptions : IIndicatorSpecOptions
{
    public RsiSpecOptions(int length = 14, MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }
    public int Length { get; }
    public MovingAvgType MaType { get; }
}
```

### 2. Dispatch Mechanism: Lookup Table with Interface

**Decision**: Use Dictionary lookup with interface-based implementations for O(1) dispatch.

**Rationale**:
- O(1) dispatch regardless of 162+ MA types
- Modern, clean code - avoids massive switch statements
- Easy to extend - just add entries to dictionary
- Initialized once at startup (static readonly)

### 3. Interface-Based MA Core Implementations

**Decision**: Use `IMovingAverageCore` interface with readonly struct implementations.

**Benefits**:
- No allocation (structs on stack)
- Type-safe dispatch
- Clear contract for each MA type
- Supports varying signatures (single input, OHLC, volume)

### 4. Delegate to Existing Core Static Methods

**Decision**: Struct implementations should delegate to existing `MovingAverageCore.*` static methods.

**Rationale**:
- Reuses existing, tested code
- No duplication
- Thin wrapper pattern

### 5. File Organization

**Decision**: New folder `src/Core/Registry/` for registry infrastructure.

**Structure**:
```
src/Core/Registry/
├── IMovingAverageCore.cs      # Interface definition
├── MovingAverageRegistry.cs   # Dictionary registry
└── Implementations/           # Optional subfolder for struct implementations
    ├── SmaCore.cs
    ├── EmaCore.cs
    └── ...
```

### 6. Handling Extra Parameters (ALMA offset/sigma, etc.)

**Decision**: Use overloads with `ReadOnlySpan<double> extraParams`.

**Interface Methods**:
```csharp
// Standard computation
void Compute(ReadOnlySpan<double> input, Span<double> output, int length);

// With extra parameters
void Compute(ReadOnlySpan<double> input, Span<double> output, int length,
             ReadOnlySpan<double> extraParams);

// For OHLC MAs
void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low,
                 ReadOnlySpan<double> close, Span<double> output, int length);

// For volume-weighted MAs
void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume,
                       Span<double> output, int length);
```

### 7. Complex Dependencies

**Decision**: Either inline the dependency logic OR call other Core methods with rented buffers.

**Approach**:
- Prefer calling other Core methods when they exist
- Use `ArrayPool<double>.Shared` for temporary buffers
- Only inline if dependency doesn't exist as Core method

### 8. Multi-Output Indicators

**Decision**: Separate Core methods per output (already implemented).

**Example**:
- `BollingerBandsUpper()`
- `BollingerBandsMiddle()`
- `BollingerBandsLower()`

## Implementation Plan

1. Create `IMovingAverageCore` interface in `src/Core/Registry/`
2. Create `MovingAverageRegistry` with dictionary lookup
3. Implement struct wrappers for all 162 MA types
4. Update indicators with MovingAvgType to use registry
5. Add SpecOptions with MovingAvgType parameter
6. Wire dispatch routes

## MA Categories by Signature

### Single-Input MAs (~144 types)
Most common. Just need `(input, output, length)`.

### OHLC MAs (~11 types)
Need high, low, close data:
- McGinley Dynamic
- Variable Moving Average
- Etc.

### Volume-Weighted MAs (~7 types)
Need volume data:
- VWMA, VWAP
- Volume Adjusted MA
- Etc.

### MAs with Extra Parameters
- ALMA: offset (0.85), sigma (6)
- KAMA: fastLength, slowLength
- T3: volumeFactor
- Etc.

## Notes

- v1 API is being completely deprecated
- All 774 indicators must have v2 fast path
- This infrastructure enables the remaining ~156 indicators
