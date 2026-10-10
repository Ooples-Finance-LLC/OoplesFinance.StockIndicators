#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    internal static bool IsPointwise(IIndicator indicator) => indicator.Source is null && indicator.Components.Count == 0
        && indicator is PriceCircularTransform or PriceTranscendentalTransform or PriceRoundingTransform or CandleArithmetic;

    private static Bar FillPointwise(Bar[] source, double[][] output, IIndicator indicator,
        CancellationToken cancellation, Bar[]? owned) => indicator switch
    {
        CandleArithmetic arithmetic => FillArithmetic(source, output, arithmetic, cancellation, owned),
        PriceCircularTransform { Operation: PriceCircularOperation.ArcSine } => FillAsin(source, output, cancellation, owned),
        PriceCircularTransform { Operation: PriceCircularOperation.Sine } => FillPointwise<Sine, AllReal>(source, output, indicator, cancellation, owned),
        PriceCircularTransform { Operation: PriceCircularOperation.Cosine } => FillPointwise<Cosine, AllReal>(source, output, indicator, cancellation, owned),
        PriceCircularTransform { Operation: PriceCircularOperation.Tangent } => FillPointwise<Tangent, AllReal>(source, output, indicator, cancellation, owned),
        PriceCircularTransform { Operation: PriceCircularOperation.ArcCosine } => FillPointwise<ArcCosine, UnitInterval>(source, output, indicator, cancellation, owned),
        PriceCircularTransform { Operation: PriceCircularOperation.ArcTangent } => FillPointwise<ArcTangent, AllReal>(source, output, indicator, cancellation, owned),
        PriceTranscendentalTransform { Operation: PriceTranscendentalOperation.NaturalLogarithm } => FillPointwise<Logarithm, Positive>(source, output, indicator, cancellation, owned),
        PriceTranscendentalTransform { Operation: PriceTranscendentalOperation.CommonLogarithm } => FillPointwise<CommonLogarithm, Positive>(source, output, indicator, cancellation, owned),
        PriceTranscendentalTransform { Operation: PriceTranscendentalOperation.Exponential } => FillPointwise<Exponential, AllReal>(source, output, indicator, cancellation, owned),
        PriceTranscendentalTransform { Operation: PriceTranscendentalOperation.HyperbolicSine } => FillPointwise<HyperbolicSine, AllReal>(source, output, indicator, cancellation, owned),
        PriceTranscendentalTransform { Operation: PriceTranscendentalOperation.HyperbolicCosine } => FillPointwise<HyperbolicCosine, AllReal>(source, output, indicator, cancellation, owned),
        PriceTranscendentalTransform { Operation: PriceTranscendentalOperation.HyperbolicTangent } => FillPointwise<HyperbolicTangent, AllReal>(source, output, indicator, cancellation, owned),
        PriceRoundingTransform { Operation: PriceRoundingOperation.Ceiling } => FillPointwise<Ceiling, AllReal>(source, output, indicator, cancellation, owned, 65536),
        PriceRoundingTransform { Operation: PriceRoundingOperation.Floor } => FillPointwise<Floor, AllReal>(source, output, indicator, cancellation, owned, 65536),
        PriceRoundingTransform { Operation: PriceRoundingOperation.SquareRoot } => FillPointwise<SquareRoot, Nonnegative>(source, output, indicator, cancellation, owned, 65536),
        _ => throw new InvalidOperationException("Unqualified pointwise indicator.")
    };

    private interface IPointwiseMath
    {
        double Invoke(double value);
        bool CanOverflow { get; }
    }
    private interface IPointwiseDomain { bool Contains(double value); bool AlwaysDefined { get; } }
    private readonly struct AllReal : IPointwiseDomain { public bool Contains(double value) => true; public bool AlwaysDefined => true; }
    private readonly struct UnitInterval : IPointwiseDomain { public bool Contains(double value) => value is >= -1 and <= 1; public bool AlwaysDefined => false; }
    private readonly struct Positive : IPointwiseDomain { public bool Contains(double value) => value > 0; public bool AlwaysDefined => false; }
    private readonly struct Nonnegative : IPointwiseDomain { public bool Contains(double value) => value >= 0; public bool AlwaysDefined => false; }
    private readonly struct Sine : IPointwiseMath { public double Invoke(double value) => Math.Sin(value); public bool CanOverflow => false; }
    private readonly struct Cosine : IPointwiseMath { public double Invoke(double value) => Math.Cos(value); public bool CanOverflow => false; }
    private readonly struct Tangent : IPointwiseMath { public double Invoke(double value) => Math.Tan(value); public bool CanOverflow => false; }
    private readonly struct ArcCosine : IPointwiseMath { public double Invoke(double value) => Math.Acos(value); public bool CanOverflow => false; }
    private readonly struct ArcTangent : IPointwiseMath { public double Invoke(double value) => Math.Atan(value); public bool CanOverflow => false; }
    private readonly struct Logarithm : IPointwiseMath { public double Invoke(double value) => Math.Log(value); public bool CanOverflow => false; }
    private readonly struct CommonLogarithm : IPointwiseMath { public double Invoke(double value) => Math.Log10(value); public bool CanOverflow => false; }
    private readonly struct Exponential : IPointwiseMath { public double Invoke(double value) => Math.Exp(value); public bool CanOverflow => true; }
    private readonly struct HyperbolicSine : IPointwiseMath { public double Invoke(double value) => Math.Sinh(value); public bool CanOverflow => true; }
    private readonly struct HyperbolicCosine : IPointwiseMath { public double Invoke(double value) => Math.Cosh(value); public bool CanOverflow => true; }
    private readonly struct HyperbolicTangent : IPointwiseMath { public double Invoke(double value) => Math.Tanh(value); public bool CanOverflow => false; }
    private readonly struct Ceiling : IPointwiseMath { public double Invoke(double value) => Math.Ceiling(value); public bool CanOverflow => false; }
    private readonly struct Floor : IPointwiseMath { public double Invoke(double value) => Math.Floor(value); public bool CanOverflow => false; }
    private readonly struct SquareRoot : IPointwiseMath { public double Invoke(double value) => Math.Sqrt(value); public bool CanOverflow => false; }

    private struct PointwiseRegion
    {
        internal Bar Last;
        internal bool InvalidInput;
        internal int InvalidOutputIndex;
        internal double InvalidOutputValue;
        internal bool HasUndefined;
    }

    private interface IPointwiseKernel
    {
        double Invoke(in Bar bar, out bool defined);
        bool AlwaysDefined { get; }
        bool CanOverflow { get; }
    }

    private readonly struct UnaryKernel<TMath, TDomain> : IPointwiseKernel
        where TMath : struct, IPointwiseMath where TDomain : struct, IPointwiseDomain
    {
        public double Invoke(in Bar bar, out bool defined)
        {
            defined = default(TDomain).Contains(bar.Close);
            return defined ? default(TMath).Invoke(bar.Close) : 0;
        }
        public bool AlwaysDefined => default(TDomain).AlwaysDefined;
        public bool CanOverflow => default(TMath).CanOverflow;
    }

    private static Bar FillPointwise<TMath, TDomain>(Bar[] source, double[][] output, IIndicator indicator,
        CancellationToken cancellation, Bar[]? owned, int parallelMinimum = 8192)
        where TMath : struct, IPointwiseMath where TDomain : struct, IPointwiseDomain
        => FillPointwiseKernel(source, output, indicator, new UnaryKernel<TMath, TDomain>(), cancellation, owned, parallelMinimum);

    private static Bar FillPointwiseKernel<TKernel>(Bar[] source, double[][] output, IIndicator indicator,
        TKernel kernel, CancellationToken cancellation, Bar[]? owned, int parallelMinimum = 8192)
        where TKernel : struct, IPointwiseKernel
    {
        cancellation.ThrowIfCancellationRequested();
        ulong[]? missing = null;
        try
        {
            if (!kernel.AlwaysDefined && source.Length > 0)
            {
                int words = (int)(((long)source.Length + 63) / 64);
                missing = System.Buffers.ArrayPool<ulong>.Shared.Rent(words);
                missing.AsSpan(0, words).Clear();
            }
            if (!CanParallelize(source.Length, parallelMinimum) || !Monitor.TryEnter(ParallelBarGate))
            {
                var region = ComputePointwiseRegion(source, output[0], missing,
                    owned is null ? Span<Bar>.Empty : owned.AsSpan(), 0, kernel, cancellation);
                cancellation.ThrowIfCancellationRequested();
                ValidatePointwiseInput(in region);
                ValidatePointwiseOutput(indicator, in region);
                output[1] = CompletePointwisePresence(source.Length, missing, region.HasUndefined, cancellation);
                return region.Last;
            }
            try
            {
                int chunks = WorkerCount();
                var regions = new PointwiseRegion[chunks];
                AiDotNet.Tensors.Helpers.CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
                {
                    // Each worker owns complete bitmap words; no atomic OR or shared
                    // boundary word is needed, even for counts not divisible by 64.
                    int start = (int)((long)source.Length * chunk / chunks / 64) * 64;
                    int end = chunk == chunks - 1 ? source.Length : (int)((long)source.Length * (chunk + 1) / chunks / 64) * 64;
                    regions[chunk] = ComputePointwiseRegion(source.AsSpan(start, end - start),
                        output[0].AsSpan(start, end - start), missing,
                        owned is null ? Span<Bar>.Empty : owned.AsSpan(start, end - start), start, kernel, cancellation);
                });
                cancellation.ThrowIfCancellationRequested();
                // Raw input validation precedes every arithmetic failure, even when
                // that arithmetic overflow occurred in an earlier worker region.
                foreach (var region in regions) ValidatePointwiseInput(in region);
                foreach (var region in regions) ValidatePointwiseOutput(indicator, in region);
                output[1] = CompletePointwisePresence(source.Length, missing, regions.Any(r => r.HasUndefined), cancellation);
                return regions[chunks - 1].Last;
            }
            finally { Monitor.Exit(ParallelBarGate); }
        }
        finally { if (missing is not null) System.Buffers.ArrayPool<ulong>.Shared.Return(missing); }
    }

    private static PointwiseRegion ComputePointwiseRegion<TKernel>(ReadOnlySpan<Bar> source,
        Span<double> values, ulong[]? missing, Span<Bar> owned, int offset, TKernel kernel, CancellationToken cancellation)
        where TKernel : struct, IPointwiseKernel
    {
        var region = new PointwiseRegion { InvalidOutputIndex = -1 };
        Bar latest = default;
        for (int i = 0; i < source.Length; i++)
        {
            if (cancellation.IsCancellationRequested) break;
            latest = source[i];
            if (!AllFieldsFinite(in latest)) { region.InvalidInput = true; break; }
            double value = kernel.Invoke(in latest, out bool defined);
            values[i] = value;
            if (!kernel.AlwaysDefined && !defined)
            {
                int index = offset + i;
                missing![index >> 6] |= 1UL << (index & 63);
                region.HasUndefined = true;
            }
            if (!owned.IsEmpty) owned[i] = latest;
            if (kernel.CanOverflow && !double.IsFinite(value) && region.InvalidOutputIndex < 0)
            {
                region.InvalidOutputIndex = offset + i;
                region.InvalidOutputValue = value;
            }
        }
        region.Last = latest;
        return region;
    }

    // Published presence is immutable. One exact-length entry is retained, capped
    // at 8 MiB; replacing it never changes arrays retained by earlier runs. It is
    // not a pool lease and must never be written by an execution kernel.
    private static double[]? _allPresent;
    private static readonly object PresenceGate = new();
    internal static double[] CompletePointwisePresence(int count, ulong[]? missing, bool hasUndefined,
        CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (count == 0) return Array.Empty<double>();
        if (!hasUndefined)
        {
            var cached = Volatile.Read(ref _allPresent);
            if (cached?.Length == count) return cached;
            if (count <= 1_048_576)
            {
                lock (PresenceGate)
                {
                    cached = _allPresent;
                    if (cached?.Length == count) return cached;
                    cached = GC.AllocateUninitializedArray<double>(count);
                    cached.AsSpan().Fill(1);
                    cancellation.ThrowIfCancellationRequested();
                    Volatile.Write(ref _allPresent, cached);
                    return cached;
                }
            }
        }
        var presence = GC.AllocateUninitializedArray<double>(count);
        for (int i = 0; i < count; i++)
        {
            if ((i & 1023) == 0) cancellation.ThrowIfCancellationRequested();
            presence[i] = !hasUndefined || (missing![i >> 6] & (1UL << (i & 63))) == 0 ? 1 : 0;
        }
        cancellation.ThrowIfCancellationRequested();
        return presence;
    }

    private static void ValidatePointwiseInput(in PointwiseRegion region)
    {
        if (region.InvalidInput) IndicatorInputDomain.Finite.Validate(in region.Last);
    }
    private static void ValidatePointwiseOutput(IIndicator indicator, in PointwiseRegion region)
    {
        if (region.InvalidOutputIndex >= 0)
            IndicatorOutputPolicy.Validate(indicator, 0, region.InvalidOutputIndex, region.InvalidOutputValue);
    }
}
#endif
