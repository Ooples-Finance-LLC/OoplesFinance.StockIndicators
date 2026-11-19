"""Reuse authenticated preparation without claiming its checks ran again."""
from pathlib import Path
import copy
import datetime
import os
import shutil


def origin(plan, proof_path):
    from .pipeline import REPO, read, digest, cpu_test_classes, original_gpu_source
    reference = plan['reuse_inputs']
    source_plan_path = REPO / reference['plan']
    assert digest(source_plan_path) == reference['plan_sha256']
    assert digest(proof_path) == reference['provenance_sha256']
    source_plan, proof = read(source_plan_path), read(proof_path)
    assert not source_plan.get('reuse_inputs'), 'Reuse must refer to an original preparation'
    assert proof.get('preparation_mode') != 'reused'
    assert proof['run_id'] == str(reference['run_id'])
    assert proof['workflow_head'] == reference['workflow_head']
    assert source_plan['sources'] == plan['sources'] == proof['sources']
    assert source_plan['proposed_dotnet_sdk'] == plan['proposed_dotnet_sdk'] == proof['sdk']
    for label in ('old', 'successor', 'variants'):
        assert cpu_test_classes(source_plan, label) == cpu_test_classes(plan, label)
        assert original_gpu_source(source_plan, label) == original_gpu_source(plan, label)
    return source_plan, proof


def verify_reused(folder, plan, proof):
    from .pipeline import _verify_input_contents
    original_path = folder/'original-preparation.json'
    source_plan, original = origin(plan, original_path)
    _verify_input_contents(folder, source_plan, original, plan['reuse_inputs']['plan_sha256'])
    assert proof['preparation_mode'] == 'reused'
    assert proof['reused_from'] == plan['reuse_inputs']
    for field in ('sources', 'sdk', 'checks', 'common_manifest', 'evidence_manifest', 'production_sha256'):
        assert proof[field] == original[field], field


def prepare_reused(source, out):
    from .pipeline import PLAN, read, digest, git, write, verify_inputs, _verify_input_contents
    source, out = Path(source).resolve(), Path(out).resolve()
    assert not out.exists(), 'Reuse destination must be fresh'
    assert not out.is_relative_to(source), 'Destination must be outside source inputs'
    plan = read(PLAN)
    source_plan, original = origin(plan, source/'provenance.json')
    _verify_input_contents(source, source_plan, original, plan['reuse_inputs']['plan_sha256'])
    shutil.copytree(source, out)
    shutil.copy2(source/'provenance.json', out/'original-preparation.json')
    proof = copy.deepcopy(original)
    proof.update(plan_sha256=digest(PLAN), workflow_head=git('rev-parse', 'HEAD'),
        run_id=os.environ['GITHUB_RUN_ID'], run_attempt=os.environ['GITHUB_RUN_ATTEMPT'],
        preparation_mode='reused', reused_from=plan['reuse_inputs'],
        reused_at_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    write(out/'provenance.json', proof)
    verify_inputs(out)
    return proof
