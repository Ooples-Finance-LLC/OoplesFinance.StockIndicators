"""Archive independently verified hosted evidence without publishing production."""
from pathlib import Path
import gzip
import hashlib
import json
import subprocess

from .pipeline import PLAN, REPO, read, digest, verify_inputs
from .verify import collect


def write(path, value):
    # Archives disable Git text conversion; emit LF bytes on Windows too.
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_bytes((json.dumps(value,indent=2)+'\n').encode('utf-8'))


def archive(raw, prepared, repo, phase):
    inputs = verify_inputs(prepared)
    if phase == 'baseline-refresh':
        from .baseline_refresh_dispatch import implementation
        summary=implementation(read(PLAN)).collect(raw)
        assert summary['baseline_refresh_verified'] and not summary['accepted'] and not summary['production_qualification']
    else:
        summary = collect(raw, phase)
    assert summary['input_provenance_sha256'] == digest(prepared/'provenance.json')
    run_id, attempt = inputs['run_id'], inputs['run_attempt']
    name = f'hosted-{phase}-{run_id}-attempt{attempt}'
    destination = repo/'benchmarks/results/remaining-indicators'/name
    assert not destination.exists(), 'Inspect existing archive before retrying'
    blobs = {('raw/'+p.relative_to(raw).as_posix()):p.read_bytes() for p in raw.rglob('*') if p.is_file()}
    blobs.update({('prepared/'+p.relative_to(prepared).as_posix()):p.read_bytes()
                  for p in (prepared/'evidence').rglob('*') if p.is_file()})
    blobs['prepared/provenance.json'] = (prepared/'provenance.json').read_bytes()
    original = prepared/'original-preparation.json'
    if original.exists():
        blobs['prepared/original-preparation.json'] = original.read_bytes()
        blobs['original-plan.json'] = (REPO/read(PLAN)['reuse_inputs']['plan']).read_bytes()
    blobs['plan.json'] = PLAN.read_bytes()
    blobs.update({'scripts/'+p.name:p.read_bytes() for p in Path(__file__).parent.glob('*.py')})
    if read(PLAN).get('matchinglow_grid_qualification'):
        plan = read(PLAN)
        assert phase == 'targeted'
        from .matchinglow_grid_qualification import BASE_PLAN, BASE_PLAN_SHA, QUALIFIED, OLD
        base_plan = REPO/BASE_PLAN
        assert digest(base_plan) == BASE_PLAN_SHA
        blobs['original-plan.json'] = base_plan.read_bytes()
        for name, approval in [('scope-approval.json', plan['scope_approval']),
                ('original-scope-approval.json', read(base_plan)['refresh_approval'])]:
            path = REPO/approval['path']
            assert digest(path) == approval['sha256']
            blobs[name] = path.read_bytes()
        # Preserve replayable authentication documents, never runtime/blob files.
        for name in ('baseline-authentication', 'candidate-authentication'):
            folder = prepared/name
            root = folder/('provenance.json' if name == 'baseline-authentication' else 'manifest.json')
            blobs['prepared/'+root.relative_to(prepared).as_posix()] = root.read_bytes()
            for path in (folder/'evidence').rglob('*'):
                if path.is_file():
                    assert path.suffix.lower() not in ('.dll', '.exe', '.pdb'), 'No runtime in source evidence archive'
                    blobs['prepared/'+path.relative_to(prepared).as_posix()] = path.read_bytes()
        preflight = (prepared/'consumer-preflight').resolve()
        for relative, expected in plan['preflight']['files'].items():
            path = (preflight/relative).resolve()
            assert path.is_relative_to(preflight) and digest(path) == expected
            assert path.suffix.lower() not in ('.dll', '.exe', '.pdb'), 'No preflight runtime in Git'
            blobs['prepared/consumer-preflight/'+relative] = path.read_bytes()
        for name in ('run-matchinglow-grid-qualification.py', 'test_hosted_matchinglow_grid_qualification.py'):
            blobs['scripts/root/'+name] = (REPO/'scripts'/name).read_bytes()
        from .pipeline import BENCH, TESTS
        blobs['harness-continuation.patch'] = subprocess.check_output(['git', 'diff', '--binary',
            QUALIFIED, OLD, '--', BENCH, TESTS], cwd=REPO)
    blobs['candidate-source.patch'] = subprocess.check_output(['git','diff','--binary',
        summary['sources']['old'],summary['sources']['new'],'--','src','tests'],cwd=repo)
    assert bool(blobs['candidate-source.patch']) == (phase not in ('coverage','baseline-refresh'))
    if phase == 'baseline-refresh':
        from .baseline_refresh_dispatch import implementation
        refresh=implementation(read(PLAN))
        from .pipeline import BENCH,TESTS
        blobs['harness-source.patch']=subprocess.check_output(['git','diff','--binary',refresh.PUBLISHED,refresh.CANONICAL,'--',BENCH,TESTS],cwd=repo)
        if read(PLAN).get('current_baseline_ten_refresh'):
            assert not blobs['harness-source.patch'], 'Current baseline refresh cannot change the harness'
            plan = read(PLAN)
            original = prepared/'origin-provenance.json'
            assert digest(original) == plan['baseline_inputs']['provenance_sha256']
            blobs['prepared/origin-provenance.json'] = original.read_bytes()
            base_plan = REPO/plan['baseline_inputs']['plan']
            assert digest(base_plan) == plan['baseline_inputs']['plan_sha256']
            blobs['original-plan.json'] = base_plan.read_bytes()
            for key, approval in [('scope-approval.json', plan['refresh_approval']),
                    ('original-scope-approval.json', read(base_plan)['refresh_approval'])]:
                path = REPO/approval['path']
                assert digest(path) == approval['sha256']
                blobs[key] = path.read_bytes()
            preflight = (prepared/'consumer-preflight').resolve()
            for relative, sha in plan['consumer_preflight']['files'].items():
                path = (preflight/relative).resolve()
                assert path.is_relative_to(preflight) and digest(path) == sha
                blobs['prepared/consumer-preflight/'+relative] = path.read_bytes()
        else:
            assert blobs['harness-source.patch']
    manifest = {}
    for relative, content in sorted(blobs.items()):
        target = destination/(relative+'.gz')
        target.parent.mkdir(parents=True,exist_ok=True)
        target.write_bytes(gzip.compress(content,mtime=0))
        assert gzip.decompress(target.read_bytes()) == content
        manifest[relative] = dict(sha256=hashlib.sha256(content).hexdigest(),archive=target.relative_to(repo).as_posix())
    write(destination/'verified-summary.json',summary)
    write(destination/'decision.json',dict(status='baseline-refresh-verified' if phase == 'baseline-refresh' else 'coverage-complete' if phase == 'coverage' else 'qualified-for-integration' if summary['accepted'] else 'rejected-or-inconclusive',
        accepted=summary['accepted'],phase=phase,case_count=summary['case_count'],decision_count=summary['decision_count'],
        verdicts=summary['verdicts'],sources=summary['sources'],run_id=run_id,run_attempt=attempt,
        workflow_head=inputs['workflow_head'],input_provenance_sha256=summary['input_provenance_sha256'],
        measurement_url=f'https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/actions/runs/{run_id}',
        archives=manifest,production_integrated=False,report_imported=False,
        **({'baseline_refresh_verified':True,'report_refresh_only':True,'production_qualification':False,'production_optimization':False,
            'confirmed_native_wins':summary['native_review']['confirmed_native_wins']} if phase=='baseline-refresh' else {}),
        **({'coverage_complete': True, 'source_qualification_claimed': False} if phase == 'coverage' else {}),
        **({'native_crossover_gate_passed':summary['native_crossover_gate_passed'],
            'native_crossover_samples':summary['native_crossover_samples']} if read(PLAN).get('matchinglow_grid_qualification') else {}),
        note=('Fresh 90-case MatchingLow-only qualification with SeparatingLines and StickSandwich sentinels. Accepted source requires zero slower decisions, at least one faster decision, and both MatchingLow 10,000-bar candidate rounds newly beating the measured native control with separated intervals and at least 10% lower means. Historical harness continuation carries no timing-equivalence claim; original baseline/frozen GPU evidence and fresh source-bound candidate GPU100 are retained.' if read(PLAN).get('matchinglow_grid_qualification') else
             'Hosted CPU current-baseline A/A refresh. Original authenticated canonical/frozen GPU receipts are reused; fresh consumer validation passed 303 canonical cases and 20 frozen/canonical alignment cases, without new compilation or GPU execution. A/A admission is not equivalence; no production qualification or optimization is claimed.' if phase == 'baseline-refresh' and read(PLAN).get('current_baseline_ten_refresh') else
             'Hosted CPU harness baseline refresh with fresh canonical correctness/GPU100 and authenticated frozen-variant evidence. Identical-binary A/A outcomes describe timing variability; no production qualification or optimization is claimed.' if phase == 'baseline-refresh' else
             'Hosted CPU timings. Historical source-matched local GPU checks are retained with their original identity.' +
             (' Unchanged qualified production; no source qualification claimed.' if phase == 'coverage' else
              ' WMA/TRIMA results do not replace default SMA rows.'))))
    (destination/'.gitattributes').write_bytes(b'* -text\n')
    return destination
