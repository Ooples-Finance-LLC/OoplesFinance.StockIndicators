namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class ComparisonVerifier
{
    internal static readonly string[] Shapes = ["constant", "zero", "negative", "ramp", "descending", "alternating", "impulse", "walk"];
    internal static CompetitorData Fixture(string shape, int count)
    {
#pragma warning disable S2245 // Seeded price fixtures must replay exactly; this generator never creates secrets.
        var random = new Random(731);
#pragma warning restore S2245
        var value = 100d;
        var closes = Enumerable.Range(0, count).Select(index => shape switch
        {
            "constant" => 100d,
            "zero" => 0d,
            "negative" => -100d - index / 8d,
            "descending" => 100d - index / 8d,
            "ramp" => 100d + index / 8d,
            "alternating" => index % 2 == 0 ? 80d : 120d,
            "impulse" => index == count / 2 ? 200d : 100d,
            "walk" => value += random.Next(-8, 9) / 8d,
            _ => throw new ArgumentException("Unknown fixture", nameof(shape))
        }).ToArray();
        return CompetitorData.FromCloses(closes);
    }

    internal static CompetitorData BenchmarkFixture(ComparisonPair pair, int count) =>
        TaDirectionalComparison.Pairs.Any(p => p.Id == pair.Id) ? CompetitorData.Create(count) :
        SmoothedAccumulationComparison.Pairs.Any(p => p.Id == pair.Id) ? AccumulationDistributionComparison.BenchmarkFixture(count) :
        pair.Id == "TaLib.Functions.Accbands" ? CompetitorData.Create(count) :
        pair.Id == "Skender.GetIchimoku" ? CompetitorData.Create(count) :
        pair.Id == "Skender.GetStoch" ? CompetitorData.Create(count) :
        pair.Id is "TaLib.Functions.Stoch" or "TaLib.Functions.StochF" ? CompetitorData.Create(count) :
        pair.Id == "Trady.Indicator.IchimokuCloud" ? CompetitorData.Create(count) :
        pair.Id == "Trady.Indicator.KaufmanAdaptiveMovingAverage" ? CompetitorData.Create(count) :
        pair.Id == "Skender.GetMama" ? CompetitorData.Create(count) :
        pair.Id == "Skender.GetPvo" ? CompetitorData.Create(count) :
        pair.Id == "Skender.GetPrs" ? CompetitorData.Create(count) :
        pair.Id == "Skender.GetAdx" ? CompetitorData.Create(count) :
        DirectionalComparison.Pairs.Any(p => p.Id == pair.Id) ? CompetitorData.Create(count) :
        TrueRangeRatioComparison.Pairs.Any(p => p.Id == pair.Id) ? CompetitorData.Create(count) :
        AtrEnvelopeComparison.Pairs.Any(p => p.Id == pair.Id) ? CompetitorData.Create(count) :
        ChandelierComparison.Pairs.Any(p => p.Id == pair.Id) ? CompetitorData.Create(count) :
        MoneyFlowIndexComparison.Pairs.Any(p => p.Id == pair.Id) ? CompetitorData.Create(count) :
        pair.Id == "Skender.GetChop" ? CompetitorData.Create(count) :
        CommodityChannelComparison.Pairs.Any(p => p.Id == pair.Id) ? CompetitorData.Create(count) :
        ReturnBetaComparison.Pairs.Any(p => p.Id == pair.Id) ? ReturnBetaComparison.Fixture(count) :
        PairStatisticsComparison.Pairs.Any(p => p.Id == pair.Id) ? PairStatisticsComparison.Fixture(count) :
        SampleShapeComparison.Pairs.Any(p => p.Id == pair.Id) ? SampleShapeComparison.Fixture(count) :
        pair.Id is "Skender.GetElderRay" or "Skender.GetHeikinAshi" ? CompetitorData.Create(count) :
        TranscendentalComparison.Pairs.Any(p => p.Id == pair.Id) ? TranscendentalComparison.Fixture(pair.Id.Split('.')[^1], "walk", count) :
        CircularComparison.Pairs.Any(p => p.Id == pair.Id) ? CircularComparison.Fixture(pair.Id.Split('.')[^1], "walk", count) :
        ElementaryMathComparison.Pairs.Any(p => p.Id == pair.Id) ? ElementaryMathComparison.Fixture("walk", count, pair.Id.EndsWith("Sqrt", StringComparison.Ordinal)) :
        (pair.Id is "QuanTAlib.Atr" or "TaLib.Functions.Natr" || SeededAtrComparison.Pairs.Any(p => p.Id == pair.Id)) ? CompetitorData.Create(count) :
        AccumulationDistributionComparison.Pairs.Any(p => p.Id == pair.Id) ? AccumulationDistributionComparison.BenchmarkFixture(count) :
        (VolumePriceComparison.Pairs.Any(p => p.Id == pair.Id) || pair.Id == "Skender.GetCmf") ? AccumulationDistributionComparison.BenchmarkFixture(count) :
        VolumeRecurrenceComparison.Ids.Contains(pair.Id) ? AccumulationDistributionComparison.BenchmarkFixture(count) :
        pair.IsCandle || BalanceOfPowerComparison.Pairs.Any(p => p.Id == pair.Id) || pair.Id == "TaLib.Functions.MidPrice" || PriceComparison.Pairs.Any(p => p.Id == pair.Id) || TradyExtremaComparison.Pairs.Any(p => p.Id == pair.Id) || ObvComparison.Pairs.Any(p => p.Id == pair.Id)
            ? CompetitorData.Create(count) : Fixture("walk", count);

    internal static int Verify(ComparisonPair pair)
    {
        // Exercise the exact datasets used by both timed arms as well as the small
        // adversarial fixtures, so a benchmark cannot introduce an unverified workload.
        var comparisons = Check(pair, BenchmarkFixture(pair, 1_000), 20) +
            Check(pair, BenchmarkFixture(pair, 10_000), 20);
        if(pair.Id=="TaLib.Functions.HtTrendline")
        {
            foreach(var suppression in new[]{0,1,7,30})
            {
                using var settings=new DelayedHilbertTrendComparison.Settings(suppression);
                var configured=DelayedHilbertTrendComparison.Pair(suppression);
                foreach(var count in new[]{0,2,37,62+suppression,63+suppression,64+suppression,150,250})
                foreach(var shape in Shapes)comparisons+=Check(configured,Fixture(shape,count),20);
                comparisons+=Check(configured,CompetitorData.Create(250),20);
            }
            return comparisons;
        }
        if(pair.Id is "TaLib.Functions.HtDcPeriod" or "TaLib.Functions.HtPhasor")
        {
            var periodOnly=pair.Id.EndsWith("HtDcPeriod",StringComparison.Ordinal);
            foreach(var suppression in new[]{0,1,7,30})
            {
                using var settings=new HilbertCycleComparison.Settings(periodOnly,suppression);
                var configured=HilbertCycleComparison.Pair(periodOnly,suppression);
                foreach(var count in new[]{0,2,12,31+suppression,32+suppression,33+suppression,110,200})
                foreach(var shape in Shapes)comparisons+=Check(configured,Fixture(shape,count),20);
                comparisons+=Check(configured,CompetitorData.Create(200),20);
            }
            return comparisons;
        }
        if(pair.Id is "TaLib.Functions.Stoch" or "TaLib.Functions.StochF")
        {
            var fast=pair.Id.EndsWith("StochF",StringComparison.Ordinal);
            var methods=Enum.GetValues<OoplesFinance.StockIndicators.Indicators.ClassicAverageMethod>();
            foreach(var first in new[]{false,true})foreach(var suppression in new[]{0,2})
            {
                using var settings=new ClassicAverageComparison.Settings(first,suppression);
                foreach(var km in (fast ? methods.Take(1) : methods))foreach(var dm in methods)
                    comparisons+=Check(ClassicStochasticComparison.Pair(fast,5,3,2,km,dm,first,suppression),CompetitorData.Create(120),20);
                foreach(var method in methods)foreach(var periods in new[]{(1,1,1),(2,1,3),(3,2,1),(5,3,3)})
                {
                    var configured=ClassicStochasticComparison.Pair(fast,periods.Item1,periods.Item2,periods.Item3,method,method,first,suppression);
                    foreach(var shape in Shapes)comparisons+=Check(configured,Fixture(shape,100),20);
                    comparisons+=Check(configured,Fixture("walk",0),20);
                }
            }
            return comparisons;
        }
        if(pair.Id=="TaLib.Functions.MacdExt")
        {
            var methods=Enum.GetValues<OoplesFinance.StockIndicators.Indicators.ClassicAverageMethod>();
            using(var settings=new ClassicAverageComparison.Settings(false,0))
            foreach(var fm in methods)foreach(var sm in methods)foreach(var dm in methods)
                comparisons+=Check(ClassicMacdComparison.Pair(3,5,3,fm,sm,dm),CompetitorData.Create(90),20);
            foreach(var method in methods)foreach(var first in new[]{false,true})foreach(var suppression in new[]{0,2})
            {
                using var settings=new ClassicAverageComparison.Settings(first,suppression);
                foreach(var periods in new[]{(2,3,2),(3,2,2),(3,3,1),(5,10,3)})
                {
                    var configured=ClassicMacdComparison.Pair(periods.Item1,periods.Item2,periods.Item3,method,method,method,first,suppression);
                    foreach(var shape in Shapes)comparisons+=Check(configured,Fixture(shape,130),20);
                    comparisons+=Check(configured,Fixture("walk",0),20);
                }
            }
            return comparisons;
        }
        if(pair.Id=="Skender.GetStoch")
        {
            foreach(var wilder in new[]{false,true})foreach(var periods in new[]{(1,1,1),(2,1,3),(3,2,1),(5,3,3),(14,5,3)})
            foreach(var factors in new[]{(3d,2d),(.5,1.5),(1d,1d)})
            {
                var configured=WindowStochasticComparison.Pair(periods.Item1,periods.Item2,periods.Item3,wilder,factors.Item1,factors.Item2);
                foreach(var count in new[]{0,1,periods.Item1-1,periods.Item1,periods.Item1+periods.Item2+periods.Item3,80}.Distinct().Where(n=>n>=configured.MinimumInputCount))
                foreach(var shape in Shapes)comparisons+=Check(configured,Fixture(shape,count),20);
                comparisons+=Check(configured,CompetitorData.Create(100),20);
            }
            return comparisons;
        }
        if(pair.Id=="TaLib.Functions.StochRsi")
        {
            foreach(var method in Enum.GetValues<OoplesFinance.StockIndicators.Indicators.ClassicAverageMethod>())
            foreach(var first in new[]{false,true})foreach(var suppression in new[]{(0,0),(1,2),(3,0)})
            {
                using var settings=new ClassicStochasticRsiComparison.Settings(first,suppression.Item1,suppression.Item2);
                foreach(var periods in new[]{(2,1,1),(3,2,2),(14,5,3),(5,3,10)})
                {
                    var configured=ClassicStochasticRsiComparison.Pair(periods.Item1,periods.Item2,periods.Item3,method,first,suppression.Item1,suppression.Item2);
                    var boundary=periods.Item1+suppression.Item1+periods.Item2-1+(int)ClassicAverageComparison.First(periods.Item3,method,suppression.Item2);
                    foreach(var count in new[]{0,boundary+2,boundary+6,boundary+50})
                    foreach(var shape in Shapes)comparisons+=Check(configured,Fixture(shape,count),20);
                    comparisons+=Check(configured,CompetitorData.Create(boundary+80),20);
                }
            }
            return comparisons;
        }
        if(pair.Id=="TaLib.Functions.Mavp")
        {
            foreach(var method in Enum.GetValues<OoplesFinance.StockIndicators.Indicators.ClassicAverageMethod>())
            foreach(var first in new[]{false,true})foreach(var suppression in new[]{0,2})
            {
                using var settings=new ClassicAverageComparison.Settings(first,suppression);
                foreach(var limits in new[]{(2,2),(2,5),(3,10),(2,30)})
                {
                    var configured=VariablePeriodComparison.Pair(limits.Item1,limits.Item2,method,first,suppression);
                    var boundary=configured.MinimumInputCount;
                    foreach(var count in new[]{boundary,boundary+1,boundary+50}.Distinct())
                    foreach(var shape in Shapes)comparisons+=Check(configured,Fixture(shape,count),20);
                    comparisons+=Check(configured,CompetitorData.Create(boundary+80),20);
                }
            }
            return comparisons;
        }
        if(pair.Id=="TaLib.Functions.Bbands")
        {
            foreach(var method in Enum.GetValues<OoplesFinance.StockIndicators.Indicators.ClassicAverageMethod>())
            foreach(var first in new[]{false,true})foreach(var suppression in new[]{0,2})
            {
                using var settings=new ClassicAverageComparison.Settings(first,suppression);
                foreach(var factors in new[]{(0d,0d),(1d,1d),(1d,2d),(2d,1d),(.5,3d)})
                foreach(var p in new[]{2,3,10,40})
                {
                    var configured=ClassicBandsComparison.Pair(method,factors.Item1,factors.Item2,first,suppression);
                    foreach(var count in new[]{0,2,p-1,p,p+1,100}.Distinct().Where(n=>n!=1))
                    foreach(var shape in Shapes)comparisons+=Check(configured,Fixture(shape,count),p);
                    comparisons+=Check(configured,CompetitorData.Create(120),p);
                }
            }
            return comparisons;
        }
        if(ClassicOscillatorComparison.Pairs.Any(p=>p.Id==pair.Id))
        {
            foreach(var method in Enum.GetValues<OoplesFinance.StockIndicators.Indicators.ClassicAverageMethod>())
            foreach(var first in new[]{false,true})foreach(var suppression in new[]{0,2})
            {
                using var settings=new ClassicAverageComparison.Settings(first,suppression);
                foreach(var periods in new[]{(2,3),(3,2),(3,3),(5,10),(12,26)})
                {
                    var configured=ClassicOscillatorComparison.Pair(pair.Id.EndsWith("Ppo",StringComparison.Ordinal),periods.Item1,periods.Item2,method,first,suppression);
                    foreach(var count in new[]{0,2,3,20,60,180})foreach(var shape in Shapes)comparisons+=Check(configured,Fixture(shape,count),20);
                    comparisons+=Check(configured,CompetitorData.Create(180),20);
                }
            }
            return comparisons;
        }
        if(pair.Id=="TaLib.Functions.Ma")
        {
            foreach(var method in Enum.GetValues<OoplesFinance.StockIndicators.Indicators.ClassicAverageMethod>())
            foreach(var first in new[]{false,true})foreach(var suppression in new[]{0,2})
            {
                using var settings=new ClassicAverageComparison.Settings(first,suppression);
                var configured=ClassicAverageComparison.Pair(method,first,suppression);
                foreach(var p in new[]{1,2,3,10})
                {
                    foreach(var count in new[]{0,2,p-1,p,p+1,65}.Distinct().Where(n=>n!=1))
                    foreach(var shape in Shapes)comparisons+=Check(configured,Fixture(shape,count),p);
                    comparisons+=Check(configured,CompetitorData.Create(90),p);
                }
            }
            return comparisons;
        }
        if(DeviationBandsComparison.Ids.Contains(pair.Id))
        {
            var skender=pair.Id==DeviationBandsComparison.Ids[0];
            foreach(var p in (skender ? new[]{2,3,5,20} : new[]{1,2,3,5,20}))
            foreach(var factor in (skender ? new[]{.1,1d,2d,3.5} : new[]{-2d,0d,.1,2d}))
            {
                var configured=DeviationBandsComparison.Pair(pair.Id,factor);
                foreach(var count in new[]{0,1,p-1,p,p+1,60}.Distinct())
                foreach(var shape in Shapes)comparisons+=Check(configured,Fixture(shape,count),p);
                comparisons+=Check(configured,CompetitorData.Create(65),p);
                comparisons+=Check(configured,SkenderDeviationComparison.FlatRoundingFixture(),p);
                comparisons+=Check(configured,SkenderDeviationComparison.CollapsedQuoteFixture(),p);
            }
            return comparisons;
        }
        if(pair.Id=="QuanTAlib.Vidya")
        {
            foreach(var c in new[]{(1,4,.2),(2,8,.2),(5,3,.5),(5,20,0d),(3,12,-.1),(20,0,.2)})
            {
                var configured=DeviationRatioComparison.Pair(c.Item2,c.Item3);
                foreach(var count in new[]{0,1,3,8,20,81,100})
                foreach(var shape in Shapes)comparisons+=Check(configured,Fixture(shape,count),c.Item1);
                comparisons+=Check(configured,CompetitorData.Create(100),c.Item1);
            }
            return comparisons;
        }
        if(pair.Id=="TaLib.Functions.Mama")
        {
            foreach(var suppression in new[]{0,2})
            {
                using var settings=new DelayedPhaseComparison.Settings(suppression);
                foreach(var c in new[]{(.5,.05),(.8,.1),(.05,.5),(.01,.99),(.99,.01)})
                {
                    var configured=DelayedPhaseComparison.Pair(c.Item1,c.Item2,suppression);
                    foreach(var count in new[]{0,2,12,31,32,33,34,35,100})
                    foreach(var shape in Shapes)comparisons+=Check(configured,Fixture(shape,count),20);
                    comparisons+=Check(configured,CompetitorData.Create(100),20);
                }
            }
            return comparisons;
        }
        if(SeededPhaseComparison.Pairs.Any(p=>p.Id==pair.Id))
        {
            var quan=pair.Id=="QuanTAlib.Mama";
            foreach(var c in new[]{(.5,.05),(.8,.1),(.2,.01)})
            {
                var configured=SeededPhaseComparison.Pair(quan,c.Item1,c.Item2);
                foreach(var count in new[]{0,1,5,6,7,12,20,80})
                foreach(var shape in Shapes)comparisons+=Check(configured,Fixture(shape,count),20);
                comparisons+=Check(configured,CompetitorData.Create(100),20);
            }
            return comparisons;
        }
        if(pair.Id=="TaLib.Functions.Kama")
        {
            foreach(var suppression in new[]{0,2})
            {
                using var settings=new TaAdaptiveComparison.Settings(suppression);
                var configured=TaAdaptiveComparison.Pair(suppression);
                foreach(var p in suppression==0?new[]{2,3,10,30,101,int.MaxValue}:new[]{2,3,10,30,101})
                {
                    foreach(var count in new[]{0,2,3,10,30,100})
                    foreach(var shape in Shapes)comparisons+=Check(configured,Fixture(shape,count),p);
                    comparisons+=Check(configured,CompetitorData.Create(100),p);
                }
            }
            return comparisons;
        }
        if(pair.Id=="Trady.Indicator.KaufmanAdaptiveMovingAverage")
        {
            foreach(var c in new[]{(1,2,30),(2,2,30),(5,2,9),(10,3,30),(20,2,30)})
            {
                var configured=TradyAdaptiveComparison.Pair(c.Item2,c.Item3);
                foreach(var count in new[]{0,1,c.Item1-1,c.Item1,c.Item1+1,100}.Distinct())
                foreach(var shape in Shapes)comparisons+=TradyAdaptiveComparison.Check(configured,Fixture(shape,count),c.Item1);
                comparisons+=TradyAdaptiveComparison.Check(configured,CompetitorData.Create(100),c.Item1);
            }
            return comparisons;
        }
        if (
            pair.Id
            is "TaLib.Functions.HtDcPhase"
                or "TaLib.Functions.HtSine"
                or "TaLib.Functions.HtTrendMode"
        )
        {
            var kind =
                pair.Id.EndsWith("HtDcPhase", StringComparison.Ordinal) ? 0
                : pair.Id.EndsWith("HtSine", StringComparison.Ordinal) ? 1
                : 2;
            foreach (var suppression in new[] { 0, 1, 7, 30 })
            {
                using var settings = new HilbertCycleSignalComparison.Settings(kind, suppression);
                var configured = HilbertCycleSignalComparison.Pair(kind, suppression);
                foreach (
                    var count in new[]
                    {
                        0,
                        2,
                        37,
                        62 + suppression,
                        63 + suppression,
                        64 + suppression,
                        150,
                        250,
                    }
                )
                foreach (var shape in Shapes)
                    comparisons += Check(configured, Fixture(shape, count), 20);
                comparisons += Check(configured, CompetitorData.Create(250), 20);
            }
            return comparisons;
        }
        if (pair.Id == "Skender.GetZigZag")
        {
            foreach(var highLow in new[]{false,true})
            foreach(var percent in new[]{.5,5d,20d})
            foreach(var count in new[]{0,1,2,10,80})
            {
                var configured=ZigZagComparison.Pair(percent,highLow);
                foreach(var shape in Shapes) comparisons+=Check(configured,Fixture(shape,count),20);
                comparisons+=Check(configured,CompetitorData.Create(count),20);
            }
            return comparisons;
        }
        if (pair.Id is "Skender.GetRenko" or "Skender.GetRenkoAtr")
        {
            var atr=pair.Id.EndsWith("Atr",StringComparison.Ordinal);
            foreach(var highLow in new[]{false,true})
            foreach(var size in new[]{.5,2d})
            foreach(var count in new[]{0,1,2,10,50,80})
            {
                var configured=RenkoComparison.Pair(atr,size,highLow);
                foreach(var shape in Shapes)comparisons+=Check(configured,Fixture(shape,count),5);
                comparisons+=Check(configured,CompetitorData.Create(count),5);
            }
            return comparisons;
        }
        if (pair.Id == "QuanTAlib.Jma")
        {
            foreach(var period in new[]{1,2,5,20})
            foreach(var phase in new[]{-200d,0d,200d})
            foreach(var shortPeriod in new[]{1,4,10})
            {
                var configured=JurikComparison.Pair(phase,shortPeriod);
                foreach(var shape in Shapes)comparisons+=Check(configured,Fixture(shape,80),period);
            }
            return comparisons;
        }
        if (pair.Id == "Skender.GetHurst")
        {
            foreach (var period in new[] { 20, 21, 32, 100, 340, 341 })
            foreach (var shape in Shapes)
                comparisons += Check(pair, Fixture(shape, period + 30), period);
            return comparisons;
        }
        if (pair.Id == DynamicMomentumComparison.Id)
        {
            foreach (var parameters in new[] { (5, 10, 14, 30, 5), (2, 3, 4, 8, 1), (1, 1, 1, 1, 1), (7, 2, 3, 12, 2) })
            foreach (var count in new[] { 0, 1, 10, 50, 120 })
            {
                var (sd, smooth, rsi, upper, lower) = parameters;
                var configured = DynamicMomentumComparison.Pair(sd, smooth, rsi, upper, lower);
                foreach (var shape in Shapes) comparisons += Check(configured, Fixture(shape, count), 20);
                comparisons += Check(configured, CompetitorData.Create(count), 20);
            }
            return comparisons;
        }
        if (ParabolicComparison.Ids.Contains(pair.Id))
        {
            var kind = Array.IndexOf(ParabolicComparison.Ids, pair.Id);
            foreach (var parameters in new[] { new SarParameters(), new SarParameters(.04, .4, .1, 0, .01, .03, .05, .3) })
            foreach (var count in new[] { 2, 3, 4, 5, 6, 50, 250 })
            {
                var configured = ParabolicComparison.Pair(kind, parameters);
                foreach (var shape in Shapes) comparisons += Check(configured, Fixture(shape, count), 20);
                comparisons += Check(configured, CompetitorData.Create(count), 20);
            }
            return comparisons;
        }
        if (pair.Id == "Skender.GetKvo")
        {
            foreach (var (fast, slow, signal) in new[] { (3, 4, 1), (3, 5, 2), (34, 55, 13) })
            foreach (var count in new[] { 0, 1, 2, slow + 1, slow + signal, slow + signal + 1, 200 })
            {
                var configured = KlingerComparison.Pair(fast, slow, signal);
                foreach (var shape in Shapes) comparisons += Check(configured, Fixture(shape, count), 20);
                comparisons += Check(configured, CompetitorData.Create(count), 20);
            }
            return comparisons;
        }
        if (pair.Id == "Skender.GetPivots")
        {
            foreach (var (left, right, max) in new[] { (2, 2, 3), (2, 3, 20), (5, 2, 50) })
            foreach (var close in new[] { false, true })
            foreach (var count in new[] { 0, 1, left + right, left + right + 1, 150 })
            {
                var configured = PivotTrendComparison.Pair(left, right, max, close);
                foreach (var shape in Shapes) comparisons += Check(configured, Fixture(shape, count), 20);
                comparisons += Check(configured, CompetitorData.Create(count), 20);
            }
            return comparisons;
        }
        if (pair.Id is "QuanTAlib.Fwma" or "QuanTAlib.Afirma")
        {
            var fib = pair.Id == "QuanTAlib.Fwma";
            foreach (var period in new[] { 2, 3, 14, 55 })
            foreach (var taps in (fib ? new[] { 21 } : new[] { 3, 8, 21 }))
            foreach (var window in (fib ? new[] { OoplesFinance.StockIndicators.Indicators.SincWindow.Hann } : Enum.GetValues<OoplesFinance.StockIndicators.Indicators.SincWindow>()))
            foreach (var count in new[] { 0, 1, 2, 3, 22, 100 })
            {
                var configured = FixedKernelSnapshotComparison.Pair(fib, taps, window);
                foreach (var shape in Shapes) comparisons += Check(configured, Fixture(shape, count), period);
                comparisons += Check(configured, CompetitorData.Create(count), period);
            }
            return comparisons;
        }
        if (pair.Id == "QuanTAlib.Maaf")
        {
            foreach (var period in new[] { 1, 2, 5, 14, 39 })
            foreach (var threshold in new[] { -.1, .002, .2 })
            foreach (var count in new[] { 0, 1, 3, period + 2, period + 3, 100 })
            {
                var configured = MedianAdaptiveComparison.Pair(threshold);
                foreach (var shape in Shapes) comparisons += Check(configured, Fixture(shape, count), period);
                comparisons += Check(configured, CompetitorData.Create(count), period);
            }
            return comparisons;
        }
        if (pair.Id == "QuanTAlib.Rvi")
        {
            foreach (var period in new[] { 2, 3, 14, 20 })
            foreach (var count in new[] { 0, 1, period - 1, period, period + 1, 100 })
            {
                foreach (var shape in Shapes) comparisons += Check(pair, Fixture(shape, count), period);
                comparisons += Check(pair, CompetitorData.Create(count), period);
            }
            return comparisons;
        }
        if (pair.Id == "QuanTAlib.Htit")
        {
            foreach (var count in new[] { 0, 1, 5, 6, 7, 10, 11, 12, 40, 150, 250 })
            {
                foreach (var shape in Shapes) comparisons += Check(pair, Fixture(shape, count), 20);
                comparisons += Check(pair, CompetitorData.Create(count), 20);
            }
            return comparisons;
        }
        if (pair.Id == "Skender.GetConnorsRsi")
        {
            foreach (var (rsi, streak, rank) in new[] { (2, 2, 2), (3, 2, 100), (5, 7, 3), (2, 3, 20) })
            foreach (var count in new[] { 0, 1, Math.Max(rsi, Math.Max(streak, rank)), Math.Max(rsi, Math.Max(streak, rank)) + 1, Math.Max(rsi, Math.Max(streak, rank)) + 3, 250 })
            {
                var configured = ConnorsComparison.Pair(rsi, streak, rank);
                foreach (var shape in Shapes) comparisons += Check(configured, Fixture(shape, count), 20);
                comparisons += Check(configured, CompetitorData.Create(count), 20);
            }
            return comparisons;
        }
        if (pair.Id == "Skender.GetStc")
        {
            foreach (var (cycle, fast, slow) in new[] { (1, 1, 2), (3, 2, 5), (10, 23, 50), (20, 5, 30) })
            foreach (var count in new[] { 0, 1, slow - 1, slow + cycle - 2, slow + cycle, slow + cycle + 1, 250 })
            {
                var configured = SchaffCycleComparison.Pair(cycle, fast, slow);
                foreach (var shape in Shapes) comparisons += Check(configured, Fixture(shape, count), 20);
                comparisons += Check(configured, CompetitorData.Create(count), 20);
            }
            return comparisons;
        }
        if (pair.Id is "Skender.GetRollingPivots" or "Skender.GetPivotPoints")
        {
            var calendar = pair.Id == "Skender.GetPivotPoints";
            foreach (var style in Enum.GetValues<OoplesFinance.StockIndicators.Indicators.PivotLevelStyle>())
            foreach (var window in (calendar ? Enum.GetValues<OoplesFinance.StockIndicators.Indicators.PivotCalendarWindow>() : new[] { OoplesFinance.StockIndicators.Indicators.PivotCalendarWindow.Hour }))
            foreach (var period in (calendar ? new[] { 20 } : new[] { 1, 3, 20 }))
            foreach (var offset in (calendar ? new[] { 0 } : new[] { 0, 2, 20 }))
            foreach (var count in new[] { 0, 1, period + offset, period + offset + 1, 150 })
            {
                var configured = PivotLevelComparison.Pair(calendar, style, offset, window);
                foreach (var shape in Shapes) comparisons += Check(configured, Fixture(shape, count), period);
                comparisons += Check(configured, CompetitorData.Create(count), period);
            }
            return comparisons;
        }
        if (pair.Id == "Skender.GetStdDevChannels")
        {
            foreach (var whole in new[] { false, true })
            foreach (var period in new[] { 2, 3, 7, 20 })
            foreach (var deviations in new[] { .5, 2, 3d })
            foreach (var count in new[] { 0, 1, period - 1, period, period + 1, 2 * period + 1, 150 })
            {
                var configured = RegressionChannelComparison.Pair(whole, deviations);
                if (count < configured.MinimumInputCount) continue;
                foreach (var shape in Shapes) comparisons += Check(configured, Fixture(shape, count), period);
                comparisons += Check(configured, CompetitorData.Create(count), period);
            }
            return comparisons;
        }
        if (pair.Id == "Skender.GetVolatilityStop")
        {
            foreach (var period in new[] { 2, 3, 7, 20 })
            foreach (var multiplier in new[] { .5, 1, 3d })
            foreach (var count in new[] { 0, 1, period - 1, period, period + 1, period + 2, 150 })
            {
                var configured = VolatilityStopComparison.Pair(multiplier);
                foreach (var shape in Shapes) comparisons += Check(configured, Fixture(shape, count), period);
                comparisons += Check(configured, CompetitorData.Create(count), period);
            }
            return comparisons;
        }
        if (pair.Id == "Skender.GetFisherTransform")
        {
            foreach (var midpoint in new[] { false, true })
            foreach (var period in new[] { 1, 2, 3, 20, 51 })
            foreach (var count in new[] { 0, 1, period - 1, period, period + 1, 150 })
            {
                var configured = WindowFisherComparison.Pair(midpoint);
                foreach (var shape in Shapes) comparisons += Check(configured, Fixture(shape, count), period);
                comparisons += Check(configured, CompetitorData.Create(count), period);
            }
            return comparisons;
        }
        if (pair.Id is "Skender.GetAtrStop" or "Skender.GetSuperTrend")
        {
            var bases = pair.Id == "Skender.GetSuperTrend"
                ? new[] { OoplesFinance.StockIndicators.Indicators.AtrTrailBasis.Midpoint }
                : new[] { OoplesFinance.StockIndicators.Indicators.AtrTrailBasis.Close, OoplesFinance.StockIndicators.Indicators.AtrTrailBasis.HighLow };
            foreach (var basis in bases)
            foreach (var period in new[] { 2, 3, 7, 20 })
            foreach (var multiplier in new[] { .5, 1, 3d })
            foreach (var count in new[] { 0, 1, period - 1, period, period + 1, 150 })
            {
                var configured = AtrTrailingComparison.Pair(basis, multiplier);
                foreach (var shape in Shapes)
                    comparisons += Check(configured, Fixture(shape, count), period);
                comparisons += Check(configured, CompetitorData.Create(count), period);
            }
            return comparisons;
        }
        if (pair.Id is "QuanTAlib.Frama" or "QuanTAlib.Dsma")
        {
            var fractal = pair.Id == "QuanTAlib.Frama";
            foreach (var period in (fractal ? new[] { 2, 3, 7, 20, 51 } : new[] { 1, 2, 3, 20, 51 }))
            foreach (var scale in (fractal ? new[] { .9 } : new[] { double.Epsilon, .1, .9, 1d }))
            foreach (var count in new[] { 0, 1, period - 1, period, period + 1, 150 })
            {
                var configured = RangeAdaptiveComparison.Pair(fractal, scale);
                foreach (var shape in Shapes)
                    comparisons += Check(configured, Fixture(shape, count), period);
                comparisons += Check(configured, CompetitorData.Create(count), period);
            }
            return comparisons;
        }
        if (pair.Id == "Skender.GetHtTrendline")
        {
            foreach (var midpoint in new[] { false, true })
            foreach (var count in new[] { 0, 1, 5, 6, 7, 10, 11, 12, 50, 150, 250 })
            {
                var configured = SeededHilbertTrendComparison.Pair(midpoint);
                foreach (var shape in Shapes)
                    comparisons += Check(configured, Fixture(shape, count), 20);
                comparisons += Check(configured, CompetitorData.Create(count), 20);
            }
            return comparisons;
        }
        if (SeededAdaptiveComparison.Pairs.Any(p=>p.Id==pair.Id))
        {
            var quan=pair.Id=="QuanTAlib.Kama";
            foreach(var c in new[]{(1,2,30),(2,2,30),(5,2,9),(10,3,30),(20,2,30)})
            {
                var configured=SeededAdaptiveComparison.Pair(quan,c.Item2,c.Item3);
                foreach(var count in new[]{0,1,c.Item1-1,c.Item1,c.Item1+1,100}.Distinct())
                foreach(var shape in Shapes)comparisons+=Check(configured,Fixture(shape,count),c.Item1);
                comparisons+=Check(configured,CompetitorData.Create(100),c.Item1);
            }
            return comparisons;
        }
        if (pair.Id == "Trady.Indicator.IchimokuCloud")
        {
            foreach(var c in new[]{(1,1,1),(2,3,5),(5,2,3),(9,26,52)})
            {
                var configured=ExtendedCloudComparison.Pair(c.Item1,c.Item2,c.Item3);
                foreach(var count in new[]{0,1,2,3,10,26,52,80,120})
                foreach(var shape in Shapes)comparisons+=Check(configured,Fixture(shape,count),20);
                comparisons+=Check(configured,CompetitorData.Create(120),20);
            }
            return comparisons;
        }
        if (pair.Id == "Skender.GetIchimoku")
        {
            foreach (var c in new[] { (1,1,2,0,0),(2,3,5,3,2),(9,26,52,26,26),(3,2,4,20,0),(2,3,5,0,20) })
            {
                var configured=IchimokuCloudComparison.Pair(c.Item1,c.Item2,c.Item3,c.Item4,c.Item5);
                foreach(var count in new[]{0,1,2,3,10,26,52,80,120})
                foreach(var shape in Shapes)comparisons+=Check(configured,Fixture(shape,count),20);
                comparisons+=Check(configured,CompetitorData.Create(120),20);
            }
            return comparisons;
        }
        if (pair.Id == "TaLib.Functions.Accbands")
        {
            foreach (var period in new[] { 2,3,20,101,int.MaxValue })
            {
                foreach (var count in new[] { 0,2,3,19,20,21,100 })
                foreach (var shape in Shapes) comparisons += Check(pair,Fixture(shape,count),period);
                comparisons += Check(pair,CompetitorData.Create(100),period);
                comparisons += Check(pair,AccumulationDistributionComparison.Fixture(),period);
            }
            return comparisons;
        }
        if (SmoothedAccumulationComparison.Pairs.Any(p => p.Id == pair.Id))
        {
            var first = pair.Id == "TaLib.Functions.AdOsc";
            foreach (var c in first ? new[] { (2,2), (2,3), (7,3), (3,10) } : new[] { (1,2), (2,3), (3,10) })
            foreach (var suppression in first ? new[] { 0,2 } : new[] { 0 })
            {
                using var settings = new TripleRateComparison.Settings(false, suppression);
                var configured = SmoothedAccumulationComparison.Pair(first,c.Item1,c.Item2,suppression);
                var lb = Math.Max(c.Item1,c.Item2)-1+suppression;
                foreach (var count in new[] { 0,2,lb,lb+1,100 }.Distinct())
                foreach (var shape in Shapes) if (!first || count!=1) comparisons += Check(configured,Fixture(shape,count),20);
                comparisons += Check(configured,CompetitorData.Create(100),20);
                comparisons += Check(configured,AccumulationDistributionComparison.Fixture(),20);
            }
            return comparisons;
        }
        if (pair.Id is "TaLib.Functions.Macd" or "TaLib.Functions.MacdFix")
        {
            var fixedCoefficients=pair.Id=="TaLib.Functions.MacdFix";
            foreach(var first in new[]{false,true})
            foreach(var suppression in new[]{0,2})
            foreach(var c in fixedCoefficients?new[]{(12,26,2),(12,26,9)}:new[]{(2,2,2),(3,7,3),(7,3,4),(12,26,9)})
            {
                using var settings=new TripleRateComparison.Settings(first,suppression);
                var configured=AlignedMacdComparison.Pair(fixedCoefficients,c.Item1,c.Item2,c.Item3,first,suppression);var lb=Math.Max(c.Item1,c.Item2)+c.Item3-2+2*suppression;
                foreach(var count in new[]{0,2,lb-1,lb,lb+1,100}.Distinct())
                foreach(var shape in Shapes)if(count!=1)comparisons+=Check(configured,Fixture(shape,count),20);
                comparisons+=Check(configured,CompetitorData.Create(100),20);
            }
            return comparisons;
        }
        if (EmaDifferenceSignalComparison.Ids.Contains(pair.Id))
        {
            var variant=Array.IndexOf(EmaDifferenceSignalComparison.Ids,pair.Id);
            foreach(var c in variant<2?new[]{(1,2,1),(2,3,2),(3,7,4),(12,26,9)}:new[]{(1,1,1),(3,2,2),(3,3,4),(2,7,3),(12,26,9)})
            {
                var configured=EmaDifferenceSignalComparison.Pair(variant,c.Item1,c.Item2,c.Item3);
                foreach(var count in new[]{0,1,c.Item1-1,c.Item1,c.Item2-1,c.Item2,c.Item2+c.Item3-2,c.Item2+c.Item3-1,100}.Distinct())
                foreach(var shape in Shapes)comparisons+=Check(configured,EmaDifferenceSignalComparison.Fixture(variant,shape,count),20);
                comparisons+=Check(configured,CompetitorData.Create(100),20);
            }
            return comparisons;
        }
        if (pair.Id is "QuanTAlib.Historical" or "QuanTAlib.Realized")
        {
            var realized=pair.Id=="QuanTAlib.Realized";
            foreach(var period in new[]{2,3,7,20})
            foreach(var annual in new[]{false,true})
            {
                var configured=LogVolatilityComparison.Pair(realized,annual);
                foreach(var count in new[]{0,1,period-1,period,period+1,80}.Distinct())
                foreach(var shape in Shapes)comparisons+=LogVolatilityComparison.Check(configured,Fixture(shape,count),period);
                comparisons+=LogVolatilityComparison.Check(configured,CompetitorData.Create(100),period);
                comparisons+=LogVolatilityComparison.Check(configured,CompetitorData.FromCloses([1,2,0,1,2,4,8,16]),period);
                comparisons+=LogVolatilityComparison.Check(configured,CompetitorData.FromCloses([1,-1,-2,-4,-8,-16]),period);
            }
            return comparisons;
        }
        if (pair.Id is "Skender.GetDynamic" or "QuanTAlib.Mgdi")
        {
            var reset=pair.Id=="Skender.GetDynamic";
            foreach(var period in new[]{1,2,7,20})
            foreach(var factor in new[]{.6,1d,2})
            {
                var configured=McGinleyComparison.Pair(reset,factor);
                foreach(var shape in Shapes)
                foreach(var count in new[]{0,1,period,period+1,80}.Distinct())
                    comparisons+=McGinleyComparison.Check(configured,Fixture(shape,count),period);
                comparisons+=McGinleyComparison.Check(configured,CompetitorData.Create(100),period);
                comparisons+=McGinleyComparison.Check(configured,CompetitorData.FromCloses([0,2,2,2,2,2]),period);
                comparisons+=McGinleyComparison.Check(configured,CompetitorData.FromCloses([1,0,1]),period);
            }
            return comparisons;
        }
        if (pair.Id == "Skender.GetPrs")
        {
            foreach(var enabled in new[]{false,true})
            foreach(var mean in new int?[]{null,1,3,20})
            foreach(var period in new[]{1,2,7,20})
            {
                var configured=PriceRelativeComparison.Create(enabled,mean);
                foreach(var count in new[]{0,1,period-1,period,period+1,mean??0,80}.Distinct())
                {
                    if(enabled&&count<period)continue;
                    foreach(var shape in Shapes)comparisons+=Check(configured,Fixture(shape,count),period);
                    comparisons+=Check(configured,CompetitorData.Create(count),period);
                }
            }
            return comparisons;
        }
        if (pair.Id is "Skender.GetTsi" or "Skender.GetPmo")
        {
            var pmo = pair.Id == "Skender.GetPmo";
            foreach (var period in pmo ? new[] {2,3,14,20} : new[] {1,2,3,14,20})
            foreach (var smooth in new[] {1,2,13,20})
            foreach (var signal in pmo ? new[] {1,2,10} : new[] {0,1,2,7})
            {
                var configured = SeededMomentumComparison.Pair(pmo, smooth, signal);
                var first = period + smooth - 1;
                foreach (var shape in Shapes)
                foreach (var count in new[] {0,1,first-1,first,first+1,first+signal,6*period+30}.Distinct())
                    comparisons += SeededMomentumComparison.Check(configured, Fixture(shape,count),period);
                comparisons += SeededMomentumComparison.Check(configured,CompetitorData.Create(100),period);
            }
            return comparisons;
        }
        if (pair.Id is "Skender.GetTrix" or "TaLib.Functions.Trix")
        {
            var ta=pair.Id=="TaLib.Functions.Trix";
            foreach(var period in ta?new[]{2,3,14,20}:new[]{1,2,3,14,20})
            foreach(var signal in ta?new int?[]{null}:new int?[]{null,1,3,20})
            foreach(var firstPrice in ta?new[]{false,true}:new[]{false})
            foreach(var suppression in ta?new[]{0,2}:new[]{0})
            {
                using var settings=new TripleRateComparison.Settings(firstPrice,suppression);
                var configured=TripleRateComparison.Pair(ta,signal,firstPrice,suppression);
                var first=ta?3*(period-1+suppression)+1:period;
                foreach(var shape in Shapes)
                foreach(var count in new[]{0,1,first-1,first,first+1,first+(signal??1),6*period+30})
                    if(!ta||count!=1)comparisons+=Check(configured,Fixture(shape,count),period);
                comparisons+=Check(configured,CompetitorData.Create(100),period);
            }
            return comparisons;
        }
        if (TillsonComparison.Ids.Contains(pair.Id))
        {
            var variant=Array.IndexOf(TillsonComparison.Ids,pair.Id);
            var old=TALib.Core.UnstablePeriodSettings.Get(TALib.Core.UnstableFunc.T3);
            try
            {
                foreach(var period in variant==1?new[]{2,3,7,20}:new[]{1,2,3,7,20})
                foreach(var factor in variant==1?new[]{0,.7,1d}:variant==0?new[]{.2,.7,2}:new[]{-.5,0,.7,2})
                foreach(var sma in variant==2?new[]{true,false}:new[]{true})
                foreach(var suppression in variant==1?new[]{0,3}:new[]{0})
                {
                    if(variant==1)TALib.Core.UnstablePeriodSettings.Set(TALib.Core.UnstableFunc.T3,suppression);
                    var configured=TillsonComparison.Pair(variant,factor,sma,suppression);
                    foreach(var shape in Shapes)
                    foreach(var count in new[]{0,1,period-1,period,6*(period-1),6*(period-1)+suppression+1,8*period+13})
                        if(variant!=1||count!=1)comparisons+=Check(configured,Fixture(shape,count),period);
                }
            }
            finally{TALib.Core.UnstablePeriodSettings.Set(TALib.Core.UnstableFunc.T3,old);}
            return comparisons;
        }
        if (TaDirectionalComparison.Pairs.Any(p => p.Id == pair.Id))
        {
            var measure=(OoplesFinance.StockIndicators.Indicators.PriorDirectionalMeasure)Array.FindIndex(TaDirectionalComparison.Pairs,p=>p.Id==pair.Id);
            foreach(var period in measure<=OoplesFinance.StockIndicators.Indicators.PriorDirectionalMeasure.NegativeIndicator?new[]{1,2,3,14,20}:new[]{2,3,14,20})
            foreach(var unstable in new[]{0,3})
            {
                using var settings=new TaDirectionalComparison.Settings(measure,unstable);
                var configured=TaDirectionalComparison.Pair(measure,unstable);
                var first=(int)TaDirectionalComparison.Lookback(period,measure,unstable);
                foreach(var shape in Shapes)
                foreach(var count in new[]{0,2,first,first+1,4*period+13})
                    if(count!=1)comparisons+=Check(configured,Fixture(shape,count),period);
                comparisons+=Check(configured,CompetitorData.Create(60),period);
            }
            return comparisons;
        }
        if (pair.Id == "Skender.GetAdx")
        {
            foreach(var period in new[]{2,3,14,20})
            foreach(var shape in Shapes)
            foreach(var count in new[]{0,1,period-1,period,period+1,2*period-1,2*period,3*period-1,4*period+13})
                comparisons+=Check(pair,Fixture(shape,count),period);
            comparisons+=Check(pair,CompetitorData.Create(50),int.MaxValue);
            return comparisons;
        }
        if (DirectionalComparison.Pairs.Any(p => p.Id == pair.Id))
        {
            var measure=(OoplesFinance.StockIndicators.Indicators.DirectionalWindowMeasure)Array.FindIndex(DirectionalComparison.Pairs,p=>p.Id==pair.Id);
            foreach(var period in new[]{1,3,14,20})
            foreach(var lag in measure==OoplesFinance.StockIndicators.Indicators.DirectionalWindowMeasure.EarlyRating?new[]{0,1,period,100,int.MaxValue}:new[]{period})
            {
                var configured=DirectionalComparison.Pair(measure,lag);
                foreach(var shape in Shapes)
                foreach(var count in new[]{0,1,period-1,period,period+1,4*period+13})
                    comparisons+=Check(configured,Fixture(shape,count),period);
                comparisons+=Check(configured,CompetitorData.Create(60),period);
            }
            return comparisons;
        }
        if (pair.Id == "Skender.GetMaEnvelopes")
        {
            foreach (var average in Enum.GetValues<OoplesFinance.StockIndicators.Indicators.EnvelopeAverage>())
            foreach (var percent in new[] { double.Epsilon, 2.5, 100, 200 })
            foreach (var period in new[] { 2, 3, 9, 20 })
            {
                var configured = EnvelopeComparison.Create(average, percent);
                comparisons += Check(configured, EnvelopeComparison.Fixture(), period);
                foreach (var shape in Shapes)
                foreach (var count in new[] { 0, 1, period - 1, period, period + 1, 4 * period + 13 })
                    comparisons += Check(configured, Fixture(shape, count), period);
            }
            return comparisons;
        }
        if (HullComparison.Pairs.Any(p => p.Id == pair.Id))
        {
            foreach (var period in new[] { 2, 3, 5, 7, 8, 9, 20 })
            {
                comparisons += Check(pair, HullComparison.Fixture(), period);
                foreach (var shape in Shapes)
                foreach (var count in new[] { 0, 1, period - 1, period, period + 1, 4 * period + 13 })
                    comparisons += Check(pair, Fixture(shape, count), period);
            }
            return comparisons;
        }
        if (AlmaComparison.Pairs.Any(p => p.Id == pair.Id))
        {
            var full = pair.Id == "Skender.GetAlma";
            foreach (var config in full ? new[] { (.85, 6d), (0d, 1d), (.5, 2d), (1d, 20d), (.5, double.MaxValue) }
                : new[] { (.85, 6d), (0d, 1d), (.5, 2d), (1d, 20d), (-1d, 0d), (2d, -6d) })
            foreach (var period in full ? new[] { 2, 3, 9, 20 } : new[] { 1, 2, 3, 9, 20 })
            {
                var configured = AlmaComparison.Create(full, config.Item1, config.Item2);
                comparisons += Check(configured, AlmaComparison.Fixture(), period);
                foreach (var shape in Shapes)
                foreach (var count in new[] { 0, 1, period - 1, period, period + 1, 4 * period + 13 })
                    comparisons += Check(configured, Fixture(shape, count), period);
            }
            return comparisons;
        }
        if (AnalyticKernelComparison.Pairs.Any(p => p.Id == pair.Id))
        {
            var gaussian = pair.Id == "QuanTAlib.Gma";
            foreach (var sigma in gaussian ? new double?[] { null, .25, 2, -2 } : new double?[] { null })
            foreach (var period in gaussian ? new[] { 2, 3, 4, 20 } : new[] { 1, 2, 3, 4, 20 })
            {
                var configured = AnalyticKernelComparison.Create(gaussian, sigma);
                comparisons += Check(configured, AnalyticKernelComparison.Fixture(), period);
                foreach (var shape in Shapes)
                foreach (var count in new[] { 0, 1, period - 1, period, period + 1, 4 * period + 13 })
                    comparisons += Check(configured, Fixture(shape, count), period);
            }
            return comparisons;
        }
        if (pair.Id == "QuanTAlib.Entropy")
        {
            foreach (var period in new[] { 2, 3, 7, 20 })
            {
                comparisons += Check(pair, EntropyComparison.Fixture(), period);
                foreach (var shape in Shapes)
                foreach (var count in new[] { 0, 1, period - 1, period, period + 1, 4 * period + 13 })
                    comparisons += Check(pair, Fixture(shape, count), period);
            }
            comparisons += Check(pair, CompetitorData.FromCloses(Enumerable.Repeat(1d, 499).Append(2).ToArray()), 500);
            return comparisons;
        }
        if (pair.Id == "QuanTAlib.Hwma")
        {
            foreach (var factors in new double[]?[] { null, [0, 0, 0], [.5, .2, .1], [1, 1, 1], [-.1, 0, 0], [2, 0, 0] })
            foreach (var period in new[] { 1, 3, 20 })
            {
                var configured = HoltWinterComparison.Create(factors);
                comparisons += Check(configured, HoltWinterComparison.Fixture(), period);
                foreach (var shape in Shapes)
                foreach (var count in new[] { 1, 3, 20, 93 }) comparisons += Check(configured, Fixture(shape, count), period);
            }
            foreach (var factors in new[] { new[] { .5, .2, .1 }, new[] { .8, .2, .1 }, new[] { .1, .1, .1 } })
                comparisons += Check(HoltWinterComparison.Create(factors, true), HoltWinterComparison.Fixture(), 20);
            comparisons += Check(pair, HoltWinterComparison.Fixture(), int.MaxValue);
            return comparisons;
        }
        if (pair.Id == "QuanTAlib.Curvature")
        {
            foreach (var period in new[] { 3, 4, 20 })
            {
                comparisons += Check(pair, CurvatureComparison.Fixture(), period);
                comparisons += Check(pair, CompetitorData.FromCloses([0, double.Epsilon, 2 * double.Epsilon, 4 * double.Epsilon, 8 * double.Epsilon]), period);
                comparisons += Check(pair, CompetitorData.FromCloses([1, Math.BitIncrement(1d), Math.BitIncrement(Math.BitIncrement(1d)), Math.BitDecrement(1d), 1]), period);
                foreach (var shape in Shapes)
                foreach (var count in new[] { 0, 1, period - 1, period, period + 1, 4 * period + 13 })
                    comparisons += Check(pair, Fixture(shape, count), period);
            }
            return comparisons;
        }
        if (ExpandingRecurrenceComparison.Pairs.Any(p => p.Id == pair.Id))
        {
            var regularized = pair.Id == "QuanTAlib.Rema";
            foreach (var lambda in regularized ? new[] { 0d, .5, 2, double.Epsilon, 1e20 } : new[] { .5 })
            foreach (var period in new[] { 1, 2, 3, 4, 20 })
            {
                var configured = ExpandingRecurrenceComparison.Create(regularized, lambda);
                comparisons += Check(configured, ExpandingRecurrenceComparison.Fixture(), period);
                foreach (var shape in Shapes)
                foreach (var count in new[] { 1, period, period + 1, 4 * period + 13 })
                    comparisons += Check(configured, Fixture(shape, count), period);
            }
            if (regularized) comparisons += Check(pair, ExpandingRecurrenceComparison.Fixture(), int.MaxValue);
            return comparisons;
        }
        if (pair.Id == "QuanTAlib.Ltma")
        {
            foreach (var gamma in new[] { 0d, .1, .5, .9, 1, double.Epsilon, Math.BitDecrement(1d) })
            {
                var configured = ZeroSeedLaguerreComparison.Create(gamma);
                comparisons += Check(configured, ZeroSeedLaguerreComparison.Fixture(), 20);
                foreach (var shape in Shapes)
                foreach (var count in new[] { 1, 2, 4, 93 }) comparisons += Check(configured, Fixture(shape, count), 20);
            }
            return comparisons;
        }
        if (pair.Id == "Skender.GetElderRay")
        {
            foreach (var period in new[] { 1, 3, 13, 20 })
                comparisons += Check(pair, ElderRayComparison.Fixture(), period);
        }
        if (TranscendentalComparison.Pairs.Any(p => p.Id == pair.Id))
        {
            var name = pair.Id.Split('.')[^1];
            foreach (var shape in Shapes)
            foreach (var count in new[] { 2, 3, 20, 93 })
                comparisons += Check(pair, TranscendentalComparison.Fixture(name, shape, count), 20);
            comparisons += Check(pair, TranscendentalComparison.BoundaryFixture(name), 20);
            return comparisons;
        }
        if (pair.Id == "Skender.GetVortex")
        {
            foreach (var period in new[] { 2, 3, 14, 20 })
            foreach (var shape in Shapes)
            foreach (var count in new[] { 0, 1, period-1, period, period+1, 4*period+13 })
                comparisons += Check(pair, Fixture(shape,count), period);
            return comparisons;
        }
        if (pair.Id is "Skender.GetUltimate" or "TaLib.Functions.UltOsc")
        {
            var ta=pair.Id=="TaLib.Functions.UltOsc";
            foreach (var p in ta ? new[] { (1,2,3), (7,14,28), (2,2,4), (4,2,2), (4,1,3), (1,3,4), (2,2,2) } : new[] { (1,2,3), (7,14,28), (1,3,4) })
            {
                var configured=TrueRangeRatioComparison.UltimatePair(ta,p.Item2,p.Item3);
                var longest=Math.Max(p.Item1,Math.Max(p.Item2,p.Item3));
                foreach (var shape in Shapes)
                foreach (var count in new[] { 0,1,longest-1,longest,longest+1,4*longest+13 })
                {
                    if(ta&&count==1)continue;
                    comparisons+=Check(configured,Fixture(shape,count),p.Item1);
                }
                comparisons+=Check(configured,CompetitorData.Create(60),p.Item1);
            }
            return comparisons;
        }
        if (AtrEnvelopeComparison.Pairs.Any(p => p.Id == pair.Id))
        {
            var variant = Array.IndexOf(AtrEnvelopeComparison.Ids, pair.Id);
            foreach (var periods in variant == 2 ? new[] { (1,1), (2,3), (3,2), (3,3), (20,10), (5,14) } : new[] { (2,3), (3,2), (3,3), (20,10), (5,14) })
            foreach (var multiplier in variant == 2 ? new[] { -1d, 0, .5, 2 } : new[] { .5, 2d })
            {
                var configured = AtrEnvelopeComparison.Create(variant, periods.Item2, multiplier);
                foreach (var shape in Shapes)
                foreach (var count in new[] { 0, 1, periods.Item1 - 1, periods.Item1, periods.Item2, periods.Item2 + 1, 4 * Math.Max(periods.Item1,periods.Item2) + 13 })
                    comparisons += Check(configured, Fixture(shape,count), periods.Item1);
                comparisons += Check(configured, CompetitorData.Create(50), periods.Item1);
            }
            return comparisons;
        }
        if (ChandelierComparison.Pairs.Any(p => p.Id == pair.Id))
        {
            var trady = pair.Id == "Trady.Indicator.ChandelierExit";
            foreach (var period in trady ? new[] { 1, 2, 3, 22 } : new[] { 2, 3, 22 })
            foreach (var multiplier in trady ? new[] { -1d, 0, .5, 3 } : new[] { .5, 3d })
            foreach (var shortSide in trady ? new[] { false } : new[] { false, true })
            {
                var configured = ChandelierComparison.Create(trady, shortSide, multiplier);
                foreach (var shape in Shapes)
                foreach (var count in new[] { 0, 1, period - 1, period, period + 1, 4 * period + 13 })
                    comparisons += Check(configured, Fixture(shape, count), period);
                comparisons += Check(configured, CompetitorData.Create(50), period);
            }
            return comparisons;
        }
        if (MoneyFlowIndexComparison.Pairs.Any(p => p.Id == pair.Id))
        {
            foreach (var period in new[] { 2, 3, 14, 20 })
            foreach (var shape in Shapes)
            foreach (var count in new[] { 0, 1, period - 1, period, period + 1, 4 * period + 13 })
            {
                if (pair.Id == "TaLib.Functions.Mfi" && count == 1) continue;
                comparisons += Check(pair, Fixture(shape, count), period);
            }
            foreach (var period in new[] { 2, 3, 14 }) comparisons += Check(pair, CompetitorData.Create(50), period);
            return comparisons;
        }
        if (pair.Id == "Skender.GetChop")
        {
            foreach (var period in new[] { 2, 3, 14, 20 })
            foreach (var shape in Shapes)
            foreach (var count in new[] { 0, 1, period - 1, period, period + 1, 4 * period + 13 })
                comparisons += Check(pair, Fixture(shape, count), period);
            foreach (var period in new[] { 2, 3, 14 })
                comparisons += Check(pair, CompetitorData.Create(50), period);
            return comparisons;
        }
        if (pair.Id == "Skender.GetUlcerIndex")
        {
            foreach (var period in new[] { 1, 2, 3, 14, 20 })
            foreach (var shape in Shapes)
            foreach (var count in new[] { 0, 1, period - 1, period, period + 1, 4 * period + 13 })
                comparisons += Check(pair, Fixture(shape, count), period);
            comparisons += Check(pair, CompetitorData.FromCloses([0,-1,2,1,0,-3,4,2,1]), 3);
            return comparisons;
        }
        if (CommodityChannelComparison.Pairs.Any(p => p.Id == pair.Id))
        {
            foreach (var period in pair.Id == "TaLib.Functions.Cci" ? new[] { 2, 3, 9, 20 } : new[] { 1, 2, 3, 9, 20 })
            {
                comparisons += Check(pair, CommodityChannelComparison.Fixture(), period);
                foreach (var shape in Shapes)
                foreach (var count in new[] { 0, 1, period - 1, period, period + 1, 2 * period - 2, 2 * period - 1, 4 * period + 13 })
                {
                    if (pair.Id == "TaLib.Functions.Cci" && count == 1) continue;
                    comparisons += Check(pair, Fixture(shape, count), period);
                }
            }
            return comparisons;
        }
        if (ReturnBetaComparison.Pairs.Any(p => p.Id == pair.Id))
        {
            var full = pair.Id == "Skender.GetBeta";
            foreach (var selection in full ? Enum.GetValues<OoplesFinance.StockIndicators.Indicators.ReturnBetaSelection>() : [OoplesFinance.StockIndicators.Indicators.ReturnBetaSelection.Standard])
            foreach (var period in new[] { 1, 2, 3, 9, 20 })
            {
                var configured = ReturnBetaComparison.Create(full, selection);
                comparisons += Check(configured, ReturnBetaComparison.Fixture(), period);
                foreach (var shape in Shapes)
                foreach (var count in new[] { 0, 1, period, period + 1, period + 2, 4 * period + 13 })
                {
                    if (!full && count == 1) continue;
                    comparisons += Check(configured, Fixture(shape, count), period);
                }
            }
            return comparisons;
        }
        if (PairStatisticsComparison.Pairs.Any(p => p.Id == pair.Id))
        {
            foreach (var period in new[] { 1, 2, 3, 9, 20 })
            {
                comparisons += Check(pair, PairStatisticsComparison.Fixture(), period);
                foreach (var shape in Shapes)
                foreach (var count in new[] { 0, 1, period - 1, period, period + 1, 4 * period + 13 })
                {
                    if (pair.Id == "TaLib.Functions.Correl" && count == 1) continue;
                    comparisons += Check(pair, Fixture(shape, count), period);
                }
            }
            return comparisons;
        }
        if (SampleShapeComparison.Pairs.Any(p => p.Id == pair.Id))
        {
            var kurtosis = pair.Id == "QuanTAlib.Kurtosis";
            foreach (var period in new[] { kurtosis ? 4 : 3, 9, 20 })
            {
                comparisons += Check(pair, SampleShapeComparison.Fixture(), period);
                foreach (var shape in kurtosis ? new[] { "negative", "ramp", "descending", "alternating" } : Shapes)
                foreach (var count in new[] { 0, 1, 2, 3, 4, period - 1, period, period + 1, 4 * period + 13 })
                    comparisons += Check(pair, Fixture(shape, count), period);
            }
            return comparisons;
        }
        if (pair.Id is "QuanTAlib.Ema" or "QuanTAlib.Qema")
        {
            double[]?[] configurations = pair.Id == "QuanTAlib.Ema"
                ? [null, [1], [.5], [.2], [.01]]
                : [[.2,.2,.2,.2], [1,1,1,1], [.1,.3,.7,.9], [.9,.7,.3,.1]];
            foreach (var alphas in configurations)
            foreach (var period in new[] { 1, 3, 20 })
            {
                var configured = MassNormalizedComparison.Create(alphas);
                comparisons += Check(configured, CompensatedAverageComparison.Fixture(), period);
                foreach (var shape in Shapes)
                foreach (var count in new[] { 0, 1, period, period + 1, 16 * period + 13 })
                    comparisons += Check(configured, Fixture(shape, count), period);
                comparisons += Check(configured, Fixture("walk", 2400), period);
            }
            if (pair.Id == "QuanTAlib.Qema") return comparisons;
        }
        if (CompensatedAverageComparison.Pairs.Any(p => p.Id == pair.Id))
        {
            foreach (var period in new[] { 1, 2, 3, 9, 20, 100 })
            {
                comparisons += Check(pair, CompensatedAverageComparison.Fixture(), period);
                foreach (var shape in Shapes)
                foreach (var count in new[] { 0, 1, period - 1, period, period + 1, 16 * period + 13 })
                    comparisons += Check(pair, Fixture(shape, count), period);
            }
            return comparisons;
        }
        if (DecayingExtremeComparison.Pairs.Any(p => p.Id == pair.Id))
        {
            foreach (var decay in new[] { 0d, .1, 2, 1000 })
            foreach (var period in new[] { 1, 2, 3, 9, 20 })
            {
                var configured = DecayingExtremeComparison.Create(pair.Id == "QuanTAlib.Max", decay);
                comparisons += Check(configured, DecayingExtremeComparison.Fixture(), period);
                foreach (var shape in Shapes)
                foreach (var count in new[] { 0, 1, period - 1, period, period + 1, 4 * period + 13 })
                    comparisons += Check(configured, Fixture(shape, count), period);
            }
            return comparisons;
        }
        if (StochasticRsiComparison.Pairs.Any(p => p.Id == pair.Id))
        {
            var skender = pair.Id.StartsWith("Skender", StringComparison.Ordinal);
            foreach (var (rsi, stochastic, signal, smooth) in new[] { (1, 1, 1, 1), (2, 3, 2, 1), (3, 2, 4, 3), (14, 14, 3, 2) })
            {
                var configured = StochasticRsiComparison.Create(skender, stochastic, signal, smooth);
                comparisons += Check(configured, NullableStrengthComparison.Fixture(), rsi);
                foreach (var shape in Shapes)
                foreach (var count in new[] { 0, 1, rsi - 1, rsi, rsi + stochastic - 1, rsi + stochastic + smooth + signal, 4 * rsi + 13 })
                    comparisons += Check(configured, Fixture(shape, count), rsi);
            }
            return comparisons;
        }
        if (StochasticMomentumComparison.Pairs.Any(p => p.Id == pair.Id))
        {
            var variant = StochasticMomentumComparison.Variants.Single(v => StochasticMomentumComparison.Id(v) == pair.Id);
            foreach (var (period, first, second, signal) in new[] { (1, 1, 1, 1), (2, 3, 2, 4), (3, 2, 5, 2), (13, 25, 2, 3) })
            {
                var configured = StochasticMomentumComparison.Create(variant, first, second, signal);
                comparisons += Check(configured, TradyExtremaComparison.Fixture(), period);
                foreach (var shape in Shapes)
                foreach (var count in new[] { 0, 1, period - 1, period, period + 1, 4 * period + 13 })
                    comparisons += Check(configured, Fixture(shape, count), period);
            }
            return comparisons;
        }
        if (StochasticSmaComparison.Pairs.Any(p => p.Id == pair.Id))
        {
            var variant = StochasticSmaComparison.Variants.Single(v => StochasticSmaComparison.Id(v) == pair.Id);
            foreach (var (period, k, d) in new[] { (1, 1, 1), (2, 5, 2), (3, 2, 5), (14, 3, 3) })
            {
                var configured = StochasticSmaComparison.Create(variant, k, d);
                comparisons += Check(configured, TradyExtremaComparison.Fixture(), period);
                foreach (var shape in Shapes)
                foreach (var count in new[] { 0, 1, period - 1, period, period + 1, 4 * period + 13 })
                    comparisons += Check(configured, Fixture(shape, count), period);
            }
            return comparisons;
        }
        if (RetrospectivePriceComparison.Pairs.Any(p => p.Id == pair.Id))
        {
            foreach (var period in pair.Id == "Skender.GetDpo" ? new[] { 1, 2, 3, 4, 9, 20 } : new[] { 2, 3, 9, 20 })
            {
                comparisons += Check(pair, RetrospectivePriceComparison.Fixture(), period);
                foreach (var shape in Shapes)
                foreach (var count in new[] { 0, 1, period - 1, period, period + 1, 2 * period + 1, 4 * period + 13 })
                    comparisons += Check(pair, Fixture(shape, count), period);
            }
            if (pair.Id == "Skender.GetFractal")
            {
                foreach (var (left, right) in new[] { (2, 3), (3, 2), (2, 7), (7, 2) })
                foreach (var close in new[] { false, true })
                    comparisons += Check(RetrospectivePriceComparison.FractalPair(left, right, close), RetrospectivePriceComparison.Fixture(), 2);
            }
            return comparisons;
        }
        if (AverageDifferenceComparison.Pairs.Any(p => p.Id == pair.Id))
        {
            var exponential = pair.Id.Contains("Exponential", StringComparison.Ordinal);
            foreach (var (first, second) in new[] { (1, 1), (1, 3), (3, 1), (3, 3), (2, 7), (12, 26) })
            foreach (var shape in Shapes)
            foreach (var count in new[] { 0, 1, Math.Max(first, second) - 1, Math.Max(first, second), 4 * Math.Max(first, second) + 13 })
                comparisons += Check(AverageDifferenceComparison.Create(exponential, first, second), Fixture(shape, count), 20);
            return comparisons;
        }
        if (NullableStrengthComparison.Pairs.Any(p => p.Id == pair.Id))
        {
            var convention = Enum.GetValues<OoplesFinance.StockIndicators.Indicators.NullableStrengthConvention>().Single(c => pair.Id.EndsWith("." + NullableStrengthComparison.Name(c), StringComparison.Ordinal));
            foreach (var lag in NullableStrengthComparison.Momentum(convention) ? new[] { 1, 2, 5 } : new[] { 1 })
            foreach (var period in new[] { 1, 2, 3, 14 })
                comparisons += Check(NullableStrengthComparison.Create(convention, lag), NullableStrengthComparison.Fixture(), period);
        }
        if (WilderStrengthComparison.Pairs.Any(p => p.Id == pair.Id))
        {
            foreach (var period in new[] { 2, 3, 14, 20 })
                comparisons += Check(pair, WilderStrengthComparison.Fixture(), period);
            if (pair.Id == "Skender.GetRsi") comparisons += Check(pair, WilderStrengthComparison.Fixture(), 1);
        }
        if (PathRatioComparison.Pairs.Any(p => p.Id == pair.Id))
        {
            foreach (var period in new[] { 1, 2, 5, 20 })
                comparisons += Check(pair, PathRatioComparison.Fixture(), period);
        }
        if (pair.Id == "Skender.GetHeikinAshi")
        {
            comparisons += Check(pair, HeikinAshiComparison.Fixture(), 20);
            foreach (var count in new[] { 1, 2, 93 })
                comparisons += Check(pair, CompetitorData.Create(count), 20);
        }
        if (pair.Id is "Skender.GetAlligator" or "Skender.GetGator")
        {
            foreach (var settings in new[] { AlligatorComparison.Default, new[] { 3, 1, 2, 1, 1, 1 }, new[] { 8, 2, 5, 3, 2, 4 } })
            {
                var configured = AlligatorComparison.Create(pair.Id == "Skender.GetGator", settings);
                comparisons += Check(configured, AlligatorComparison.Fixture(), 20);
                foreach (var shape in Shapes)
                foreach (var count in new[] { 1, 2, 8, 20, 93 })
                    comparisons += Check(configured, Fixture(shape, count), 20);
            }
            return comparisons;
        }
        if (pair.Id == "Skender.GetAwesome")
        {
            foreach (var (fast, slow) in new[] { (1, 2), (2, 5), (5, 34), (10, 11) })
            {
                var configured = AwesomeComparison.Create(fast, slow);
                comparisons += Check(configured, AwesomeComparison.Fixture(), 20);
                foreach (var shape in Shapes)
                foreach (var count in new[] { 1, 2, 34, 93 })
                    comparisons += Check(configured, Fixture(shape, count), 20);
            }
            return comparisons;
        }
        if (CircularComparison.Pairs.Any(p => p.Id == pair.Id))
        {
            var name = pair.Id.Split('.')[^1];
            foreach (var shape in Shapes)
            foreach (var count in new[] { 2, 3, 20, 93 })
                comparisons += Check(pair, CircularComparison.Fixture(name, shape, count), 20);
            comparisons += Check(pair, CircularComparison.BoundaryFixture(name), 20);
            return comparisons;
        }
        if (ElementaryMathComparison.Pairs.Any(p => p.Id == pair.Id))
        {
            var squareRoot = pair.Id.EndsWith("Sqrt", StringComparison.Ordinal);
            foreach (var shape in Shapes)
            foreach (var count in new[] { 2, 3, 20, 93 })
                comparisons += Check(pair, ElementaryMathComparison.Fixture(shape, count, squareRoot), 20);
            comparisons += Check(pair, ElementaryMathComparison.BoundaryFixture(squareRoot), 20);
            return comparisons;
        }
        if (pair.IsCandle)
        {
            foreach (var period in new[] { 1, 3, 20 })
            {
                comparisons += Check(pair, CandleComparison.Fixture(), period);
                comparisons += Check(pair, CandleComparison.ShadowFixture(), period);
                comparisons += Check(pair, AverageRangeDojiComparison.Fixture(), period);
                comparisons += Check(pair, BodyShadowComparison.Fixture(), period);
                comparisons += Check(pair, ContextReversalComparison.Fixture(), period);
                comparisons += Check(pair, PercentileCandleComparison.Fixture(), period);
                comparisons += Check(pair, ContainmentCandleComparison.Fixture(), period);
                comparisons += Check(pair, SequenceCandleComparison.Fixture(), period);
                comparisons += Check(pair, PenetrationCandleComparison.Fixture(), period);
                comparisons += Check(pair, StrictHaramiComparison.Fixture(period), period);
                comparisons += Check(pair, DelayedDarkCloudComparison.Fixture(period), period);
                comparisons += Check(pair, NeckCandleComparison.Fixture(), period);
                comparisons += Check(pair, TripleBodyComparison.Fixture(), period);
                comparisons += Check(pair, KickingRickshawComparison.Fixture(), period);
                comparisons += Check(pair, MatchedLinesComparison.Fixture(), period);
                comparisons += Check(pair, CrowSoldierComparison.Fixture(), period);
                comparisons += Check(pair, StarReversalComparison.Fixture(), period);
                comparisons += Check(pair, GapContinuationComparison.Fixture(), period);
                comparisons += Check(pair, ExtendedReversalComparison.Fixture(), period);
                comparisons += Check(pair, HikkakeComparison.Fixture(), period);
                comparisons += Check(pair, FiveCandleContinuationComparison.Fixture(), period);
                comparisons += Check(pair, ExhaustionComparison.Fixture(), period);
                comparisons += Check(pair, TrendTasukiComparison.Fixture(period), period);
                comparisons += Check(pair, TrendBabyComparison.Fixture(period), period);
                comparisons += Check(pair, TrendMethodsComparison.Fixture(period), period);
                comparisons += Check(pair, TrendStarComparison.Fixture(period), period);
                if (pair.Id is "Skender.GetDoji" or "Skender.GetMarubozu") comparisons += Check(pair, PriceCandleComparison.Fixture(), period);
                comparisons += Check(pair, TradyCandleComparison.TrendFixture(period), period);
                foreach (var length in new[] { 1, 2, 3, period, period + 1 })
                    comparisons += Check(pair, Fixture("constant", length), period);
            }
            return comparisons;
        }
        if (pair.Id.StartsWith("Trady.Indicator.", StringComparison.Ordinal) && TradyExtremaComparison.Pairs.Any(p => p.Id == pair.Id))
            foreach (var period in new[] { 2, 3, 20 }) comparisons += Check(pair, TradyExtremaComparison.Fixture(), period);
        if (AccumulationDistributionComparison.Pairs.Any(p => p.Id == pair.Id))
        {
            comparisons += Check(pair, AccumulationDistributionComparison.Fixture(), 20);
            // Retain the unrounded workloads as correctness stress tests. Their
            // decimal package inputs and binary64 inputs have separate references.
            comparisons += Check(pair, CompetitorData.Create(1000), 20);
            comparisons += Check(pair, CompetitorData.Create(10000), 20);
        }
        if (BalanceOfPowerComparison.Pairs.Any(p => p.Id == pair.Id))
            foreach (var period in new[] { 1, 2, 3, 20 }) comparisons += Check(pair, BalanceOfPowerComparison.Fixture(), period);
        if (PercentileComparison.Pairs.Any(p => p.Id == pair.Id))
            foreach (var period in new[] { 2, 3, 20 }) comparisons += Check(pair, PercentileComparison.Fixture(), period);
        if (WilderAverageComparison.Pairs.Any(p => p.Id == pair.Id))
            foreach (var period in new[] { 1, 2, 3, 20 }) comparisons += Check(pair, WilderAverageComparison.Fixture(), period);
        if (SeededAtrComparison.Pairs.Any(p => p.Id == pair.Id))
            foreach (var period in new[] { 2, 3, 20 }) comparisons += Check(pair, SeededAtrComparison.Fixture(), period);
        if (pair.Id == "QuanTAlib.Atr")
            foreach (var period in new[] { 1, 2, 3, 20 }) comparisons += Check(pair, SeededAtrComparison.Fixture(), period);
        if (pair.Id == "TaLib.Functions.Natr")
            foreach (var period in new[] { 1, 2, 3, 20 }) comparisons += Check(pair, SeededAtrComparison.Fixture(), period);
        if (WindowRegressionComparison.Pairs.Any(p => p.Id == pair.Id))
            foreach (var period in new[] { 2, 3, 20 }) comparisons += Check(pair, WindowRegressionComparison.Fixture(), period);
        if (ConvolutionComparison.Pairs.Any(p => p.Id == pair.Id))
            foreach (var period in new[] { 1, 2, 3, 20 }) comparisons += Check(pair, WindowRegressionComparison.Fixture(), period);
        if (pair.Id == "QuanTAlib.Mma")
            foreach (var period in new[] { 2, 3, 20 }) comparisons += Check(pair, WindowRegressionComparison.Fixture(), period);
        if (pair.Id is "QuanTAlib.Slope" or "Skender.GetSlope")
            foreach (var period in new[] { 2, 3, 20 }) comparisons += Check(pair, RegressionStatisticsComparison.Fixture(), period);
        if (DispersionComparison.Ids.Contains(pair.Id))
        {
            foreach (var period in new[] { 2, 3, 20 }) comparisons += Check(pair, DispersionComparison.Fixture(), period);
            if (pair.Id.StartsWith("TaLib.", StringComparison.Ordinal)) comparisons += Check(pair, DispersionComparison.CancellationFixture(), 3);
            if (pair.Id is "QuanTAlib.Variance" or "QuanTAlib.Stddev")
                foreach (var sample in new[] { false, true }) comparisons += Check(DispersionComparison.Create(pair.Id, sample), DispersionComparison.Fixture(), 3);
            if (pair.Id == "TaLib.Functions.StdDev")
                foreach (var multiplier in new[] { -2d, 0, 0.5, 2 }) comparisons += Check(DispersionComparison.Create(pair.Id, false, multiplier), DispersionComparison.Fixture(), 3);
        }
        if (pair.Id == "Skender.GetStdDev")
            foreach (var smooth in new int?[] { null, 1, 3, 20 })
            {
                var configured = SkenderDeviationComparison.Create(smooth);
                comparisons += Check(configured, DispersionComparison.Fixture(), 3);
                comparisons += Check(configured, SkenderDeviationComparison.FlatRoundingFixture(), 3);
                comparisons += Check(configured, SkenderDeviationComparison.CollapsedQuoteFixture(), 2);
            }
        if (pair.Id == "QuanTAlib.Rma")
            foreach (var period in new[] { 1, 2, 3, 20 })
            {
                comparisons += Check(pair, ReverseWilderComparison.Fixture(), period);
                comparisons += Check(pair, ReverseWilderComparison.RoundedSeedFixture(), period);
            }
        if (FixedWeightedComparison.Pairs.Any(p => p.Id == pair.Id))
            foreach (var period in new[] { 1, 2, 3, 20, 65536 })
                comparisons += Check(pair, FixedWeightedComparison.Fixture(), period);
        if (pair.Id == "QuanTAlib.Mode")
        {
            foreach (var period in new[] { 1, 2, 3, 4, 20 })
            {
                comparisons += Check(pair, WindowModeComparison.Fixture(), period);
                comparisons += Check(pair, WindowModeComparison.AdjacentFixture(), period);
            }
            comparisons += Check(pair, WindowModeComparison.LaneFixture(), 9);
        }
        if (VolumeRecurrenceComparison.Ids.Contains(pair.Id))
        {
            foreach (var period in new[] { 1, 2, 3, 20 }) comparisons += Check(pair, VolumeRecurrenceComparison.Fixture(), period);
            comparisons += Check(pair, VolumeRecurrenceComparison.CollapsedVolumeFixture(), 2);
            if (pair.Id == "Skender.GetForceIndex") comparisons += Check(pair, VolumeRecurrenceComparison.ForceCancellationFixture(), 1);
        }
        if (VolumePriceComparison.Pairs.Any(p => p.Id == pair.Id) || pair.Id == "Skender.GetCmf")
        {
            foreach (var period in new[] { 1, 2, 3, 20 }) comparisons += Check(pair, VolumePriceComparison.Fixture(), period);
            comparisons += Check(pair, VolumePriceComparison.CollapsedFixture(), 2);
            if (pair.Id == "Skender.GetCmf") comparisons += Check(pair, MoneyFlowDetailComparison.CollapsedRangeFixture(), 2);
            if (pair.Id != "Trady.Indicator.VolumeWeightedAveragePrice")
                foreach (var period in new[] { 1, 2, 3, 20 }) comparisons += Check(pair, VolumePriceComparison.Fixture(true), period);
            if (pair.Id == "Skender.GetVwap")
            {
                var data = VolumePriceComparison.Fixture(true);
                foreach (var anchor in new[] { data.Dates[2], data.Dates[2].AddTicks(1), data.Dates[^1].AddDays(1) })
                    comparisons += Check(VolumePriceComparison.Vwap(anchor), data, 20);
            }
            if (pair.Id == "Trady.Indicator.VolumeWeightedAveragePrice")
            {
                foreach (var period in new[] { 1, 2, 3, 20 }) comparisons += Check(VolumePriceComparison.Trady(period), VolumePriceComparison.Fixture(), period);
                comparisons += Check(VolumePriceComparison.Trady(20), BenchmarkFixture(pair, 10_000), 20);
            }
        }
        if (AroonComparison.Ids.Contains(pair.Id))
        {
            foreach (var period in new[] { 2, 3, 20 }) comparisons += Check(pair, AroonComparison.Fixture(), period);
            comparisons += Check(pair, PriceWindowChannelComparison.CollapsedQuoteFixture(), 2);
            if (!pair.Id.StartsWith("TaLib.", StringComparison.Ordinal)) comparisons += Check(pair, AroonComparison.Fixture(), 1);
        }
        if (PriceWindowChannelComparison.Ids.Contains(pair.Id))
        {
            foreach (var period in new[] { 2, 3, 20 }) comparisons += Check(pair, PriceWindowChannelComparison.Fixture(), period);
            comparisons += Check(pair, PriceWindowChannelComparison.CollapsedQuoteFixture(), 2);
            if (pair.Id != "TaLib.Functions.WillR") comparisons += Check(pair, PriceWindowChannelComparison.Fixture(), 1);
        }
        if (MeanErrorComparison.Pairs.Any(p => p.Id == pair.Id))
        {
            foreach (var period in new[] { 2, 3, 20 }) comparisons += Check(pair, MeanErrorComparison.Fixture(), period);
            comparisons += Check(pair, MeanErrorComparison.RoundedMeanFixture(), 2);
            comparisons += Check(pair, MeanErrorComparison.CollapsedQuoteFixture(), 2);
            if (pair.Id == "Skender.GetSmaAnalysis") comparisons += Check(pair, MeanErrorComparison.Fixture(), 1);
        }
        if (WindowExtremeComparison.Pairs.Any(p => p.Id == pair.Id))
            foreach (var period in new[] { 2, 3, 20 }) comparisons += Check(pair, WindowExtremeComparison.Fixture(), period);
        if (PriceComparison.Pairs.Any(p => p.Id == pair.Id)) comparisons += Check(pair, PriceComparison.Fixture(), 20);
        if (ObvComparison.Pairs.Any(p => p.Id == pair.Id)) comparisons += Check(pair, ObvComparison.Fixture(), 20);
        foreach (var period in new[] { 2, 3, 20 })
        foreach (var shape in Shapes)
        foreach (var count in new[] { period - 1, period, period + 1, period * 4 + 13 })
        {
            if (count < pair.MinimumInputCount) continue;
            var data = Fixture(shape, count);
            comparisons += Check(pair, data, period);
        }
        return comparisons;
    }

    internal static int Check(ComparisonPair pair, CompetitorData data, int period, bool verifyIsolation = true)
    {
        var expected = pair.Reference?.Invoke(data, period) ?? Reference(data.Closes, period, pair.Indicator == "Wma");
        var declared = pair.OutputNames ?? ["Value"];
        if (!declared.OrderBy(key => key, StringComparer.Ordinal).SequenceEqual(expected.Outputs.Keys.OrderBy(key => key, StringComparer.Ordinal)))
            throw new InvalidOperationException(pair.Id + ": independent reference does not cover every declared output.");
        var competitorExpected = pair.CompetitorReference?.Invoke(data, period) ?? expected;
        if (!declared.OrderBy(key => key, StringComparer.Ordinal).SequenceEqual(competitorExpected.Outputs.Keys.OrderBy(key => key, StringComparer.Ordinal)))
            throw new InvalidOperationException(pair.Id + ": competitor reference does not cover every declared output.");
        var ours = pair.Ooples(data, period);
        var theirs = pair.Competitor(data, period);
        Compare(expected, ours, pair.Id + " Ooples", pair.ErrorBudget);
        Compare(competitorExpected, theirs, pair.Id + " competitor", pair.ErrorBudget);
        if (!verifyIsolation) return CountValues(expected);
        // Retained results must not alias later invocations; every measured arm returns every value.
        var retainedOurs = Snapshot(ours);
        var retainedTheirs = Snapshot(theirs);
        Compare(expected, pair.Ooples(data, period), pair.Id + " repeated Ooples", pair.ErrorBudget);
        Compare(competitorExpected, pair.Competitor(data, period), pair.Id + " repeated competitor", pair.ErrorBudget);
        RequireUnchanged(retainedOurs, ours, pair.Id);
        RequireUnchanged(retainedTheirs, theirs, pair.Id);
        foreach (var output in ours.Outputs.Values) Poison(output);
        foreach (var output in theirs.Outputs.Values) Poison(output);
        Compare(expected, pair.Ooples(data, period), pair.Id + " poisoned Ooples", pair.ErrorBudget);
        Compare(competitorExpected, pair.Competitor(data, period), pair.Id + " poisoned competitor", pair.ErrorBudget);
        return CountValues(expected);
    }

    private static int CountValues(ComparisonSeries series) => series.Outputs.Values.Sum(output =>
        output.Present is null ? output.Values.Length - output.FirstValid : output.Present.Skip(output.FirstValid).Count(present => present));

    private static void Poison(ComparisonOutput output)
    {
        Array.Fill(output.Values, double.NaN);
        if (output.Present is not null)
            for (var i = 0; i < output.Present.Length; i++) output.Present[i] = !output.Present[i];
    }

    private static ComparisonSeries Snapshot(ComparisonSeries series) => new(series.Outputs.ToDictionary(
        pair => pair.Key, pair => new ComparisonOutput(pair.Value.FirstValid, (double[])pair.Value.Values.Clone(),
            pair.Value.Present is null ? null : (bool[])pair.Value.Present.Clone()), StringComparer.Ordinal));

    private static void RequireUnchanged(ComparisonSeries retained, ComparisonSeries actual, string context)
    {
        if (retained.Outputs.Count != actual.Outputs.Count || retained.Outputs.Any(pair =>
            !actual.Outputs.TryGetValue(pair.Key, out var output) || output.FirstValid != pair.Value.FirstValid ||
            !output.Values.SequenceEqual(pair.Value.Values) ||
            (output.Present is null) != (pair.Value.Present is null) ||
            output.Present is not null && !output.Present.SequenceEqual(pair.Value.Present!)))
            throw new InvalidOperationException(context + " mutated a retained result.");
    }

    internal static ComparisonSeries Reference(double[] input, int period, bool weighted)
    {
        var values = Enumerable.Repeat(double.NaN, input.Length).ToArray();
        for (var index = period - 1; index < input.Length; index++)
        {
            decimal sum = 0;
            decimal weights = 0;
            for (var offset = 0; offset < period; offset++)
            {
                var weight = weighted ? offset + 1 : 1;
                sum += (decimal)input[index - period + 1 + offset] * weight;
                weights += weight;
            }
            values[index] = (double)(sum / weights);
        }
        return new(Math.Min(period - 1, input.Length), values);
    }

    private static void ValidateShape(ComparisonOutput output, string context)
    {
        if (output.FirstValid < 0 || output.FirstValid > output.Values.Length ||
            output.Present is not null && output.Present.Length != output.Values.Length)
            throw new InvalidOperationException(context + ": invalid output shape.");
    }

    internal static void Compare(ComparisonSeries expected, ComparisonSeries actual, string context,
        OoplesFinance.StockIndicators.Validation.IndicatorErrorBudget? errorBudget = null)
    {
        if (expected.Outputs.Count != actual.Outputs.Count || expected.Outputs.Keys.Except(actual.Outputs.Keys, StringComparer.Ordinal).Any())
            throw new InvalidOperationException(context + ": output names differ.");
        foreach (var (name, wantedOutput) in expected.Outputs)
        {
            var actualOutput = actual.Outputs[name];
            ValidateShape(wantedOutput, context + " reference/" + name);
            ValidateShape(actualOutput, context + "/" + name);
            if (actualOutput.Values.Length != wantedOutput.Values.Length || actualOutput.FirstValid != wantedOutput.FirstValid)
                throw new InvalidOperationException(context + "/" + name + ": output alignment differs.");
            for (var index = wantedOutput.FirstValid; index < wantedOutput.Values.Length; index++)
            {
                var value = actualOutput.Values[index];
                var wanted = wantedOutput.Values[index];
                var wantedPresent = wantedOutput.Present?[index] ?? true;
                var actualPresent = actualOutput.Present?[index] ?? true;
                if (wantedPresent != actualPresent)
                    throw new InvalidOperationException($"{context}/{name}, bar {index}: output presence differs.");
                if (!wantedPresent)
                {
                    if (!double.IsNaN(wanted) || !double.IsNaN(value))
                        throw new InvalidOperationException($"{context}/{name}, bar {index}: absent values must use the NaN placeholder.");
                    continue;
                }
                if (!double.IsFinite(wanted) || !double.IsFinite(value) || (errorBudget is null ? Math.Abs(value - wanted) > 1e-10 + 1e-12 * Math.Abs(wanted) : !errorBudget.Accepts(wanted, value)))
                    throw new InvalidOperationException($"{context}/{name}, bar {index}: expected {wanted:R}, got {value:R}.");
            }
        }
    }
}
