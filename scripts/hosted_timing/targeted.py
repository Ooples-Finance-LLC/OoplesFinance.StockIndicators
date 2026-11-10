"""Bound each arithmetic batch to complete mapped consumers and an accepted baseline."""
import hashlib
import json
import subprocess

from .pipeline import BENCH, OWNED, REPO, TESTS, git


FIXTURES = {
    'SharedHilbertBenchmarks': {'TaLib.Functions.HtDcPeriod', 'TaLib.Functions.HtPhasor', 'TaLib.Functions.Mama'},
    'SharedTaHilbertNativeBenchmarks': {'TaLib.Functions.HtDcPhase', 'TaLib.Functions.HtSine', 'TaLib.Functions.HtTrendline'},
    'SharedTaHilbertTrendModeBenchmarks': {'TaLib.Functions.HtTrendMode'},
    'SharedSeededPhaseBenchmarks': {'QuanTAlib.Mama', 'Skender.GetMama'},
    'SharedHilbertTrendlineBenchmarks': {'Skender.GetHtTrendline'},
}

SCOPES = {
    'hilbert-fir': dict(fixtures={f:{p:p for p in pairs} for f,pairs in FIXTURES.items()},
        paths={'src/Helpers/HilbertRoundedKernel.cs', 'src/Indicators/HilbertPhaseState.cs',
               'tests/ModelsTests/HilbertRoundedKernelTests.cs'}, totals=(300,60)),
    'ad-cumulative': dict(fixtures={
        'SharedRecursiveStateBenchmarks': {'TaLib.Functions.Ad':'Ad'},
        'SharedAdlDetailsBenchmarks': {'Skender.GetAdl':'Skender.GetAdl'},
        'SharedTradyNativeVolumeBenchmarks': {'Trady.Indicator.AccumulationDistributionLine':'AccumulationDistributionLine'}},
        paths={'src/Helpers/CompactAccumulationSum.cs', 'src/Helpers/MoneyFlowAccumulationWindow.cs',
               'src/Indicators/AccumulationDistributionWithAverage.cs',
               'src/Indicators/PriceAdjustedAccumulationDistribution.cs',
               'src/Indicators/ValuesBarExecution.AccumulationDistribution.cs',
               'tests/ModelsTests/CompactAccumulationSumTests.cs'}, totals=(90,18)),
    'klinger-schaff': dict(fixtures={
        'SharedKlingerBenchmarks': {'Skender.GetKvo':'Skender.GetKvo'},
        'SharedSchaffCycleBenchmarks': {'Skender.GetStc':'Skender.GetStc'}},
        paths={'src/Helpers/CompactMacdValue.cs', 'src/Indicators/EmaDifferenceSignal.cs',
               'src/Indicators/KlingerVolumeWithSignal.cs', 'src/Indicators/SingleStochasticCycle.cs',
               'tests/ModelsTests/CompactKlingerSchaffEmaTests.cs'}, totals=(60,12)),
    'rolling-dispersion': dict(fixtures={
        'SharedNativeDispersionBenchmarks': {p:p for p in ('QuanTAlib.Variance','QuanTAlib.Stddev',
            'QuanTAlib.Zscore','Trady.Indicator.StandardDeviation')},
        'SharedDispersionBenchmarks': {'TaLib.Functions.Var':'True','TaLib.Functions.StdDev':'False'},
        'SharedDetailedDeviationBenchmarks': {'Skender.GetStdDev':'Skender.GetStdDev'},
        'SharedClassicBandsBenchmarks': {'TaLib.Functions.Bbands':'TaLib.Functions.Bbands'},
        'SharedDeviationBandsBenchmarks': {'Skender.GetBollingerBands':'Skender.GetBollingerBands'},
        'SharedTradyBandsBenchmarks': {p:p for p in ('Trady.Indicator.BollingerBands','Trady.Indicator.BollingerBandWidth')}},
        paths={'src/Indicators/WindowDispersion.cs','tests/ModelsTests/CompactDispersionMomentsTests.cs'},
        totals=(330,66)),
}


SCOPES['ad-small-first'] = dict(
    fixtures=SCOPES['ad-cumulative']['fixtures'],
    paths=SCOPES['ad-cumulative']['paths'] | {'src/Helpers/ExactMeanAccumulator.cs'},
    totals=(90,18))

SCOPES['combined-hilbert-klinger'] = dict(
    fixtures={**SCOPES['hilbert-fir']['fixtures'], **SCOPES['klinger-schaff']['fixtures']},
    paths=SCOPES['hilbert-fir']['paths'] | SCOPES['klinger-schaff']['paths'],
    totals=(360,72), shards=6)

SCOPES['tsi-compact-ema'] = dict(
    fixtures={'SharedSkenderEmaFamilyBenchmarks': {'Skender.GetTsi':'Skender.GetTsi'}},
    paths={'src/Indicators/SeededTrueStrength.cs', 'tests/ModelsTests/SeededTrueStrengthExecutionTests.cs'},
    totals=(30,6), baseline_phase='targeted', baseline_totals=(360,72))

SCOPES['pmo-compact-ema'] = dict(
    fixtures={'SharedSkenderEmaFamilyBenchmarks': {'Skender.GetPmo':'Skender.GetPmo'}},
    paths={'src/Indicators/SeededPriceMomentum.cs', 'tests/ModelsTests/SeededPriceMomentumExecutionTests.cs'},
    totals=(30,6), baseline_phase='targeted', baseline_totals=(30,6))

SCOPES['quartic-interval'] = dict(
    fixtures={'SharedQuarticDynamicBenchmarks': {p:p for p in ('QuanTAlib.Mgdi','Skender.GetDynamic')}},
    paths={'src/Indicators/ValuesBarExecution.QuarticAverage.cs',
           'tests/ModelsTests/QuarticSharedExecutionTests.cs'},
    totals=(60,12), baseline_phase='targeted', baseline_totals=(30,6))

SCOPES['combined-compact-kernels'] = dict(
    fixtures={
        'SharedDeviationAverageBenchmarks': {'QuanTAlib.Dsma':'QuanTAlib.Dsma'},
        'SharedTaWindowNativeBenchmarks': {'TaLib.Functions.Accbands':'TaLib.Functions.Accbands'}},
    implicit_fixtures={'SharedDeviationAverageBenchmarks'},
    paths={'src/Indicators/ValuesBarExecution.DeviationAverage.cs',
           'tests/ModelsTests/DeviationAverageSharedExecutionTests.cs',
           'src/Indicators/ValuesBarExecution.Acceleration.cs',
           'tests/ModelsTests/AccelerationSharedExecutionTests.cs'},
    totals=(60,12), baseline_phase='targeted', baseline_totals=(60,12))


SCOPES['stoch-compact-smoothing'] = dict(
    fixtures={'SharedSkenderStochasticBenchmarks': {'Skender.GetStoch':'Skender.GetStoch'}},
    implicit_fixtures=set(),
    paths={'src/Indicators/ValuesBarExecution.SmoothedStochastic.cs',
           'tests/ModelsTests/SmoothedStochasticSharedExecutionTests.cs'},
    totals=(30,6), baseline_phase='targeted', baseline_totals=(60,12))


SCOPES['sum-directional-range'] = dict(
    fixtures={'SharedSkenderStrengthBenchmarks': {'Skender.GetAdx':'Skender.GetAdx'}},
    implicit_fixtures=set(),
    paths={'src/Indicators/SumSeededDirectionalIndex.cs',
           'tests/ModelsTests/SumDirectionalExecutionTests.cs'},
    totals=(30,6), baseline_phase='targeted', baseline_totals=(60,12))


SCOPES['trend-star-compact-ranks'] = dict(
    fixtures={'SharedTrendReversalBenchmarks': {
        'Trady.Candlestick.'+name:name for name in ('MorningStar','EveningStar',
            'MorningDojiStar','EveningDojiStar','MoringinDojiStar')}},
    implicit_fixtures=set(),
    paths={'src/Indicators/TrendStarPattern.cs',
           'tests/ModelsTests/SharedTrendReversalTests.cs'},
    totals=(150,30), baseline_phase='targeted', baseline_totals=(60,12))


SCOPES['stoch-raw-certificate'] = dict(
    fixtures={'SharedSkenderStochasticBenchmarks': {'Skender.GetStoch':'Skender.GetStoch'}},
    implicit_fixtures=set(),
    paths={'src/Indicators/ValuesBarExecution.SmoothedStochastic.cs',
           'tests/ModelsTests/StochasticRawRatioCertificateTests.cs'},
    totals=(30,6), baseline_phase='targeted', baseline_totals=(30,6))


SCOPES['trend-star-certified-bodies'] = {
    **SCOPES['trend-star-compact-ranks'], 'baseline_totals': (30,6)}


for name, members, shards in (
    ('combined-local-directional', ('sum-directional-range',), 2),
    ('combined-local-directional-body', ('sum-directional-range','trend-star-certified-bodies'), 6),
):
    SCOPES[name] = dict(
        fixtures={fixture:mapping for member in members for fixture,mapping in SCOPES[member]['fixtures'].items()},
        implicit_fixtures=set(),
        paths=set().union(*(SCOPES[member]['paths'] for member in members)),
        totals=tuple(sum(SCOPES[member]['totals'][i] for member in members) for i in (0,1)),
        shards=shards, baseline_phase='targeted', baseline_totals=(30,6))


SCOPES['curvature-wide-slope'] = dict(
    fixtures={'SharedCurvatureBenchmarks': {'QuanTAlib.Curvature':'QuanTAlib.Curvature'}},
    implicit_fixtures={'SharedCurvatureBenchmarks'},
    paths={'src/Indicators/ValuesBarExecution.Curvature.cs', 'tests/ModelsTests/CurvatureSharedExecutionTests.cs'},
    totals=(30,6), baseline_phase='targeted', baseline_totals=(180,36))


SCOPES['curvature-packed-statistics'] = dict(
    fixtures=SCOPES['curvature-wide-slope']['fixtures'],
    implicit_fixtures={'SharedCurvatureBenchmarks'},
    paths={'src/Indicators/ValuesBarExecution.Curvature.cs',
           'src/Indicators/ValuesBarExecution.CurvatureWide.cs',
           'src/Indicators/ValuesBarExecution.CurvaturePacked.cs',
           'tests/ModelsTests/CurvatureSharedExecutionTests.cs'},
    totals=(30,6), baseline_phase='targeted', baseline_totals=(30,6))


SCOPES['envelope-coefficient-cache'] = dict(
    fixtures={'SharedSkenderDiagnosticsBenchmarks': {'Skender.GetMaEnvelopes':'Skender.GetMaEnvelopes'}},
    implicit_fixtures=set(),
    paths={'src/Indicators/WindowAverageEnvelope.cs'},
    totals=(30,6), baseline_phase='targeted', baseline_totals=(30,6))


SCOPES['pvo-certified-ema'] = dict(
    fixtures={
        'SharedSkenderEmaFamilyBenchmarks': {p:p for p in ('Skender.GetMacd', 'Skender.GetPvo')},
        'SharedTradyMacdBenchmarks': {'Trady.Indicator.'+p:p for p in (
            'MovingAverageConvergenceDivergence', 'MovingAverageConvergenceDivergenceHistogram')}},
    implicit_fixtures=set(),
    paths={'src/Indicators/EmaDifferenceSignal.cs', 'tests/ModelsTests/PvoCertifiedAverageTests.cs'},
    totals=(120,24), shards=3, baseline_phase='targeted', baseline_totals=(30,6))


SCOPES['envelope-band-certificate'] = dict(
    fixtures={'SharedSkenderDiagnosticsBenchmarks': {'Skender.GetMaEnvelopes':'Skender.GetMaEnvelopes'}},
    implicit_fixtures=set(),
    paths={'src/Indicators/WindowAverageEnvelope.cs', 'tests/ModelsTests/EnvelopeBandCertificateTests.cs'},
    totals=(30,6), baseline_phase='targeted', baseline_totals=(30,6))


SCOPES['pvo-volume-dispatch'] = dict(SCOPES['pvo-certified-ema'])



SCOPES['adx-weighted-certificate'] = dict(
    fixtures={'SharedSkenderStrengthBenchmarks': {'Skender.GetAdx':'Skender.GetAdx'}},
    implicit_fixtures=set(),
    paths={'src/Indicators/SumSeededDirectionalIndex.cs', 'tests/ModelsTests/AdxWeightedCertificateTests.cs'},
    totals=(30,6), baseline_phase='targeted', baseline_totals=(30, 6))



SCOPES['fisher-exact-inputs'] = dict(
    fixtures={'SharedFisherBenchmarks': {'Skender.GetFisherTransform':'Skender.GetFisherTransform'}},
    implicit_fixtures=set(),
    paths={'src/Indicators/WindowFisherTransform.cs', 'tests/ModelsTests/FisherExactInputsTests.cs'},
    totals=(30,6), baseline_phase='targeted', baseline_totals=(30, 6))


SCOPES['combined-private-inputs'] = {'fixtures': {'SharedSkenderStrengthBenchmarks': {'Skender.GetAdx': 'Skender.GetAdx'}, 'SharedFisherBenchmarks': {'Skender.GetFisherTransform': 'Skender.GetFisherTransform'}, 'SharedSkenderEmaFamilyBenchmarks': {'Skender.GetMacd': 'Skender.GetMacd', 'Skender.GetPvo': 'Skender.GetPvo'}, 'SharedTradyMacdBenchmarks': {'Trady.Indicator.MovingAverageConvergenceDivergence': 'MovingAverageConvergenceDivergence', 'Trady.Indicator.MovingAverageConvergenceDivergenceHistogram': 'MovingAverageConvergenceDivergenceHistogram'}}, 'implicit_fixtures': set(), 'paths': {'src/Indicators/WindowFisherTransform.cs', 'src/Indicators/SumSeededDirectionalIndex.cs', 'tests/ModelsTests/AdxWeightedCertificateTests.cs', 'src/Indicators/EmaDifferenceSignal.cs', 'tests/ModelsTests/FisherExactInputsTests.cs', 'tests/ModelsTests/PvoCertifiedAverageTests.cs'}, 'totals': (180, 36), 'shards': 6, 'baseline_phase': 'targeted', 'baseline_totals': (30, 6)}



SCOPES['awesome-private-arithmetic'] = dict(
    fixtures={'SharedSkenderFiniteWindowBenchmarks': {'Skender.GetAwesome':'Skender.GetAwesome'}},
    implicit_fixtures=set(),
    paths={'src/Indicators/AwesomeWithDetails.cs', 'tests/ModelsTests/AwesomeArithmeticCertificateTests.cs'},
    totals=(30,6), baseline_phase='targeted', baseline_totals=(180, 36))



SCOPES['crow-soldier-active-snapshots'] = dict(
    fixtures={'SharedTaReversalContinuationBenchmarks': {
        'TaLib.Candles.ThreeBlackCrows':'ThreeBlackCrows',
        'TaLib.Candles.IdenticalThreeCrows':'IdenticalThreeCrows',
        'TaLib.Candles.ThreeWhiteSoldiers':'ThreeWhiteSoldiers'}},
    implicit_fixtures=set(), paths={'src/Indicators/CrowSoldierCandles.cs'},
    totals=(90,18), baseline_phase='targeted', baseline_totals=(30,6))



SCOPES['prior-directional-weighted-certificate'] = dict(
    fixtures={'SharedTaDirectionalBenchmarks': {p:p for p in (
        'TaLib.Functions.PlusDM','TaLib.Functions.MinusDM','TaLib.Functions.PlusDI',
        'TaLib.Functions.MinusDI','TaLib.Functions.Dx','TaLib.Functions.Adx','TaLib.Functions.Adxr')}},
    implicit_fixtures=set(), paths={'src/Indicators/PriorSeededDirectionalMeasure.cs',
        'tests/ModelsTests/PriorDirectionalWeightedCertificateTests.cs'},
    totals=(210,42), baseline_phase='targeted', baseline_totals=(30,6))



SCOPES['smoothed-accumulation-certified-ema'] = dict(
    fixtures={'SharedSmoothedAccumulationBenchmarks': {p:p for p in ('Skender.GetChaikinOsc','TaLib.Functions.AdOsc')}},
    implicit_fixtures=set(), paths={'src/Indicators/SmoothedAccumulationOscillator.cs'},
    totals=(60,12), baseline_phase='targeted', baseline_totals=(30,6))


SCOPES['combined-directional-accumulation'] = {'fixtures': {'SharedSmoothedAccumulationBenchmarks': {'Skender.GetChaikinOsc': 'Skender.GetChaikinOsc', 'TaLib.Functions.AdOsc': 'TaLib.Functions.AdOsc'}, 'SharedTaDirectionalBenchmarks': {'TaLib.Functions.PlusDM': 'TaLib.Functions.PlusDM', 'TaLib.Functions.MinusDM': 'TaLib.Functions.MinusDM', 'TaLib.Functions.PlusDI': 'TaLib.Functions.PlusDI', 'TaLib.Functions.MinusDI': 'TaLib.Functions.MinusDI', 'TaLib.Functions.Dx': 'TaLib.Functions.Dx', 'TaLib.Functions.Adx': 'TaLib.Functions.Adx', 'TaLib.Functions.Adxr': 'TaLib.Functions.Adxr'}}, 'implicit_fixtures': set(), 'paths': {'src/Indicators/PriorSeededDirectionalMeasure.cs', 'tests/ModelsTests/PriorDirectionalWeightedCertificateTests.cs', 'src/Indicators/SmoothedAccumulationOscillator.cs'}, 'totals': (270, 54), 'shards': 9, 'baseline_phase': 'targeted', 'baseline_totals': (30, 6)}



SCOPES['prior-directional-raw-stages'] = dict(
    fixtures={'SharedTaDirectionalBenchmarks': {p:p for p in (
        'TaLib.Functions.PlusDM','TaLib.Functions.MinusDM','TaLib.Functions.PlusDI',
        'TaLib.Functions.MinusDI','TaLib.Functions.Dx','TaLib.Functions.Adx','TaLib.Functions.Adxr')}},
    implicit_fixtures=set(), paths={'src/Indicators/PriorSeededDirectionalMeasure.cs',
        'tests/ModelsTests/PriorDirectionalRawStageTests.cs'},
    totals=(210,42), baseline_phase='targeted', baseline_totals=(270,54))



SCOPES['prior-directional-ratios'] = dict(
    fixtures={'SharedTaDirectionalBenchmarks': {p:p for p in (
        'TaLib.Functions.PlusDM','TaLib.Functions.MinusDM','TaLib.Functions.PlusDI',
        'TaLib.Functions.MinusDI','TaLib.Functions.Dx','TaLib.Functions.Adx','TaLib.Functions.Adxr')}},
    implicit_fixtures=set(), paths={'src/Indicators/PriorSeededDirectionalMeasure.cs',
        'tests/ModelsTests/PriorDirectionalRatioTests.cs'},
    totals=(210,42), baseline_phase='targeted', baseline_totals=(210,42))


SCOPES['klinger-private-blend'] = dict(
    fixtures={'SharedKlingerBenchmarks': {'Skender.GetKvo':'Skender.GetKvo'}},
    implicit_fixtures=set(), paths={'src/Indicators/KlingerVolumeWithSignal.cs',
        'tests/ModelsTests/KlingerPrivateBlendTests.cs'},
    totals=(30,6), baseline_phase='targeted', baseline_totals=(210,42))


SCOPES['combined-ratio-klinger'] = {'fixtures': {'SharedKlingerBenchmarks': {'Skender.GetKvo': 'Skender.GetKvo'}, 'SharedTaDirectionalBenchmarks': {'TaLib.Functions.PlusDM': 'TaLib.Functions.PlusDM', 'TaLib.Functions.MinusDM': 'TaLib.Functions.MinusDM', 'TaLib.Functions.PlusDI': 'TaLib.Functions.PlusDI', 'TaLib.Functions.MinusDI': 'TaLib.Functions.MinusDI', 'TaLib.Functions.Dx': 'TaLib.Functions.Dx', 'TaLib.Functions.Adx': 'TaLib.Functions.Adx', 'TaLib.Functions.Adxr': 'TaLib.Functions.Adxr'}}, 'implicit_fixtures': set(), 'paths': {'tests/ModelsTests/KlingerPrivateBlendTests.cs', 'tests/ModelsTests/PriorDirectionalRatioTests.cs', 'src/Indicators/KlingerVolumeWithSignal.cs', 'src/Indicators/PriorSeededDirectionalMeasure.cs'}, 'totals': (240, 48), 'shards': 8, 'baseline_phase': 'targeted', 'baseline_totals': (210, 42)}


SCOPES['klinger-after-directional-ratios'] = {'fixtures': {'SharedKlingerBenchmarks': {'Skender.GetKvo': 'Skender.GetKvo'}, 'SharedTaDirectionalBenchmarks': {'TaLib.Functions.Adx': 'TaLib.Functions.Adx', 'TaLib.Functions.Adxr': 'TaLib.Functions.Adxr', 'TaLib.Functions.Dx': 'TaLib.Functions.Dx', 'TaLib.Functions.MinusDI': 'TaLib.Functions.MinusDI', 'TaLib.Functions.MinusDM': 'TaLib.Functions.MinusDM', 'TaLib.Functions.PlusDI': 'TaLib.Functions.PlusDI', 'TaLib.Functions.PlusDM': 'TaLib.Functions.PlusDM'}}, 'implicit_fixtures': set(), 'paths': {'tests/ModelsTests/KlingerPrivateBlendTests.cs', 'src/Indicators/KlingerVolumeWithSignal.cs'}, 'totals': (240, 48), 'shards': 8, 'baseline_phase': 'targeted', 'baseline_totals': (210, 42)}


SCOPES['classic-ppo-normalization'] = {'fixtures': {'SharedTaAverageNativeBenchmarks': {'TaLib.Functions.Apo': 'TaLib.Functions.Apo', 'TaLib.Functions.Ppo': 'TaLib.Functions.Ppo'}}, 'implicit_fixtures': set(), 'paths': {'src/Indicators/ClassicPriceOscillator.cs', 'tests/ModelsTests/ClassicPpoNormalizationTests.cs'}, 'totals': (60, 12), 'shards': 2, 'baseline_phase': 'targeted', 'baseline_totals': (240, 48)}


SCOPES['log-return-short-certificate'] = {'fixtures': {'SharedLogVolatilityBenchmarks': {'QuanTAlib.Historical': 'QuanTAlib.Historical', 'QuanTAlib.Realized': 'QuanTAlib.Realized'}}, 'implicit_fixtures': set(), 'paths': {'src/Helpers/CertifiedLogReturn.cs', 'tests/ModelsTests/ShortLogReturnCertificateTests.cs', 'src/Helpers/CertifiedLogReturn.Short.cs'}, 'totals': (60, 12), 'shards': 2, 'baseline_phase': 'targeted', 'baseline_totals': (60, 12)}


SCOPES['classic-oscillator-finite-inputs'] = {'fixtures': {'SharedTaAverageNativeBenchmarks': {'TaLib.Functions.Apo': 'TaLib.Functions.Apo', 'TaLib.Functions.Ppo': 'TaLib.Functions.Ppo'}}, 'implicit_fixtures': set(), 'paths': {'tests/ModelsTests/ClassicOscillatorFiniteInputTests.cs', 'src/Indicators/ClassicPriceOscillator.cs'}, 'totals': (60, 12), 'shards': 2, 'baseline_phase': 'targeted', 'baseline_totals': (60, 12)}


SCOPES['sum-directional-normalization'] = {'fixtures': {'SharedSkenderStrengthBenchmarks': {'Skender.GetAdx': 'Skender.GetAdx'}}, 'implicit_fixtures': set(), 'paths': {'src/Indicators/SumSeededDirectionalIndex.cs'}, 'totals': (30, 6), 'shards': 2, 'baseline_phase': 'targeted', 'baseline_totals': (60, 12)}


SCOPES['pvo-normalization'] = {'fixtures': {'SharedSkenderEmaFamilyBenchmarks': {'Skender.GetMacd': 'Skender.GetMacd', 'Skender.GetPvo': 'Skender.GetPvo'}, 'SharedTradyMacdBenchmarks': {'Trady.Indicator.MovingAverageConvergenceDivergence': 'MovingAverageConvergenceDivergence', 'Trady.Indicator.MovingAverageConvergenceDivergenceHistogram': 'MovingAverageConvergenceDivergenceHistogram'}}, 'implicit_fixtures': set(), 'paths': {'src/Indicators/EmaDifferenceSignal.cs', 'tests/ModelsTests/PvoNormalizationTests.cs'}, 'totals': (120, 24), 'shards': 4, 'baseline_phase': 'targeted', 'baseline_totals': (60, 12)}


SCOPES['seeded-hilbert-midpoint'] = {'fixtures': {'SharedHilbertTrendlineBenchmarks': {'Skender.GetHtTrendline': 'Skender.GetHtTrendline'}}, 'implicit_fixtures': set(), 'paths': {'src/Indicators/SeededHilbertTrendline.cs', 'tests/ModelsTests/SeededHilbertMidpointTests.cs'}, 'totals': (30, 6), 'shards': 2, 'baseline_phase': 'targeted', 'baseline_totals': (30, 6)}


SCOPES['pvo-adjacent-certificate'] = {'fixtures': {'SharedSkenderEmaFamilyBenchmarks': {'Skender.GetMacd': 'Skender.GetMacd', 'Skender.GetPvo': 'Skender.GetPvo'}, 'SharedTradyMacdBenchmarks': {'Trady.Indicator.MovingAverageConvergenceDivergence': 'MovingAverageConvergenceDivergence', 'Trady.Indicator.MovingAverageConvergenceDivergenceHistogram': 'MovingAverageConvergenceDivergenceHistogram'}}, 'implicit_fixtures': set(), 'paths': {'tests/ModelsTests/PvoAdjacentCertificateTests.cs', 'src/Indicators/EmaDifferenceSignal.cs'}, 'totals': (120, 24), 'shards': 4, 'baseline_phase': 'targeted', 'baseline_totals': (30, 6)}


SCOPES['beta-derived-ratio'] = {'fixtures': {'SharedSkenderStatisticsBenchmarks': {'Skender.GetBeta': 'Skender.GetBeta'}}, 'implicit_fixtures': set(), 'paths': {'src/Indicators/WindowReturnBeta.cs', 'tests/ModelsTests/BetaDerivedRatioTests.cs'}, 'totals': (30, 6), 'shards': 2, 'baseline_phase': 'targeted', 'baseline_totals': (30, 6)}


SCOPES['atr-published-width'] = {'fixtures': {'SharedSkenderAtrFamilyBenchmarks': {'Skender.GetKeltner': 'Skender.GetKeltner', 'Skender.GetStarcBands': 'Skender.GetStarcBands'}, 'SharedTradyAtrChannelBenchmarks': {'Trady.Indicator.KeltnerChannels': 'KeltnerChannels'}}, 'implicit_fixtures': set(), 'paths': {'tests/ModelsTests/AtrPublishedWidthTests.cs', 'src/Indicators/WindowAtrBands.cs'}, 'totals': (90, 18), 'shards': 3, 'baseline_phase': 'targeted', 'baseline_totals': (30, 6)}


SCOPES['deviation-position-certificate'] = {'fixtures': {'SharedDeviationBandsBenchmarks': {'Skender.GetBollingerBands': 'Skender.GetBollingerBands'}, 'SharedTradyBandsBenchmarks': {'Trady.Indicator.BollingerBands': 'Trady.Indicator.BollingerBands', 'Trady.Indicator.BollingerBandWidth': 'Trady.Indicator.BollingerBandWidth'}}, 'implicit_fixtures': set(), 'paths': {'src/Indicators/WindowDeviationBands.cs', 'tests/ModelsTests/DeviationPositionCertificateTests.cs'}, 'totals': (90, 18), 'shards': 3, 'baseline_phase': 'targeted', 'baseline_totals': (90, 18)}


SCOPES['doji-shadow-comparison'] = {'fixtures': {'SharedRangeDojiBenchmarks': {'TaLib.Candles.Doji': 'TaLib.Candles.Doji', 'TaLib.Candles.DragonflyDoji': 'TaLib.Candles.DragonflyDoji', 'TaLib.Candles.GravestoneDoji': 'TaLib.Candles.GravestoneDoji', 'TaLib.Candles.LongLeggedDoji': 'TaLib.Candles.LongLeggedDoji', 'TaLib.Candles.TakuriLine': 'TaLib.Candles.TakuriLine'}}, 'implicit_fixtures': set(), 'paths': {'src/Indicators/AverageRangeDojiCandles.cs', 'tests/ModelsTests/DojiShadowComparisonTests.cs'}, 'totals': (150, 30), 'shards': 5, 'baseline_phase': 'targeted', 'baseline_totals': (90, 18)}


SCOPES['renko-price-grid'] = {'fixtures': {'SharedSkenderRenkoChartNativeBenchmarks': {'Skender.GetRenko': 'Skender.GetRenko', 'Skender.GetRenkoAtr': 'Skender.GetRenkoAtr'}}, 'implicit_fixtures': set(), 'paths': {'tests/ModelsTests/RenkoPriceGridTests.cs', 'src/Indicators/RenkoSnapshot.cs'}, 'methods': {'SharedSkenderRenkoChartNativeBenchmarks': ['Snapshot', 'NativeSingle', 'NativeParallel']}, 'totals': (44, 8), 'shards': 2, 'baseline_phase': 'targeted', 'baseline_totals': (90, 18)}


SCOPES['price-relative-compact-mean'] = {'fixtures': {'SharedSkenderDiagnosticsBenchmarks': {'Skender.GetPrs': 'Skender.GetPrs'}}, 'implicit_fixtures': set(), 'paths': {'src/Indicators/PriceRelativeStrength.cs', 'tests/ModelsTests/PriceRelativeMeanTests.cs'}, 'totals': (30, 6), 'shards': 2, 'baseline_phase': 'targeted', 'baseline_totals': (90, 18)}


SCOPES['volatility-stop-finite-stages'] = {'fixtures': {'SharedSkenderVolatilityStopNativeBenchmarks': {'Skender.GetVolatilityStop': 'Skender.GetVolatilityStop'}}, 'implicit_fixtures': set(), 'paths': {'src/Indicators/VolatilityStopSnapshot.cs', 'tests/ModelsTests/VolatilityStopFiniteStageTests.cs'}, 'methods': {'SharedSkenderVolatilityStopNativeBenchmarks': ['Snapshot', 'NativeSingle', 'NativeParallel']}, 'totals': (22, 4), 'shards': 2, 'baseline_phase': 'targeted', 'baseline_totals': (44, 8)}


SCOPES['combined-renko-mean-stop'] = {'fixtures': {'SharedSkenderRenkoChartNativeBenchmarks': {'Skender.GetRenko': 'Skender.GetRenko', 'Skender.GetRenkoAtr': 'Skender.GetRenkoAtr'}, 'SharedSkenderDiagnosticsBenchmarks': {'Skender.GetPrs': 'Skender.GetPrs'}, 'SharedSkenderVolatilityStopNativeBenchmarks': {'Skender.GetVolatilityStop': 'Skender.GetVolatilityStop'}}, 'methods': {'SharedSkenderRenkoChartNativeBenchmarks': ['Snapshot', 'NativeSingle', 'NativeParallel'], 'SharedSkenderVolatilityStopNativeBenchmarks': ['Snapshot', 'NativeSingle', 'NativeParallel']}, 'implicit_fixtures': set(), 'paths': {'src/Indicators/VolatilityStopSnapshot.cs', 'tests/ModelsTests/VolatilityStopFiniteStageTests.cs', 'src/Indicators/PriceRelativeStrength.cs', 'tests/ModelsTests/PriceRelativeMeanTests.cs'}, 'totals': (96, 18), 'shards': 4, 'baseline_phase': 'targeted', 'baseline_totals': (44, 8)}


SCOPES['renko-direct-coordinates'] = {'fixtures': {'SharedSkenderRenkoChartNativeBenchmarks': {'Skender.GetRenko': 'Skender.GetRenko', 'Skender.GetRenkoAtr': 'Skender.GetRenkoAtr'}}, 'implicit_fixtures': set(), 'paths': {'src/Indicators/RenkoSnapshot.cs', 'tests/ModelsTests/RenkoPriceGridTests.cs'}, 'methods': {'SharedSkenderRenkoChartNativeBenchmarks': ['Snapshot', 'NativeSingle', 'NativeParallel']}, 'totals': (44, 8), 'shards': 2, 'baseline_phase': 'targeted', 'baseline_totals': (96, 18)}


SCOPES['renko-compact-volume'] = {'fixtures': {'SharedSkenderRenkoChartNativeBenchmarks': {'Skender.GetRenko': 'Skender.GetRenko', 'Skender.GetRenkoAtr': 'Skender.GetRenkoAtr'}}, 'implicit_fixtures': set(), 'paths': {'tests/ModelsTests/RenkoCompactVolumeTests.cs', 'src/Indicators/RenkoSnapshot.cs'}, 'methods': {'SharedSkenderRenkoChartNativeBenchmarks': ['Snapshot', 'NativeSingle', 'NativeParallel']}, 'totals': (44, 8), 'shards': 2, 'baseline_phase': 'targeted', 'baseline_totals': (44, 8)}


SCOPES['renko-price-normalization'] = {'fixtures': {'SharedSkenderRenkoChartNativeBenchmarks': {'Skender.GetRenko': 'Skender.GetRenko', 'Skender.GetRenkoAtr': 'Skender.GetRenkoAtr'}}, 'implicit_fixtures': set(), 'paths': {'src/Indicators/RenkoSnapshot.cs', 'tests/ModelsTests/RenkoPriceNormalizationTests.cs'}, 'methods': {'SharedSkenderRenkoChartNativeBenchmarks': ['Snapshot', 'NativeSingle', 'NativeParallel']}, 'totals': (44, 8), 'shards': 2, 'baseline_phase': 'targeted', 'baseline_totals': (44, 8)}


SCOPES['renko-endpoint-cache'] = {'fixtures': {'SharedSkenderRenkoChartNativeBenchmarks': {'Skender.GetRenko': 'Skender.GetRenko', 'Skender.GetRenkoAtr': 'Skender.GetRenkoAtr'}}, 'implicit_fixtures': set(), 'paths': {'src/Indicators/RenkoSnapshot.cs', 'tests/ModelsTests/RenkoEndpointCacheTests.cs'}, 'methods': {'SharedSkenderRenkoChartNativeBenchmarks': ['Snapshot', 'NativeSingle', 'NativeParallel']}, 'totals': (44, 8), 'shards': 2, 'baseline_phase': 'targeted', 'baseline_totals': (44, 8)}


SCOPES['adl-component-sharing'] = {'fixtures': {'SharedAdlDetailsBenchmarks': {'Skender.GetAdl': 'Skender.GetAdl'}}, 'implicit_fixtures': set(), 'paths': {'tests/ModelsTests/AccumulationDistributionComponentSharingTests.cs', 'src/Indicators/ValuesBarExecution.AccumulationDistribution.cs'}, 'methods': {'SharedAdlDetailsBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (30, 6), 'shards': 1, 'baseline_phase': 'targeted', 'baseline_totals': (44, 8)}


SCOPES['adl-exact-product-fallback'] = {'fixtures': {'SharedAdlDetailsBenchmarks': {'Skender.GetAdl': 'Skender.GetAdl'}}, 'implicit_fixtures': set(), 'paths': {'tests/ModelsTests/AccumulationDistributionComponentSharingTests.cs', 'src/Indicators/ValuesBarExecution.AccumulationDistribution.cs'}, 'methods': {'SharedAdlDetailsBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (30, 6), 'shards': 1, 'baseline_phase': 'targeted', 'baseline_totals': (30, 6)}


SCOPES['zigzag-coordinate-normalization'] = {'fixtures': {'SharedSkenderZigZagChartNativeBenchmarks': {'Skender.GetZigZag': 'Skender.GetZigZag'}}, 'implicit_fixtures': set(), 'paths': {'src/Indicators/ZigZagSnapshot.cs', 'tests/ModelsTests/ZigZagExactScaleTests.cs'}, 'methods': {'SharedSkenderZigZagChartNativeBenchmarks': ['Snapshot', 'NativeSingle']}, 'totals': (20, 4), 'shards': 1, 'baseline_phase': 'targeted', 'baseline_totals': (30, 6)}


SCOPES['combined-containment-mfi-20261106'] = {'fixtures': {'SharedContainmentCandleBenchmarks': {'TaLib.Candles.Harami': 'TaLib.Candles.Harami', 'TaLib.Candles.HaramiCross': 'TaLib.Candles.HaramiCross', 'TaLib.Candles.HomingPigeon': 'TaLib.Candles.HomingPigeon', 'TaLib.Candles.DojiStar': 'TaLib.Candles.DojiStar'}, 'SharedMoneyFlowIndexBenchmarks': {'TaLib.Functions.Mfi': 'TaLib.Functions.Mfi', 'Skender.GetMfi': 'Skender.GetMfi'}}, 'implicit_fixtures': set(), 'paths': {'tests/ModelsTests/CompactMoneyFlowExecutionTests.cs', 'src/Indicators/ValuesBarExecution.MoneyFlow.cs', 'src/Indicators/ContainmentCandles.cs'}, 'methods': {'SharedContainmentCandleBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel'], 'SharedMoneyFlowIndexBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (180, 36), 'shards': 6, 'baseline_phase': 'targeted', 'baseline_totals': (20, 4)}


SCOPES['hilbert-variable-window-20261107'] = {'fixtures': {'SharedHilbertTrendlineBenchmarks': {'Skender.GetHtTrendline': 'Skender.GetHtTrendline'}, 'SharedTaHilbertNativeBenchmarks': {'TaLib.Functions.HtTrendline': 'TaLib.Functions.HtTrendline'}, 'SharedTaHilbertTrendModeBenchmarks': {'TaLib.Functions.HtTrendMode': 'TaLib.Functions.HtTrendMode'}}, 'implicit_fixtures': set(), 'paths': {'tests/ModelsTests/HilbertVariableWindowTests.cs', 'src/Indicators/HilbertTrendMeanState.cs'}, 'methods': {'SharedHilbertTrendlineBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel'], 'SharedTaHilbertNativeBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel'], 'SharedTaHilbertTrendModeBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (90, 18), 'shards': 3, 'baseline_phase': 'targeted', 'baseline_totals': (270, 54)}


SCOPES['hilbert-coefficient-certificates-20261107'] = {'fixtures': {'SharedTaHilbertNativeBenchmarks': {'TaLib.Functions.HtDcPhase': 'TaLib.Functions.HtDcPhase', 'TaLib.Functions.HtSine': 'TaLib.Functions.HtSine'}, 'SharedTaHilbertTrendModeBenchmarks': {'TaLib.Functions.HtTrendMode': 'TaLib.Functions.HtTrendMode'}}, 'implicit_fixtures': set(), 'paths': {'src/Helpers/CertifiedHilbertProjection.cs'}, 'methods': {'SharedTaHilbertNativeBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel'], 'SharedTaHilbertTrendModeBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (90, 18), 'shards': 3, 'baseline_phase': 'targeted', 'baseline_totals': (90, 18)}


SCOPES['hilbert-phase-only-trigonometry-20261107'] = {'fixtures': {'SharedTaHilbertNativeBenchmarks': {'TaLib.Functions.HtDcPhase': 'TaLib.Functions.HtDcPhase', 'TaLib.Functions.HtSine': 'TaLib.Functions.HtSine'}, 'SharedTaHilbertTrendModeBenchmarks': {'TaLib.Functions.HtTrendMode': 'TaLib.Functions.HtTrendMode'}}, 'implicit_fixtures': set(), 'paths': {'src/Indicators/HilbertCycleSignalState.cs'}, 'methods': {'SharedTaHilbertNativeBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel'], 'SharedTaHilbertTrendModeBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (90, 18), 'shards': 3, 'baseline_phase': 'targeted', 'baseline_totals': (90, 18)}


SCOPES['hilbert-followup-composition-20261107'] = {'fixtures': {'SharedTaHilbertNativeBenchmarks': {'TaLib.Functions.HtDcPhase': 'TaLib.Functions.HtDcPhase', 'TaLib.Functions.HtSine': 'TaLib.Functions.HtSine'}, 'SharedTaHilbertTrendModeBenchmarks': {'TaLib.Functions.HtTrendMode': 'TaLib.Functions.HtTrendMode'}}, 'implicit_fixtures': set(), 'paths': {'src/Indicators/HilbertCycleSignalState.cs', 'src/Helpers/CertifiedHilbertProjection.cs'}, 'methods': {'SharedTaHilbertNativeBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel'], 'SharedTaHilbertTrendModeBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (90, 18), 'shards': 3, 'baseline_phase': 'targeted', 'baseline_totals': (90, 18)}


SCOPES['hilbert-finite-bit-steps-20261107'] = {'fixtures': {'SharedTaHilbertNativeBenchmarks': {'TaLib.Functions.HtDcPhase': 'TaLib.Functions.HtDcPhase', 'TaLib.Functions.HtSine': 'TaLib.Functions.HtSine'}, 'SharedTaHilbertTrendModeBenchmarks': {'TaLib.Functions.HtTrendMode': 'TaLib.Functions.HtTrendMode'}}, 'implicit_fixtures': set(), 'paths': {'tests/ModelsTests/CertifiedHilbertProjectionTests.cs', 'src/Helpers/CertifiedHilbertProjection.cs'}, 'methods': {'SharedTaHilbertNativeBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel'], 'SharedTaHilbertTrendModeBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (90, 18), 'shards': 3, 'baseline_phase': 'targeted', 'baseline_totals': (90, 18)}


SCOPES['hilbert-residual-bound-20261107'] = {'fixtures': {'SharedTaHilbertNativeBenchmarks': {'TaLib.Functions.HtDcPhase': 'TaLib.Functions.HtDcPhase', 'TaLib.Functions.HtSine': 'TaLib.Functions.HtSine'}, 'SharedTaHilbertTrendModeBenchmarks': {'TaLib.Functions.HtTrendMode': 'TaLib.Functions.HtTrendMode'}}, 'implicit_fixtures': set(), 'paths': {'src/Helpers/CertifiedHilbertProjection.cs', 'tests/ModelsTests/CertifiedHilbertProjectionTests.cs'}, 'methods': {'SharedTaHilbertNativeBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel'], 'SharedTaHilbertTrendModeBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (90, 18), 'shards': 3, 'baseline_phase': 'targeted', 'baseline_totals': (90, 18)}


SCOPES['body-shadow-retained-differences-20261107'] = {'fixtures': {'SharedBodyShadowBenchmarks': {'TaLib.Candles.BeltHold': 'BeltHold', 'TaLib.Candles.Marubozu': 'Marubozu', 'TaLib.Candles.ClosingMarubozu': 'ClosingMarubozu', 'TaLib.Candles.SpinningTop': 'SpinningTop', 'TaLib.Candles.HighWave': 'HighWave', 'TaLib.Candles.LongLine': 'LongLine', 'TaLib.Candles.ShortLine': 'ShortLine'}, 'SharedContainmentCandleBenchmarks': {'TaLib.Candles.Harami': 'TaLib.Candles.Harami'}}, 'implicit_fixtures': set(), 'paths': {'src/Indicators/BodyShadowCandles.cs', 'tests/ModelsTests/RetainedBodyShadowTests.cs'}, 'methods': {'SharedBodyShadowBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel'], 'SharedContainmentCandleBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (240, 48), 'shards': 8, 'baseline_phase': 'targeted', 'baseline_totals': (90, 18)}


SCOPES['sum-directional-exact-movement-20261107'] = {'fixtures': {'SharedSkenderStrengthBenchmarks': {'Skender.GetAdx': 'Skender.GetAdx'}}, 'implicit_fixtures': set(), 'paths': {'src/Indicators/SumSeededDirectionalIndex.cs', 'tests/ModelsTests/SumDirectionalExecutionTests.cs'}, 'methods': {'SharedSkenderStrengthBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (30, 6), 'shards': 1, 'baseline_phase': 'targeted', 'baseline_totals': (90, 18)}


SCOPES['hilbert-accumulation-directional-composition-20261107'] = {'fixtures': {'SharedTaHilbertNativeBenchmarks': {'TaLib.Functions.HtDcPhase': 'TaLib.Functions.HtDcPhase', 'TaLib.Functions.HtSine': 'TaLib.Functions.HtSine'}, 'SharedTaHilbertTrendModeBenchmarks': {'TaLib.Functions.HtTrendMode': 'TaLib.Functions.HtTrendMode'}, 'SharedRecursiveStateBenchmarks': {'TaLib.Functions.Ad': 'Ad'}, 'SharedSmoothedAccumulationBenchmarks': {'TaLib.Functions.AdOsc': 'TaLib.Functions.AdOsc', 'Skender.GetChaikinOsc': 'Skender.GetChaikinOsc'}, 'SharedSkenderStrengthBenchmarks': {'Skender.GetAdx': 'Skender.GetAdx'}}, 'implicit_fixtures': set(), 'paths': {'src/Indicators/HilbertCycleSignalState.cs', 'tests/ModelsTests/CertifiedHilbertProjectionTests.cs', 'src/Indicators/SumSeededDirectionalIndex.cs', 'src/Helpers/CertifiedHilbertProjection.cs', 'tests/ModelsTests/CompactMoneyFlowPublicationTests.cs', 'tests/ModelsTests/SumDirectionalExecutionTests.cs', 'src/Helpers/MoneyFlowAccumulationWindow.cs', 'src/Helpers/ExactMeanAccumulator.cs'}, 'methods': {'SharedTaHilbertNativeBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel'], 'SharedTaHilbertTrendModeBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel'], 'SharedRecursiveStateBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel'], 'SharedSmoothedAccumulationBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel'], 'SharedSkenderStrengthBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (210, 42), 'shards': 7, 'baseline_phase': 'targeted', 'baseline_totals': (90, 18)}


SCOPES['body-shadow-compact-slot-20261108'] = {'fixtures': {'SharedBodyShadowBenchmarks': {'TaLib.Candles.BeltHold': 'BeltHold', 'TaLib.Candles.Marubozu': 'Marubozu', 'TaLib.Candles.ClosingMarubozu': 'ClosingMarubozu', 'TaLib.Candles.SpinningTop': 'SpinningTop', 'TaLib.Candles.HighWave': 'HighWave', 'TaLib.Candles.LongLine': 'LongLine', 'TaLib.Candles.ShortLine': 'ShortLine'}, 'SharedContainmentCandleBenchmarks': {'TaLib.Candles.Harami': 'TaLib.Candles.Harami'}}, 'implicit_fixtures': set(), 'paths': {'src/Indicators/BodyShadowCandles.cs', 'tests/ModelsTests/RetainedBodyShadowTests.cs'}, 'methods': {'SharedBodyShadowBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel'], 'SharedContainmentCandleBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (240, 48), 'shards': 8, 'baseline_phase': 'targeted', 'baseline_totals': (30, 6)}


SCOPES['adl-batch-wide-publication-20261108'] = {'fixtures': {'SharedRecursiveStateBenchmarks': {'TaLib.Functions.Ad': 'Ad'}, 'SharedSmoothedAccumulationBenchmarks': {'TaLib.Functions.AdOsc': 'TaLib.Functions.AdOsc', 'Skender.GetChaikinOsc': 'Skender.GetChaikinOsc'}}, 'implicit_fixtures': set(), 'paths': {'tests/ModelsTests/CompactMoneyFlowPublicationTests.cs', 'src/Helpers/ExactMeanAccumulator.cs', 'src/Indicators/ValuesBarExecution.GeneratedStates.cs', 'src/Helpers/MoneyFlowAccumulationWindow.cs'}, 'methods': {'SharedRecursiveStateBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel'], 'SharedSmoothedAccumulationBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (90, 18), 'shards': 3, 'baseline_phase': 'targeted', 'baseline_totals': (240, 48)}


SCOPES['hilbert-lag-zero-seed-20261108'] = {'fixtures': {'SharedTaHilbertNativeBenchmarks': {'TaLib.Functions.HtDcPhase': 'TaLib.Functions.HtDcPhase', 'TaLib.Functions.HtSine': 'TaLib.Functions.HtSine'}, 'SharedTaHilbertTrendModeBenchmarks': {'TaLib.Functions.HtTrendMode': 'TaLib.Functions.HtTrendMode'}}, 'implicit_fixtures': set(), 'paths': {'src/Helpers/CertifiedHilbertProjection.cs', 'tests/ModelsTests/CertifiedHilbertProjectionTests.cs'}, 'methods': {'SharedTaHilbertNativeBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel'], 'SharedTaHilbertTrendModeBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (90, 18), 'shards': 3, 'baseline_phase': 'targeted', 'baseline_totals': (90, 18)}


SCOPES['hilbert-ordered-sum-20261108'] = {'fixtures': {'SharedHilbertBenchmarks': {'TaLib.Functions.HtDcPeriod': 'TaLib.Functions.HtDcPeriod', 'TaLib.Functions.HtPhasor': 'TaLib.Functions.HtPhasor', 'TaLib.Functions.Mama': 'TaLib.Functions.Mama'}, 'SharedTaHilbertNativeBenchmarks': {'TaLib.Functions.HtDcPhase': 'TaLib.Functions.HtDcPhase', 'TaLib.Functions.HtSine': 'TaLib.Functions.HtSine', 'TaLib.Functions.HtTrendline': 'TaLib.Functions.HtTrendline'}, 'SharedTaHilbertTrendModeBenchmarks': {'TaLib.Functions.HtTrendMode': 'TaLib.Functions.HtTrendMode'}, 'SharedHilbertTrendlineBenchmarks': {'Skender.GetHtTrendline': 'Skender.GetHtTrendline'}, 'SharedSeededPhaseBenchmarks': {'QuanTAlib.Mama': 'QuanTAlib.Mama', 'Skender.GetMama': 'Skender.GetMama'}}, 'implicit_fixtures': set(), 'paths': {'src/Helpers/HilbertRoundedKernel.cs', 'tests/ModelsTests/HilbertOrderedSumTests.cs', 'src/Indicators/HilbertPhaseState.cs'}, 'methods': {'SharedHilbertBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel'], 'SharedTaHilbertNativeBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel'], 'SharedTaHilbertTrendModeBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel'], 'SharedHilbertTrendlineBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel'], 'SharedSeededPhaseBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (300, 60), 'shards': 10, 'baseline_phase': 'targeted', 'baseline_totals': (90, 18)}


SCOPES['hilbert-floating-grid-guard-20261108'] = {'fixtures': {'SharedTaHilbertNativeBenchmarks': {'TaLib.Functions.HtDcPhase': 'TaLib.Functions.HtDcPhase', 'TaLib.Functions.HtSine': 'TaLib.Functions.HtSine'}, 'SharedTaHilbertTrendModeBenchmarks': {'TaLib.Functions.HtTrendMode': 'TaLib.Functions.HtTrendMode'}}, 'implicit_fixtures': set(), 'paths': {'tests/ModelsTests/CertifiedHilbertProjectionTests.cs', 'src/Helpers/CertifiedHilbertProjection.cs'}, 'methods': {'SharedTaHilbertNativeBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel'], 'SharedTaHilbertTrendModeBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (90, 18), 'shards': 3, 'baseline_phase': 'targeted', 'baseline_totals': (90, 18)}


SCOPES['prior-directional-single-loop-20261108'] = {'fixtures': {'SharedTaDirectionalBenchmarks': {'TaLib.Functions.PlusDM': 'TaLib.Functions.PlusDM', 'TaLib.Functions.MinusDM': 'TaLib.Functions.MinusDM', 'TaLib.Functions.PlusDI': 'TaLib.Functions.PlusDI', 'TaLib.Functions.MinusDI': 'TaLib.Functions.MinusDI', 'TaLib.Functions.Dx': 'TaLib.Functions.Dx', 'TaLib.Functions.Adx': 'TaLib.Functions.Adx', 'TaLib.Functions.Adxr': 'TaLib.Functions.Adxr'}, 'SharedRecursiveStateBenchmarks': {'TaLib.Functions.Ema': 'Ema'}, 'SharedTaHilbertNativeBenchmarks': {'TaLib.Functions.HtSine': 'TaLib.Functions.HtSine'}}, 'implicit_fixtures': set(), 'paths': {'src/Indicators/ValuesBarExecution.cs', 'tests/ModelsTests/PriorDirectionalSingleLoopTests.cs'}, 'methods': {'SharedTaDirectionalBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel'], 'SharedRecursiveStateBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel'], 'SharedTaHilbertNativeBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (270, 54), 'shards': 9, 'baseline_phase': 'targeted', 'baseline_totals': (90, 18)}


SCOPES['prior-directional-lazy-factors-20261108'] = {'fixtures': {'SharedTaDirectionalBenchmarks': {'TaLib.Functions.PlusDM': 'TaLib.Functions.PlusDM', 'TaLib.Functions.MinusDM': 'TaLib.Functions.MinusDM', 'TaLib.Functions.PlusDI': 'TaLib.Functions.PlusDI', 'TaLib.Functions.MinusDI': 'TaLib.Functions.MinusDI', 'TaLib.Functions.Dx': 'TaLib.Functions.Dx', 'TaLib.Functions.Adx': 'TaLib.Functions.Adx', 'TaLib.Functions.Adxr': 'TaLib.Functions.Adxr'}}, 'implicit_fixtures': set(), 'paths': {'src/Indicators/PriorSeededDirectionalMeasure.cs', 'tests/ModelsTests/PriorDirectionalLazyFactorTests.cs'}, 'methods': {'SharedTaDirectionalBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (210, 42), 'shards': 7, 'baseline_phase': 'targeted', 'baseline_totals': (90, 18)}


SCOPES['prior-directional-dx-ratio-20261108'] = {'fixtures': {'SharedTaDirectionalBenchmarks': {'TaLib.Functions.PlusDM': 'TaLib.Functions.PlusDM', 'TaLib.Functions.MinusDM': 'TaLib.Functions.MinusDM', 'TaLib.Functions.PlusDI': 'TaLib.Functions.PlusDI', 'TaLib.Functions.MinusDI': 'TaLib.Functions.MinusDI', 'TaLib.Functions.Dx': 'TaLib.Functions.Dx', 'TaLib.Functions.Adx': 'TaLib.Functions.Adx', 'TaLib.Functions.Adxr': 'TaLib.Functions.Adxr'}}, 'implicit_fixtures': set(), 'paths': {'tests/ModelsTests/PriorDirectionalDxCertificateTests.cs', 'src/Indicators/PriorSeededDirectionalMeasure.cs'}, 'methods': {'SharedTaDirectionalBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (210, 42), 'shards': 7, 'baseline_phase': 'targeted', 'baseline_totals': (210, 42)}


SCOPES['trix-scalar-stage-storage-20261108'] = {'fixtures': {'SharedSkenderEmaFamilyBenchmarks': {'Skender.GetTrix': 'Skender.GetTrix'}, 'SharedTaAdaptiveRateNativeBenchmarks': {'TaLib.Functions.Trix': 'TaLib.Functions.Trix'}}, 'implicit_fixtures': set(), 'paths': {'src/Indicators/ValuesBarExecution.TripleRate.cs'}, 'methods': {'SharedSkenderEmaFamilyBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel'], 'SharedTaAdaptiveRateNativeBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (60, 12), 'shards': 2, 'baseline_phase': 'targeted', 'baseline_totals': (210, 42)}


SCOPES['trix-rate-certificate-20261109'] = {'fixtures': {'SharedSkenderEmaFamilyBenchmarks': {'Skender.GetTrix': 'Skender.GetTrix'}, 'SharedTaAdaptiveRateNativeBenchmarks': {'TaLib.Functions.Trix': 'TaLib.Functions.Trix'}}, 'implicit_fixtures': set(), 'paths': {'tests/ModelsTests/SeededMomentumSharedExecutionTests.cs', 'src/Indicators/ValuesBarExecution.TripleRate.cs'}, 'methods': {'SharedSkenderEmaFamilyBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel'], 'SharedTaAdaptiveRateNativeBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (60, 12), 'shards': 2, 'baseline_phase': 'targeted', 'baseline_totals': (60, 12)}


SCOPES['adl-batch-balanced-product-20261109'] = {'fixtures': {'SharedRecursiveStateBenchmarks': {'TaLib.Functions.Ad': 'Ad'}}, 'implicit_fixtures': set(), 'paths': {'tests/ModelsTests/CompactMoneyFlowPublicationTests.cs', 'src/Helpers/MoneyFlowAccumulationWindow.cs'}, 'methods': {'SharedRecursiveStateBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (30, 6), 'shards': 1, 'baseline_phase': 'targeted', 'baseline_totals': (60, 12)}


SCOPES['trix-private-update-work-20261109'] = {'fixtures': {'SharedSkenderEmaFamilyBenchmarks': {'Skender.GetTrix': 'Skender.GetTrix'}, 'SharedTaAdaptiveRateNativeBenchmarks': {'TaLib.Functions.Trix': 'TaLib.Functions.Trix'}}, 'implicit_fixtures': set(), 'paths': {'src/Indicators/ValuesBarExecution.TripleRate.cs'}, 'methods': {'SharedSkenderEmaFamilyBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel'], 'SharedTaAdaptiveRateNativeBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (60, 12), 'shards': 2, 'baseline_phase': 'targeted', 'baseline_totals': (60, 12)}


SCOPES['adl-price-bounds-20261109'] = {'fixtures': {'SharedRecursiveStateBenchmarks': {'TaLib.Functions.Ad': 'Ad'}}, 'implicit_fixtures': set(), 'paths': {'src/Helpers/MoneyFlowAccumulationWindow.cs', 'tests/ModelsTests/CompactMoneyFlowPublicationTests.cs'}, 'methods': {'SharedRecursiveStateBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (30, 6), 'shards': 1, 'baseline_phase': 'targeted', 'baseline_totals': (60, 12)}


SCOPES['adl-direct-prefix-publication-20261109'] = {'fixtures': {'SharedAdlDetailsBenchmarks': {'Skender.GetAdl': 'Skender.GetAdl'}}, 'implicit_fixtures': set(), 'paths': {'src/Helpers/CompactMacdValue.cs', 'tests/ModelsTests/CompactAccumulationDistributionExecutionTests.cs', 'src/Indicators/ValuesBarExecution.AccumulationDistribution.cs'}, 'methods': {'SharedAdlDetailsBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (30, 6), 'shards': 1, 'baseline_phase': 'targeted', 'baseline_totals': (60, 12)}


SCOPES['beta-return-bounds-20261109'] = {'fixtures': {'SharedPairInputBenchmarks': {'TaLib.Functions.Beta': 'Beta'}, 'SharedSkenderStatisticsBenchmarks': {'Skender.GetBeta': 'Skender.GetBeta'}}, 'implicit_fixtures': set(), 'paths': {'tests/ModelsTests/BetaReturnBoundsTests.cs', 'src/Indicators/WindowReturnBeta.cs'}, 'methods': {'SharedPairInputBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel'], 'SharedSkenderStatisticsBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (60, 12), 'shards': 2, 'baseline_phase': 'targeted', 'baseline_totals': (30, 6)}


SCOPES['adl-fixed-grid-20261109'] = {'fixtures': {'SharedAdlDetailsBenchmarks': {'Skender.GetAdl': 'Skender.GetAdl'}}, 'implicit_fixtures': set(), 'paths': {'src/Indicators/ValuesBarExecution.AccumulationDistribution.cs', 'tests/ModelsTests/FixedGridAccumulationDistributionTests.cs'}, 'methods': {'SharedAdlDetailsBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (30, 6), 'shards': 1, 'baseline_phase': 'targeted', 'baseline_totals': (30, 6)}


SCOPES['adl-four-difference-bounds-20261109'] = {'fixtures': {'SharedAdlDetailsBenchmarks': {'Skender.GetAdl': 'Skender.GetAdl'}}, 'implicit_fixtures': set(), 'paths': {'tests/ModelsTests/BoundedAccumulationDifferenceTests.cs', 'src/Indicators/ValuesBarExecution.AccumulationDistribution.cs'}, 'methods': {'SharedAdlDetailsBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (30, 6), 'shards': 1, 'baseline_phase': 'targeted', 'baseline_totals': (30, 6)}


SCOPES['adl-cached-word-mean-20261109'] = {'fixtures': {'SharedAdlDetailsBenchmarks': {'Skender.GetAdl': 'Skender.GetAdl'}}, 'implicit_fixtures': set(), 'paths': {'src/Indicators/ValuesBarExecution.AccumulationDistribution.cs', 'src/Helpers/ExactMeanAccumulator.cs', 'src/Helpers/ExactMeanAccumulator.CachedWordMean.cs', 'tests/ModelsTests/CachedWordAccumulationMeanTests.cs'}, 'methods': {'SharedAdlDetailsBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (30, 6), 'shards': 1, 'baseline_phase': 'targeted', 'baseline_totals': (30, 6)}


SCOPES['adl-direct-binary-publication-20261109'] = {'fixtures': {'SharedAdlDetailsBenchmarks': {'Skender.GetAdl': 'Skender.GetAdl'}}, 'implicit_fixtures': set(), 'paths': {'src/Helpers/CompactMacdValue.cs', 'src/Helpers/JurikDyadic.cs', 'tests/ModelsTests/DirectBinaryAccumulationPublicationTests.cs'}, 'methods': {'SharedAdlDetailsBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (30, 6), 'shards': 1, 'baseline_phase': 'targeted', 'baseline_totals': (30, 6)}


SCOPES['adl-identity-column-20261109'] = {'fixtures': {'SharedRecursiveStateBenchmarks': {'TaLib.Functions.Ad': 'Ad'}}, 'implicit_fixtures': set(), 'paths': {'src/Indicators/ValuesBarExecution.IdentityAdl.cs', 'src/Indicators/ValuesBarExecution.cs', 'tests/ModelsTests/IdentityAdlColumnExecutionTests.cs'}, 'methods': {'SharedRecursiveStateBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (30, 6), 'shards': 1, 'baseline_phase': 'targeted', 'baseline_totals': (30, 6)}


SCOPES['exp-parallel-minimum-20261109'] = {'fixtures': {'SharedPointwiseBenchmarks': {'TaLib.Functions.Exp': 'Exp'}}, 'implicit_fixtures': set(), 'paths': {'src/Indicators/ValuesBarExecution.Pointwise.cs'}, 'methods': {'SharedPointwiseBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (30, 6), 'shards': 1, 'baseline_phase': 'targeted', 'baseline_totals': (30, 6)}


SCOPES['log-parallel-minimum-20261109'] = {'fixtures': {'SharedPointwiseBenchmarks': {'TaLib.Functions.Ln': 'Ln', 'TaLib.Functions.Log10': 'Log10'}}, 'implicit_fixtures': set(), 'paths': {'src/Indicators/ValuesBarExecution.Pointwise.cs'}, 'methods': {'SharedPointwiseBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (60, 12), 'shards': 2, 'baseline_phase': 'targeted', 'baseline_totals': (30, 6)}


SCOPES['log-physical-fanout-threshold-20261110'] = {'fixtures': {'SharedPointwiseBenchmarks': {'TaLib.Functions.Ln': 'Ln', 'TaLib.Functions.Log10': 'Log10'}}, 'implicit_fixtures': set(), 'paths': {'tests/ModelsTests/LogarithmParallelLimitTests.cs', 'src/Indicators/ValuesBarExecution.Pointwise.cs'}, 'methods': {'SharedPointwiseBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (60, 12), 'shards': 2, 'baseline_phase': 'targeted', 'baseline_totals': (30, 6)}


SCOPES['circular-physical-fanout-threshold-20261110'] = {'fixtures': {'SharedPointwiseBenchmarks': {'TaLib.Functions.Ln': 'Ln', 'TaLib.Functions.Log10': 'Log10', 'TaLib.Functions.Sin': 'Sin', 'TaLib.Functions.Cos': 'Cos', 'TaLib.Functions.Tan': 'Tan'}}, 'implicit_fixtures': set(), 'paths': {'tests/ModelsTests/PointwiseParallelLimitTests.cs', 'src/Indicators/ValuesBarExecution.Pointwise.cs'}, 'methods': {'SharedPointwiseBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (150, 30), 'shards': 5, 'baseline_phase': 'targeted', 'baseline_totals': (60, 12)}


SCOPES['tsi-compact-signal-20261110'] = {'fixtures': {'SharedSkenderEmaFamilyBenchmarks': {'Skender.GetTsi': 'Skender.GetTsi'}}, 'implicit_fixtures': set(), 'paths': {'tests/ModelsTests/SeededTrueStrengthExecutionTests.cs', 'src/Indicators/SeededTrueStrength.cs'}, 'methods': {'SharedSkenderEmaFamilyBenchmarks': ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']}, 'totals': (30, 6), 'shards': 1, 'baseline_phase': 'targeted', 'baseline_totals': (150, 30)}


def expected_totals(plan):
    return SCOPES[plan['targeted_reference']['scope']]['totals']


def verify_reference(plan):
    reference = plan.get('targeted_reference')
    if reference is None:
        return
    scope = SCOPES[reference['scope']]
    fixtures = scope['fixtures']
    assert not any(plan.get(k) for k in ('qualify_variants', 'standalone_variants', 'coverage_reference', 'trix_preflight'))
    assert plan['sources']['published'] == plan['sources']['old']
    raw = subprocess.check_output(['git', 'show', plan['sources']['published'] + ':' + reference['decision']], cwd=REPO)
    assert hashlib.sha256(raw).hexdigest() == reference['sha256']
    decision = json.loads(raw)
    assert decision['accepted'] and decision['phase'] == scope.get('baseline_phase','successor')
    assert (decision['case_count'], decision['decision_count']) == scope.get('baseline_totals',(1860,372))
    if decision['phase'] == 'targeted':
        assert decision['production_integrated'] and decision['report_imported']
    assert decision['verdicts'].get('faster', 0) > 0
    assert not decision['verdicts'].get('slower', 0)
    assert str(decision['run_id']) == str(reference['run_id'])
    assert decision['sources']['new'] == reference['qualified_source']
    if reference.get('dependency_reconciliation'):
        from .reconciled_baseline import verify_reconciliation
        verify_reconciliation(reference, plan['sources']['old'])
    else:
        assert not git('diff', '--name-only', reference['qualified_source'], plan['sources']['old'],
                       '--', 'src', 'tests', BENCH, TESTS)
    changed = set(git('diff', '--name-only', plan['sources']['old'], plan['sources']['successor'],
                      '--', 'src', 'tests').splitlines())
    assert changed == scope['paths'], changed
    pairs = set().union(*(set(mapping) for mapping in fixtures.values()))
    seen = {'batch': set(), 'builder': set()}
    cases = decisions = 0
    assert len(plan['targeted_shards']) == scope.get('shards',2)
    for index, shard in enumerate(plan['targeted_shards']):
        assert shard['index'] == index
        shard_fixtures = [task['fixture'] for task in shard['tasks']]
        assert shard_fixtures and len(shard_fixtures) == len(set(shard_fixtures))
        for task in shard['tasks']:
            batch = task['fixture'] == 'LibraryPairBenchmarks'
            allowed = {p:p for p in pairs} if batch else fixtures[task['fixture']]
            expected_methods = ['Ooples', 'Competitor'] if batch else scope.get('methods', {}).get(
                task['fixture'], ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel'])
            assert task['methods'] == expected_methods
            assert bool(task.get('implicit_pair')) == (task['fixture'] in scope.get(
                'implicit_fixtures', {'SharedTaHilbertTrendModeBenchmarks'}))
            assert bool(task.get('variance_parameter')) == (task['fixture'] == 'SharedDispersionBenchmarks')
            category = 'batch' if batch else 'builder'
            for operation in task['operations']:
                pair = operation['pair']
                assert pair in allowed and operation['raw'] == allowed[pair] and pair not in seen[category]
                seen[category].add(pair)
            owned = len(set(task['methods']) & OWNED)
            expected_decisions = len(task['operations']) * 2 * owned
            expected_cases = len(task['operations']) * 2 * (3 * owned + len(task['methods']))
            assert (task['cases'], task['decisions']) == (expected_cases, expected_decisions)
            cases += expected_cases
            decisions += expected_decisions
    assert seen['batch'] == seen['builder'] == pairs
    assert (cases, decisions) == scope['totals']
