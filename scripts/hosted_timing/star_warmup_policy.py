"""The approved five-pair warmup trial in one complete source qualification."""
PAIRS = {'TaLib.Candles.'+name for name in ('MorningStar','EveningStar','MorningDojiStar','EveningDojiStar','AbandonedBaby')}
PROPOSAL = 'benchmarks/plans/evidence/star-warmup-combined-qualification-20261114.json'
SHA = '55db0db99dad0412c62c7d7259858b7a537fbb8db54f52a7908a4de1f17dd9f1'

REPAIR_PROPOSAL = 'benchmarks/plans/evidence/star-warmup-inventory-repair-20261114.json'
REPAIR_SHA = '813f4a140a9b9e2cf2c74ddb6f34f898ff7aba07020c7118edb40598506386be'


def approval_identity(plan):
    if plan.get('targeted_reference', {}).get('scope') == 'abandoned-inventory-repair-20261114':
        return REPAIR_PROPOSAL, REPAIR_SHA, 176
    return PROPOSAL, SHA, 180


def validate(plan):
    from . import pipeline as p
    proposal_path, approval_sha, cases = approval_identity(plan)
    assert p.digest(p.REPO/proposal_path)==approval_sha, 'Frozen star warmup approval changed'
    proposal=p.read(p.REPO/proposal_path)
    assert plan.get('star_warmup_approval')==dict(approved=True,proposal=proposal_path,sha256=approval_sha,diagnostic_run='43601699975')
    assert proposal['approved'] and proposal['trial_only'] and proposal['one_cycle']
    assert proposal['runtime_policy_overrides']=={}
    assert plan['sources']==proposal['sources']
    assert plan['runtime_dependency_alignment']==proposal['runtime_dependency_alignment']=={'tensor_version':'0.150.0'}
    assert set(plan['affected_pairs'])==PAIRS|{'TaLib.Candles.ThreeBlackCrows'} and len(plan['affected_pairs'])==6
    assert (plan['cases'],plan['decisions'],plan['matrix']['max_parallel'])==(cases,36,1)
    expected_scope = 'abandoned-inventory-repair-20261114' if cases == 176 else 'abandoned-certified-blocks-20261114'
    assert plan['targeted_reference']['scope']==expected_scope
    if cases == 176:
        assert proposal['failed_run']=='43610765565' and not proposal['partial_measurements_reused']
        assert proposal['require_fixture_inventory'] and plan['require_fixture_inventory']
        crow = [task for shard in plan['targeted_shards'] for task in shard['tasks'] if task['fixture']=='SharedCrowSoldierBenchmarks']
        assert len(crow)==1 and crow[0]['methods']==['BuilderFull','BuilderLatestOnly']
    policy=plan['measurement_policy']
    assert policy['fixed_invocation_pair_warmups']=={pair:20000 for pair in PAIRS}
    assert policy['warmups']==policy['iterations']==10 and policy['fixed_invocation_warmups']==1000
    assert policy['quiet_samples']==12 and policy['max_cpu_percent']==15
    diagnostic=p.REPO/proposal['diagnostic_evidence']['path']
    assert p.digest(diagnostic)==proposal['diagnostic_evidence']['sha256']
    evidence=p.read(diagnostic)
    assert evidence['diagnostic_only'] and evidence['same_binary'] and evidence['all_cases_verified']
    assert (evidence['case_count'],evidence['comparison_count'])==(20,5)
    assert {row['pair'] for row in evidence['comparisons']}==PAIRS
    assert not any(evidence[k] for k in ('production_integrated','report_imported','policy_changed')) and 'accepted' not in evidence


def permits_context(pairs,methods,context):
    return len(pairs)==1 and set(pairs)<=PAIRS and set(methods) in ({'Ooples'},{'Ooples','Competitor'}) \
        and len(methods)==len(set(methods)) and context is not None \
        and context.get('warmup_count')==20000 and context.get('star_warmup_policy_sha256') in {SHA, REPAIR_SHA}
