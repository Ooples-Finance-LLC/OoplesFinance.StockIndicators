; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SI0001 | StreamingInput | Error | StreamingStateAnalyzer: a streaming state must be buildable with no arguments
SI0002 | StreamingInput | Error | StreamingStateAnalyzer: no public input-name or selector constructor parameters
SI0003 | StreamingInput | Error | StreamingStateAnalyzer: a non-close default input needs ICustomInputConsumer
SI0004 | StreamingInput | Error | StreamingStateAnalyzer: a resolved input must be read
