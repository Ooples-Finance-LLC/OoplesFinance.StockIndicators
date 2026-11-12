"""Only the explicitly approved four-pair warmup exception."""
PAIRS = {'TaLib.Functions.'+name for name in ('Add','Sub','Mult','Div')}
PROPOSAL = 'benchmarks/plans/arithmetic-warmup-policy-proposal-20261112.json'
SHA = 'f37b0b9e2f22a3175202ec8fe8cb751913ba8665e5d6cc4a81df0843480baa46'


def validate(plan):
    from . import pipeline as p
    path=p.REPO/PROPOSAL
    assert p.digest(path)==SHA, 'Frozen arithmetic warmup approval changed'
    proposal=p.read(path)
    assert plan.get('arithmetic_warmup_approval')==dict(approved=True,proposal=PROPOSAL,sha256=SHA,diagnostic_run='43230319639')
    assert proposal['approved'] is True and proposal['runtime_policy_overrides']=={}
    assert plan['sources']==proposal['sources']
    assert plan['runtime_dependency_alignment']==proposal['runtime_dependency_alignment']=={'tensor_version':'0.150.0'}
    assert set(plan['affected_pairs'])==PAIRS and len(plan['affected_pairs'])==4
    assert (plan['cases'],plan['decisions'],plan['matrix']['max_parallel'])==(120,24,2)
    policy=plan['measurement_policy']
    assert policy['fixed_invocation_pair_warmups']=={pair:20000 for pair in PAIRS}
    assert policy['warmups']==policy['iterations']==10 and policy['fixed_invocation_warmups']==1000
    assert policy['quiet_samples']==12 and policy['max_cpu_percent']==15
    assert plan['qualification_context']['report_refresh_only'] is True
    assert plan['reuse_inputs']['run_id']=='43214078458'


def permits_context(pairs,methods,context):
    return bool(pairs) and set(pairs)<=PAIRS and set(methods) in ({'Ooples'},{'Ooples','Competitor'}) \
        and len(methods)==len(set(methods)) and context is not None \
        and context.get('warmup_count')==20000 and context.get('arithmetic_warmup_policy_sha256')==SHA
