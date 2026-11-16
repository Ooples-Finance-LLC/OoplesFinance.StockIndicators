"""Separately pinned six-API integer-window pilot; shared authentication remains unchanged."""
import re
from . import pipeline as p, candle_family_roi as common
from .candle_family_roi import BASE_PLAN, BASE_PLAN_SHA, OLD, VARIANTS, PRODUCER, POLICY
APPROVAL = 'benchmarks/plans/evidence/candle-six-api-native-pilot-scope-20261115.json'
APPROVAL_SHA = 'ad9869f0bbb8401fec5e5f00bebf4d502913bb3e07e2006ad6d6d13dc22907d8'
NEW = '55cfd038b6c0a12138387ae7b7790039a3fd98e1'
CI_RUN = "43777283855"
UNIT = ['CandleIntegerWindowTests', 'SequenceParallelExecutionTests', 'NeckParallelExecutionTests']
PARITY = ['SequenceCandleTests', 'NeckCandleTests', 'SharedSequenceCandleBenchmarkTests', 'SharedNeckCandleBenchmarkTests']
PAIRS = ['TaLib.Candles.MatchingLow', 'TaLib.Candles.StickSandwich', 'TaLib.Candles.ThreeLineStrike', 'TaLib.Candles.OnNeck', 'TaLib.Candles.InNeck', 'TaLib.Candles.Thrusting']


def validate_scope(plan):
    assert plan['diagnostic_kind'] == 'candle-six-api-native-pilot'
    assert p.digest(p.REPO / APPROVAL) == APPROVAL_SHA
    approval = p.read(p.REPO / APPROVAL)
    assert approval['approved'] and approval['diagnostic_only'] and not approval['qualification']
    assert plan['roi_approval'] == dict(path=APPROVAL, sha256=APPROVAL_SHA)
    assert plan['diagnostic_only'] and plan['runtime_policy_overrides'] == {}
    assert plan['sources'] == dict(published=OLD, old=OLD, successor=NEW, variants=VARIANTS)
    assert (plan['cases'], plan['decisions'], plan['matrix']['shards'], plan['matrix']['max_parallel']) == (60, 12, 1, 1)
    assert plan['proposed_dotnet_sdk'] == '10.0.403'
    assert plan['measurement_policy'] == POLICY
    assert plan['runtime_dependency_alignment'] == dict(tensor_version='0.150.0')
    assert plan['require_fixture_inventory']
    task = dict(fixture='LibraryPairBenchmarks', operations=[dict(raw=x, pair=x) for x in PAIRS],
                methods=['Ooples', 'Competitor'], implicit_pair=False, cases=60, decisions=12)
    assert plan['diagnostic_shards'] == plan['targeted_shards'] == [dict(index=0, tasks=[task], cases=60, decisions=12)]
    assert not any(plan.get(k) for k in ('targeted_reference', 'coverage_reference', 'standalone_variants',
                                       'qualify_variants', 'trix_preflight', 'reuse_inputs'))
    candidate = plan['candidate']
    assert candidate['ci_run'] == CI_RUN and candidate['producer'] == PRODUCER
    assert candidate['source'] == NEW and candidate['unit_classes'] == UNIT and candidate['parity_classes'] == PARITY
    assert re.fullmatch('[a-f0-9]{40}', candidate['evidence_source'])
    for key in ('manifest_sha256', 'gpu_receipt_sha256'):
        assert re.fullmatch('[a-f0-9]{64}', candidate[key])
    assert plan['baseline_inputs']['run_id'] == '43673961720'
    assert plan['baseline_inputs']['plan'] == BASE_PLAN and plan['baseline_inputs']['plan_sha256'] == BASE_PLAN_SHA
    assert re.fullmatch('[a-f0-9]{64}', plan['baseline_inputs']['provenance_sha256'])


def candidate_inputs(folder, plan):
    return common._candidate_inputs(folder, plan, NEW, UNIT, PARITY, 64, 135, CI_RUN)


def prepare(baseline, candidate, out):
    return common._prepare(baseline, candidate, out, validate_scope, candidate_inputs, NEW)


def collect(folder):
    from pathlib import Path
    from .verify import _collect
    result = _collect(Path(folder), 'diagnostic', preflight=False, diagnostic=True)
    assert not result['accepted'] and (result['case_count'], result['decision_count']) == (60, 12)
    result.update(diagnostic_only=True, qualification=False, production_integrated=False,
                  report_imported=False, policy_changed=False)
    return result
