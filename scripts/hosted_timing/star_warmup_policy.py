"""The approved five-pair warmup trial in one complete source qualification."""
PAIRS = {'TaLib.Candles.'+name for name in ('MorningStar','EveningStar','MorningDojiStar','EveningDojiStar','AbandonedBaby')}
PROPOSAL = 'benchmarks/plans/evidence/star-warmup-combined-qualification-20261114.json'
SHA = '55db0db99dad0412c62c7d7259858b7a537fbb8db54f52a7908a4de1f17dd9f1'


def validate(plan):
    from . import pipeline as p
    assert p.digest(p.REPO/PROPOSAL)==SHA, 'Frozen star warmup approval changed'
    proposal=p.read(p.REPO/PROPOSAL)
    assert plan.get('star_warmup_approval')==dict(approved=True,proposal=PROPOSAL,sha256=SHA,diagnostic_run='43601699975')
    assert proposal['approved'] and proposal['trial_only'] and proposal['one_cycle']
    assert proposal['runtime_policy_overrides']=={}
    assert plan['sources']==proposal['sources']
    assert plan['runtime_dependency_alignment']==proposal['runtime_dependency_alignment']=={'tensor_version':'0.150.0'}
    assert set(plan['affected_pairs'])==PAIRS|{'TaLib.Candles.ThreeBlackCrows'} and len(plan['affected_pairs'])==6
    assert (plan['cases'],plan['decisions'],plan['matrix']['max_parallel'])==(180,36,1)
    assert plan['targeted_reference']['scope']=='abandoned-certified-blocks-20261114'
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
        and context.get('warmup_count')==20000 and context.get('star_warmup_policy_sha256')==SHA
