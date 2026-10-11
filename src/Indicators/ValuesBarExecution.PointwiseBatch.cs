#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    // One input validation/history pass; each bar flows through all requested
    // kernels directly into its final columns. No evaluator states or intermediate
    // price columns are retained. Generic steps specialize each scalar operation.
    private abstract class PointwiseStep : IDisposable
    {
        internal readonly Node Node;
        internal readonly ulong[]? Missing;
        protected PointwiseStep(Node node, bool alwaysDefined, int count)
        {
            Node = node;
            if (!alwaysDefined && count > 0)
            {
                int words = (int)(((long)count + 63) / 64);
                Missing = System.Buffers.ArrayPool<ulong>.Shared.Rent(words);
                Missing.AsSpan(0, words).Clear();
            }
        }
        internal abstract void Append(in Bar bar, int index, ref PointwiseRegion region);
        public void Dispose()
        {
            if (Missing is not null) System.Buffers.ArrayPool<ulong>.Shared.Return(Missing);
        }
    }

    private sealed class PointwiseStep<TKernel>(Node node, TKernel kernel, int count)
        : PointwiseStep(node, kernel.AlwaysDefined, count) where TKernel : struct, IPointwiseKernel
    {
        internal override void Append(in Bar bar, int index, ref PointwiseRegion region)
        {
            double value = kernel.Invoke(in bar, out bool defined);
            Node.Values[0][index] = value;
            if (!kernel.AlwaysDefined && !defined)
            {
                Missing![index >> 6] |= 1UL << (index & 63);
                region.HasUndefined = true;
            }
            if (kernel.CanOverflow && !double.IsFinite(value) && region.InvalidOutputIndex < 0)
            {
                region.InvalidOutputIndex = index;
                region.InvalidOutputValue = value;
            }
        }
    }

    private readonly struct ArcSine : IPointwiseMath
    {
        public double Invoke(double value) => Math.Asin(value);
        public bool CanOverflow => false;
    }

    private static PointwiseStep CreatePointwiseStep(Node node, int count)
    {
        PointwiseStep Step<TKernel>(TKernel kernel) where TKernel : struct, IPointwiseKernel =>
            new PointwiseStep<TKernel>(node, kernel, count);
        return node.Indicator switch
        {
            CandleArithmetic a => a.Operation switch
            {
                CandleArithmeticOperation.Add => Step(new BinaryKernel<Add>(a.Left, a.Right)),
                CandleArithmeticOperation.Subtract => Step(new BinaryKernel<Subtract>(a.Left, a.Right)),
                CandleArithmeticOperation.Multiply => Step(new BinaryKernel<Multiply>(a.Left, a.Right)),
                CandleArithmeticOperation.Divide => Step(new BinaryKernel<Divide>(a.Left, a.Right)),
                _ => throw new InvalidOperationException("Unqualified arithmetic operation.")
            },
            MedianPrice => Step(new MedianPriceKernel()),
            TypicalPrice => Step(new TypicalPriceKernel()),
            WeightedClose => Step(new WeightedCloseKernel()),
            FullTypicalPrice => Step(new FullTypicalPriceKernel()),
            DojiCandle c => Step(new DojiKernel(c.BodyFraction)),
            BullishCandle => Step(new CandlePolarityKernel(true)),
            BearishCandle => Step(new CandlePolarityKernel(false)),
            DragonflyDojiCandle c => Step(new ShadowDojiKernel(c.BodyFraction, c.ShadowFraction, true)),
            GravestoneDojiCandle c => Step(new ShadowDojiKernel(c.BodyFraction, c.ShadowFraction, false)),
            PriceCircularTransform c => c.Operation switch
            {
                PriceCircularOperation.ArcSine => Step(new UnaryKernel<ArcSine, UnitInterval>()),
                PriceCircularOperation.Sine => Step(new UnaryKernel<Sine, AllReal>()),
                PriceCircularOperation.Cosine => Step(new UnaryKernel<Cosine, AllReal>()),
                PriceCircularOperation.Tangent => Step(new UnaryKernel<Tangent, AllReal>()),
                PriceCircularOperation.ArcCosine => Step(new UnaryKernel<ArcCosine, UnitInterval>()),
                PriceCircularOperation.ArcTangent => Step(new UnaryKernel<ArcTangent, AllReal>()),
                _ => throw new InvalidOperationException("Unqualified circular operation.")
            },
            PriceTranscendentalTransform c => c.Operation switch
            {
                PriceTranscendentalOperation.NaturalLogarithm => Step(new UnaryKernel<Logarithm, Positive>()),
                PriceTranscendentalOperation.CommonLogarithm => Step(new UnaryKernel<CommonLogarithm, Positive>()),
                PriceTranscendentalOperation.Exponential => Step(new UnaryKernel<Exponential, AllReal>()),
                PriceTranscendentalOperation.HyperbolicSine => Step(new UnaryKernel<HyperbolicSine, AllReal>()),
                PriceTranscendentalOperation.HyperbolicCosine => Step(new UnaryKernel<HyperbolicCosine, AllReal>()),
                PriceTranscendentalOperation.HyperbolicTangent => Step(new UnaryKernel<HyperbolicTangent, AllReal>()),
                _ => throw new InvalidOperationException("Unqualified transcendental operation.")
            },
            PriceRoundingTransform c => c.Operation switch
            {
                PriceRoundingOperation.Ceiling => Step(new UnaryKernel<Ceiling, AllReal>()),
                PriceRoundingOperation.Floor => Step(new UnaryKernel<Floor, AllReal>()),
                PriceRoundingOperation.SquareRoot => Step(new UnaryKernel<SquareRoot, Nonnegative>()),
                _ => throw new InvalidOperationException("Unqualified rounding operation.")
            },
            _ => throw new InvalidOperationException("Unqualified pointwise batch indicator.")
        };
    }

    private static Bar FillPointwiseBatch(Bar[] source, List<Node> nodes, CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        var steps = new List<PointwiseStep>(nodes.Count);
        bool parallel = false;
        try
        {
            foreach (var node in nodes) steps.Add(CreatePointwiseStep(node, source.Length));
            var kernels = steps.ToArray();
            parallel = CanParallelize(source.Length, 8192) && Monitor.TryEnter(ParallelBarGate);
            int chunks = parallel ? WorkerCount() : 1;
            var inputs = new PointwiseRegion[chunks];
            var outputs = new PointwiseRegion[chunks][];
            for (int chunk = 0; chunk < chunks; chunk++)
            {
                outputs[chunk] = new PointwiseRegion[kernels.Length];
                for (int step = 0; step < kernels.Length; step++) outputs[chunk][step].InvalidOutputIndex = -1;
            }
            void Compute(int chunk)
            {
                // Complete bitmap words belong to exactly one worker.
                int start = (int)((long)source.Length * chunk / chunks / 64) * 64;
                int end = chunk == chunks - 1 ? source.Length : (int)((long)source.Length * (chunk + 1) / chunks / 64) * 64;
                ref var input = ref inputs[chunk];
                var results = outputs[chunk];
                for (int i = start; i < end; i++)
                {
                    if (cancellation.IsCancellationRequested) break;
                    var bar = source[i];
                    input.Last = bar;
                    if (!AllFieldsFinite(in bar)) { input.InvalidInput = true; break; }
                    if (owned is not null) owned[i] = bar;
                    for (int step = 0; step < kernels.Length; step++) kernels[step].Append(in bar, i, ref results[step]);
                }
            }
            if (parallel) AiDotNet.Tensors.Helpers.CpuParallelSettings.LightweightParallel(chunks, chunks, Compute);
            else Compute(0);
            cancellation.ThrowIfCancellationRequested();
            foreach (var input in inputs) ValidatePointwiseInput(in input);
            // Input failures precede every arithmetic failure. Output failures use
            // registration order, then the earliest index within that indicator.
            for (int step = 0; step < kernels.Length; step++)
            {
                bool missing = false;
                foreach (var region in outputs)
                {
                    ValidatePointwiseOutput(kernels[step].Node.Indicator, in region[step]);
                    missing |= region[step].HasUndefined;
                }
                var values = kernels[step].Node.Values;
                if (values.Length > 1)
                    values[1] = CompletePointwisePresence(source.Length, kernels[step].Missing, missing, cancellation);
            }
            cancellation.ThrowIfCancellationRequested();
            return inputs[chunks - 1].Last;
        }
        finally
        {
            if (parallel) Monitor.Exit(ParallelBarGate);
            foreach (var step in steps) step.Dispose();
        }
    }
}
#endif
