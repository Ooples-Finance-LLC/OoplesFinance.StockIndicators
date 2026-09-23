# Changelog

## [2.0.0](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/compare/v1.1.0...v2.0.0) (2026-09-23)


### ⚠ BREAKING CHANGES

* AlpacaBroker and AlpacaMarketDataProvider ship in the new OoplesFinance.StockIndicators.Trading package instead of OoplesFinance.StockIndicators. Add a reference to it; nothing else changes.
* take every remaining dispersion consumer over its own window ([#232](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/232))
* take three dispersion consumers' deviations over their own windows ([#231](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/231))
* address builder outputs by published key instead of a six-slot enum ([#230](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/230))
* find five-bar fractals rather than three-bar pivots ([#214](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/214))
* take the kase family's deviation over its own window ([#210](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/210))
* take historical volatility's deviation over its own window ([#205](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/205))
* seven indicators rename an output key. AverageTrueRangeChannel MiddleBand is now the centre of its bands and the moving average is published as Sma; MovingAverageBands MiddleBand is the slow average and the fast one is FastMa; RateOfChangeBands MiddleBand is zero and the rate of change is Roc; ScalpersChannel MiddleBand is the midpoint of its bands and its line is Scalper; StationaryExtrapolatedLevels MiddleBand is the midpoint and the deviation is Deviation; VervoortModifiedBollingerBandIndicator MiddleBand is the mean of its bands and %b is PercentB; and LBRPaintBars no longer publishes MiddleBand at all, its width being published as Aatr.
* publish the performance index under its own name ([#203](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/203))
* IndicatorCatalog.Trix() returns SeriesHandle instead of TrixResult, and the AroonOscillatorResult, AlligatorIndexResult and GatorOscillatorResult members are renamed to the outputs the indicators publish. See MIGRATION.md for the replacement for each member.
* promote every typed builder spec to a real indicator ([#188](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/188))
* compute and stream every typed builder spec with the indicator it names ([#187](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/187))
* the public Func<OhlcvBar, double> constructor overload is removed from every streaming indicator state. Wrap the state instead: new CustomInputState(state, selector), or new CustomInputState(state, InputSeries.MedianPrice) for a preset.

### Features

* add an explicit indicator source and derived-series helpers ([#168](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/168)) ([c08a143](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/c08a143e27f4afb7925ba710b0ca7859e362a644))
* address builder outputs by name ([#218](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/218)) ([f880f52](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/f880f52df6b35c04d0ae699c9bf6c153b123b677))
* compute and stream every typed builder spec with the indicator it names ([#187](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/187)) ([427fd72](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/427fd72ee5f1dd633da556e20e0d8d00ee54188d))
* declare which output each streaming state's value is ([#185](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/185)) ([30b2b57](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/30b2b57624f7e6beae1bbfa1a6e55a1af562bf6d))
* let every indicator take custom values in both engines ([#181](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/181)) ([1bb55e4](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/1bb55e41816a2f15f3283dab5593f893a28c069c))
* make a state that cannot take or ignores custom input a build error ([#184](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/184)) ([7b48fd8](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/7b48fd84f2d3ac63b3811add1a0208cd9696b614))
* promote every typed builder spec to a real indicator ([#188](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/188)) ([1fd9ac5](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/1fd9ac55c3c3ce01d2241d71de435148d098019a))
* report what the machine can actually do ([#175](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/175)) ([d2d6e2b](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/d2d6e2b528f406c8e1776b87cca06aafd7935987))
* serve every arm that agrees with its batch indicator ([#238](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/238)) ([0a27c27](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/0a27c274b0cf87de948cf0eb937f359a008a90b3))


### Bug Fixes

* close the flat-market and band-ordering halves of [#178](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/178) ([#197](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/197)) ([fecbc8e](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/fecbc8ebfebd95a95a88bfb9821e0dee77a2452e))
* find five-bar fractals rather than three-bar pivots ([#214](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/214)) ([a6e20da](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/a6e20da203fe612dcc80c28ebcb46e17652ca843))
* give every multi-output catalog handle the series it names ([#226](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/226)) ([01d1a88](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/01d1a88f164bde2faa8ae7ce4c3969065f7c0245))
* give the dispersion consumers the quantity their arithmetic needs ([#224](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/224)) ([37957a6](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/37957a6ee8d2ac5bcdf84b013a8d76343781b171))
* judge a price-like input by more than an exact comparison ([#215](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/215)) ([59d90eb](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/59d90ebf92ced9ee4b034e4b8b246e4460ca319e))
* keep the first bar's fabricated return out of the peak oscillator's window ([#213](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/213)) ([e93872b](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/e93872bb7e5cc3c0e9cfc1e7555df03e97c47bd5))
* make a moving average of a constant series be that constant ([#242](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/242)) ([13253bf](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/13253bfcd89715062bd2bb077e9b2eb712b127af))
* make every streaming state compute what its batch twin computes ([#186](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/186)) ([9976c14](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/9976c142b1b8307df77027a3b564d25f1c5a02c9))
* make SI0006 reject a sentinel indicator name ([#204](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/204)) ([5ebc328](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/5ebc328f10d796413ac0b743ff579f144b1e9f68))
* match the batch lookback in ultimatetraderoscillator ([#176](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/176)) ([db911b6](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/db911b67df97b2146e71dca1e8f58db69c6205dc))
* measure a standard deviation where the definition says so ([#195](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/195)) ([e78c117](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/e78c117b0172f22691d81b15250f94ffc216ff18))
* publish the performance index under its own name ([#203](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/203)) ([bc9afa0](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/bc9afa0cc335e9d1dd9c2c9699c7ec80e54c9ceb))
* read the dispersion consumers that route through a shared helper ([#225](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/225)) ([edadea1](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/edadea1cd8b8d7ddb611a74cf43a533ec84e60b2)), closes [#222](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/222)
* refuse a builder slot the indicator does not publish ([#229](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/229)) ([afc289d](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/afc289d15f13e574ba3507b49d976967e547ee86))
* repair every fast arm that disagreed with its bound batch call ([#235](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/235)) ([176ac5c](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/176ac5c8010885e1da6a34d4ac4e1bc002fab7d4))
* serve the four repaired arms issue [#233](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/233) named ([#237](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/237)) ([bb60999](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/bb609997d27c0e51ca68ad0b0cef93a232b31c7b))
* **sourcegen:** keep the generator's Roslyn floor, and catch the next one in CI ([#169](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/169)) ([1dca066](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/1dca0664a5d97faea603d58921878d35ea5cc333))
* take every remaining dispersion consumer over its own window ([#232](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/232)) ([f4b854e](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/f4b854e50b7aacb1403ce411ed826b08ceca5d05))
* take historical volatility's deviation over its own window ([#205](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/205)) ([d59f37f](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/d59f37f7f832f98a8b79925a292d8b34f885ca8c))
* take the kase family's deviation over its own window ([#210](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/210)) ([b2faa7f](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/b2faa7f2f33386abb8c71737cda44ee42de87ba6))
* take the standard deviation over its window, and stamp three indicators with their own names ([#198](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/198)) ([b09576c](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/b09576c68d19525f7ae70553839fb7c80f4254e7))
* take three dispersion consumers' deviations over their own windows ([#231](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/231)) ([0db2531](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/0db25312dcb7f5ed362f7ecc1ba42457984ac512))


### Performance

* fuse an unread indicator chain into a single pass over the bars ([#234](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/234)) ([c119e98](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/c119e98e5d956dede9f29c744e494c8677f69f15))
* share identical indicator computations in the graph ([#174](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/174)) ([67d45a3](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/67d45a3b93f17042b98a63e328c721973b33e5ae))


### Refactoring

* address builder outputs by published key instead of a six-slot enum ([#230](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/230)) ([c933a00](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/c933a008972d52cc7d0bb982549108e1bdfad233))
* remove the alpaca adapters from core ([#171](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/171)) ([5210b87](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/5210b87dfd677bda2a4a86569c5ec378e1ce0e5d))
* stop emitting a compute dispatch that discards its own length ([#216](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/216)) ([ffbd57f](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/ffbd57f98fc00cc4eeb8ba3422a370022cec51d4)), closes [#206](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/206)


### Dependencies

* Bump FluentAssertions from 8.10.0 to 8.11.0 ([#192](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/192)) ([cb1864a](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/cb1864a86d2fc3e7002b116dafd50f8500b1c1aa))
* Bump Microsoft.NET.Test.Sdk from 18.4.0 to 18.10.1 ([#193](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/193)) ([b7b8131](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/b7b813137dcd6b8d2055b74f1c1aa239118c9664))

## [1.1.0](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/compare/v1.0.53...v1.1.0) (2026-09-09)


### Features

* add FXMacroData macro data integration ([#136](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/136)) ([2625e50](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/2625e50d15924ff9d5f52f0fb0bf49a64c078574))


### Bug Fixes

* **alpaca:** tolerant account fetch — don't require pattern_day_trader ([#135](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/135)) ([c9749a2](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/c9749a26cc228ce2641e61a7d6a42bb28d1fe3c2))
* map nonstandard batch indicator names ([#137](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/137)) ([1ee82f4](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/1ee82f471dfd80158ebe19e6c55fc2b82181db1b))
* **sourcegen:** format numeric defaults with InvariantCulture ([#138](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/138)) ([be48580](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/be4858011a2c0268499bb0854fc53a3996226e3b))
* unblock ci, restore net461, add net8.0, and fix signal pattern and volume index defects ([#144](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/144)) ([6c7618d](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/6c7618dfb3ed543675da1e10e9090ca23d574007))


### Dependencies

* Bump FluentAssertions from 8.9.0 to 8.10.0 ([#153](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/153)) ([cdd8d1b](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/cdd8d1b02c37e45713442572530e3f14bbc28500))
* Bump Microsoft.CodeAnalysis.Analyzers and Microsoft.CodeAnalysis.CSharp ([#155](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/155)) ([f4a38bf](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/f4a38bf7cb513f9aecdb6062e14731207afa2c8c))
* Bump NSubstitute from 5.3.0 to 6.2.0 ([#156](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/156)) ([4671158](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/4671158a258b91a69082b7874757623074df3201))
* bump system.buffers to 4.6.1, restoring the net461 build ([5eb078b](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/5eb078ba31c06033e5ef780b663fc0d50c9de406))
* Bump System.Linq.Async from 6.0.1 to 7.0.1 ([#158](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/158)) ([2e0a4fd](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/2e0a4fdd30ab27a9722c7492e8bb5fb8ab79b096))
* Bump System.Memory from 4.6.0 to 4.6.3 ([#159](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/159)) ([68e945b](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/68e945b6666020d4e8e1ffb422e169da5977e40d))
* Bump System.Text.Json from 6.0.11 to 10.0.12 ([#160](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/160)) ([1651d76](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/1651d769175669dbc987c811d745ac50aca17b1f))
* Bump xunit from 2.7.0 to 2.9.3 ([#161](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/161)) ([fbd71e3](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/fbd71e3d475648316889aec32369edf0c5a5e1e3))
* Bump xunit.runner.visualstudio from 2.5.7 to 4.0.0 ([#162](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/162)) ([a602eb7](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/a602eb706e58b81969e7b3fd58057469ebe1f5df))
* Bump Xunit.SkippableFact from 1.5.23 to 1.5.85 ([#163](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/issues/163)) ([01dc696](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/commit/01dc69621ebe8acd5c22c8377d32e4eae8fe4b22))
