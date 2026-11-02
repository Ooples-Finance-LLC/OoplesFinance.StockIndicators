"""Build immutable inputs, measure on one VM per shard, and collect verified evidence."""
from pathlib import Path
import argparse
import copy
import datetime
import hashlib
import gzip
import json
import os
import platform
import shutil
import subprocess
import time
import uuid
import xml.etree.ElementTree as ET

from .checkpoints import run_checkpointed

REPO = Path(__file__).resolve().parents[2]
PLAN = REPO / os.environ.get('HOSTED_TIMING_PLAN', 'benchmarks/plans/hosted-rollout-20261101.json')
DLL = 'OoplesFinance.StockIndicators.dll'
BENCH = 'benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks'
TESTS = 'benchmarks/OoplesFinance.StockIndicators.CompetitorTests'
OWNED = {'Ooples', 'BuilderFull', 'BuilderLatestOnly'}


def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def write(path, data):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + '.tmp')
    temporary.write_text(json.dumps(data, indent=2) + '\n', encoding='utf-8', newline='\n')
    temporary.replace(path)


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def manifest(folder):
    return {p.relative_to(folder).as_posix(): digest(p) for p in sorted(Path(folder).rglob('*')) if p.is_file()}


def git(*args, cwd=REPO):
    return subprocess.check_output(['git', *args], cwd=cwd, text=True).strip()


def command(args, cwd, log):
    print('Running:', ' '.join(map(str, args)), flush=True)
    with Path(log).open('wb') as output:
        subprocess.run(list(map(str, args)), cwd=cwd, stdout=output, stderr=subprocess.STDOUT, check=True)


def verify_trx(path, classes):
    results = ET.fromstring(Path(path).read_bytes()).findall('.//{*}UnitTestResult')
    assert results and all(r.attrib['outcome'] == 'Passed' for r in results)
    counts = {name: sum(name + '.' in r.attrib['testName'] for r in results) for name in classes}
    assert all(counts.values()), counts
    return dict(passed=len(results), classes=counts, sha256=digest(path))


def phase_tasks(plan, phase, shard):
    if phase == 'coverage':
        assert shard == 0 and plan.get('coverage_reference')
        from .coverage import tasks
        return tasks()
    if phase == 'successor':
        assert shard in range(8)
        merged = {}
        for original in plan['shards'][shard]['tasks']:
            task = copy.deepcopy(original)
            fixture = task['fixture']
            if fixture not in merged:
                merged[fixture] = task
            else:
                assert merged[fixture]['methods'] == task['methods']
                merged[fixture]['operations'].extend(task['operations'])
                for key in ('cases', 'decisions'):
                    merged[fixture][key] += task[key]
        return list(merged.values())
    assert phase == 'variants' and shard == 0
    return [dict(fixture='SharedClassicalWindowVariantBenchmarks', group=0,
                 operations=[dict(raw=p, pair=p) for p in ('Wma', 'Trima')],
                 methods=['BuilderFull', 'BuilderLatestOnly'], implicit_pair=False,
                 variant=True, cases=32, decisions=8)]


def methods_for(task, index):
    return [m for m in task['methods'] if index == 2 or m in OWNED]


def phase_labels(phase):
    assert phase in ('successor', 'variants', 'coverage')
    if phase == 'coverage':
        return ('successor', 'successor')
    return ('old', 'successor') if phase == 'successor' else ('successor', 'variants')


def phase_sources(plan, phase):
    keys = phase_labels(phase)
    return dict(zip(('old', 'new'), (plan['sources'][k] for k in keys)))


def original_gpu_source(plan, label):
    legacy = '6bc51bec06a49e2fa2e86e634ec6e2163ee4faaa' if label == 'variants' else plan['sources'][label]
    return plan.get('gpu_sources', {}).get(label, legacy)


def cpu_test_classes(plan, label):
    defaults = ['CompactMacdWordEmaTests', 'CompactClassicalWindowTests']
    if label != 'old':
        defaults.append('ExactMeanUnitWeightTests')
    selected = plan.get('cpu_test_classes', {}).get(label, defaults)
    assert selected and len(selected) == len(set(selected))
    assert all(isinstance(c, str) and c.isidentifier() for c in selected)
    return selected


def warmups_for(plan, fixture):
    assert plan['measurement_policy']['warmups'] == 10
    assert plan['measurement_policy']['iterations'] == 10
    # LibraryPair is the fixed-invocation batch fixture in this qualification matrix.
    count = plan['measurement_policy'].get('fixed_invocation_warmups', 10)
    assert count in (10, 1000)
    return count if fixture == 'LibraryPairBenchmarks' else 10


def warmup_context(plan, fixture):
    return {'warmup_count': warmups_for(plan, fixture)} if 'fixed_invocation_warmups' in plan['measurement_policy'] else {}


def verify_inputs(folder):
    proof, plan = read(folder/'provenance.json'), read(PLAN)
    _verify_input_contents(folder, plan, proof, digest(PLAN))
    if plan.get('reuse_inputs'):
        from .reuse import verify_reused
        verify_reused(folder, plan, proof)
    else:
        assert proof.get('preparation_mode') != 'reused'
    return proof


def _verify_input_contents(folder, plan, proof, plan_sha256):
    from .variants import verify_baseline
    verify_baseline(plan)
    from .coverage import verify_reference
    verify_reference(plan)
    assert proof['plan_sha256'] == plan_sha256 and proof['sources'] == plan['sources']
    assert proof['sdk'] == plan['proposed_dotnet_sdk']
    assert manifest(folder/'common') == proof['common_manifest']
    assert manifest(folder/'evidence') == proof['evidence_manifest']
    for label in ('old', 'successor', 'variants'):
        assert digest(folder/'production'/(label+'.dll')) == proof['production_sha256'][label]
        expected = cpu_test_classes(plan, label)
        actual = verify_trx(folder/'evidence'/(label+'.trx'), expected)
        assert all(proof['checks'][label][k] == v for k,v in actual.items())
        assert proof['checks'][label]['original_gpu_source'] == original_gpu_source(plan, label)
        receipt = read(folder/'evidence'/(label+'-original-gpu-receipt.json'))
        raw = gzip.decompress((folder/'evidence'/(label+'-original-gpu-gpu.trx.gz')).read_bytes())
        assert hashlib.sha256(raw).hexdigest() == receipt['trx_sha256']
        results = ET.fromstring(raw).findall('.//{*}UnitTestResult')
        assert len(results) == receipt['passed_cases'] == 100
        assert all(r.attrib['outcome'] == 'Passed' for r in results)
    assert verify_trx(folder/'evidence/variant-harness.trx', ['SharedClassicalWindowVariantBenchmarkTests']) == proof['checks']['variant-harness']
    assert proof['checks']['variant-harness']['passed'] == 13
    return proof


def prepare(out, reuse_from=None):
    plan = read(PLAN)
    assert subprocess.check_output(['dotnet', '--version'], text=True).strip() == plan['proposed_dotnet_sdk']
    # rev-parse of a bare 40-character SHA echoes even a missing object.
    # Resolve the commit type for every input before spending time on builds.
    for source in {*plan['sources'].values(), *(original_gpu_source(plan, label) for label in ('old', 'successor', 'variants'))}:
        assert git('rev-parse', '--verify', source + '^{commit}') == source
    from .variants import verify_baseline
    verify_baseline(plan)
    from .coverage import verify_reference
    verify_reference(plan)
    suite = os.environ.get('HOSTED_TIMING_SUITE')
    if suite:
        expected = ('qualified-coverage' if plan.get('coverage_reference') else
                    'qualified-variants' if plan.get('standalone_variants') else 'qualified-rollout')
        assert suite == expected
    if plan.get('reuse_inputs'):
        assert reuse_from is not None, 'Download the pinned preparation first'
        from .reuse import prepare_reused
        prepare_reused(reuse_from, out)
        return
    assert reuse_from is None
    assert not out.exists(), 'Use a fresh preparation output directory'
    out.mkdir(parents=True)
    evidence = out / 'evidence'
    evidence.mkdir()
    sources = {}
    checks = {}
    source_root = Path(os.environ.get('RUNNER_TEMP', str(out.parent))) / ('si-sources-' + uuid.uuid4().hex)
    source_root.mkdir()
    for label in ('old', 'successor', 'variants'):
        source = plan['sources'][label]
        assert git('rev-parse', source) == source
        tree = source_root / label
        git('worktree', 'add', '--detach', '--no-checkout', str(tree), source)
        git('sparse-checkout', 'set', 'src', 'tests', 'examples', 'scripts', BENCH, TESTS, cwd=tree)
        git('checkout', cwd=tree)
        assert git('rev-parse', 'HEAD', cwd=tree) == source
        selected = cpu_test_classes(plan, label)
        command(['dotnet', 'build', 'tests/OoplesFinance.StockIndicators.Tests.Unit.csproj',
                 '-c', 'Release', '-p:GeneratePackageOnBuild=false', '-m:2'], tree, evidence / (label + '-build.log'))
        unit = tree / 'tests/bin/Release/net10.0/OoplesFinance.StockIndicators.Tests.Unit.dll'
        command(['dotnet', 'vstest', unit, '/TestCaseFilter:' + '|'.join('FullyQualifiedName~' + c for c in selected),
                 '/Logger:trx;LogFileName=' + label + '.trx', '/ResultsDirectory:' + str(evidence)],
                tree, evidence / (label + '-tests.log'))
        checks[label] = verify_trx(evidence / (label + '.trx'), selected)
        sources[label] = tree
    # The existing benchmark inputs are unchanged between the timing arms. The
    # variant harness adds only its dedicated fixture and correctness test.
    paths = [BENCH, TESTS, 'Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props', 'global.json']
    assert not git('diff', '--name-only', plan['sources']['old'], plan['sources']['successor'], '--', *paths)
    additions = set(git('diff', '--name-only', plan['sources']['successor'], plan['sources']['variants'], '--', *paths).splitlines())
    assert additions == {BENCH + '/LibraryComparisons/SharedClassicalWindowVariantBenchmarks.cs',
                         TESTS + '/SharedClassicalWindowVariantBenchmarkTests.cs'}, additions
    variant_tree = sources['variants']
    command(['dotnet', 'build', TESTS, '-c', 'Release', '-p:GeneratePackageOnBuild=false', '-m:2'],
            variant_tree, evidence / 'variant-harness-build.log')
    command(['dotnet', 'vstest', variant_tree / TESTS / 'bin/Release/net10.0/OoplesFinance.StockIndicators.CompetitorTests.dll',
             '/TestCaseFilter:FullyQualifiedName~SharedClassicalWindowVariantBenchmarkTests',
             '/Logger:trx;LogFileName=variant-harness.trx', '/ResultsDirectory:' + str(evidence)],
            variant_tree, evidence / 'variant-harness-tests.log')
    checks['variant-harness'] = verify_trx(evidence / 'variant-harness.trx', ['SharedClassicalWindowVariantBenchmarkTests'])
    assert checks['variant-harness']['passed'] == 13
    shutil.copytree(variant_tree / BENCH / 'bin/Release/net10.0', out / 'common')
    (out / 'production').mkdir()
    production = {}
    source_trees = {}
    for label, tree in sources.items():
        source_dll = tree / 'src/bin/Release/net10.0' / DLL
        target = out / 'production' / (label + '.dll')
        shutil.copy2(source_dll, target)
        production[label] = digest(target)
        source_trees[label] = git('rev-parse', plan['sources'][label] + ':src')
        assert not git('status', '--porcelain', cwd=tree)
        # Original GPU evidence is retained with its source identity, never
        # represented as a hosted GPU run. The original plan predates its variant
        # fixture; repaired plans pin fresh evidence for their complete source.
        gpu_source = original_gpu_source(plan, label)
        assert git('rev-parse', gpu_source + ':src') == source_trees[label]
        for name in ('receipt.json', 'gpu.trx.gz'):
            raw = subprocess.check_output(['git', 'show', gpu_source + ':benchmarks/results/shared-rollout/local-gpu-contracts/' + name], cwd=REPO)
            (evidence / (label + '-original-gpu-' + name)).write_bytes(raw)
        gpu_receipt = read(evidence / (label + '-original-gpu-receipt.json'))
        gpu_trx = gzip.decompress((evidence / (label + '-original-gpu-gpu.trx.gz')).read_bytes())
        assert hashlib.sha256(gpu_trx).hexdigest() == gpu_receipt['trx_sha256']
        gpu_results = ET.fromstring(gpu_trx).findall('.//{*}UnitTestResult')
        assert len(gpu_results) == gpu_receipt['passed_cases'] == 100
        assert all(r.attrib['outcome'] == 'Passed' for r in gpu_results)
        checks[label]['original_gpu_source'] = gpu_source
    proof = dict(plan_sha256=digest(PLAN), sources=plan['sources'], source_trees=source_trees,
                 production_sha256=production, common_manifest=manifest(out/'common'),
                 evidence_manifest=manifest(evidence), checks=checks,
                 sdk=plan['proposed_dotnet_sdk'], dotnet_info=subprocess.check_output(['dotnet','--info'],text=True),
                 workflow_head=os.environ.get('GITHUB_SHA', git('rev-parse','HEAD')),
                 run_id=os.environ.get('GITHUB_RUN_ID'), run_attempt=os.environ.get('GITHUB_RUN_ATTEMPT'))
    write(out/'provenance.json', proof)
    print('Prepared immutable source-qualified hosted inputs.', flush=True)


def trix_diagnostic_tasks(plan, library=False):
    tasks = phase_tasks(plan, 'successor', 1 if library else 6)
    fixture = 'LibraryPairBenchmarks' if library else 'SharedSkenderEmaFamilyBenchmarks'
    pair = 'TaLib.Functions.Trix' if library else 'Skender.GetTrix'
    task = next(t for t in tasks if t['fixture'] == fixture)
    task['operations'] = [op for op in task['operations'] if op['pair'] == pair]
    assert len(task['operations']) == 1
    task.update(methods=['Ooples'] if library else ['BuilderFull', 'BuilderLatestOnly'],
        cases=8 if library else 16, decisions=2 if library else 4)
    return [task]


def trix_preflight_tasks(plan):
    selected = {}
    for shard in range(8):
        for task in phase_tasks(plan, 'successor', shard):
            operations = [op for op in task['operations'] if op['pair'] in ('Skender.GetTrix', 'TaLib.Functions.Trix')]
            if not operations:
                continue
            fixture = task['fixture']
            if fixture not in selected:
                task.update(operations=[], methods=[m for m in task['methods'] if m in OWNED])
                selected[fixture] = task
            selected[fixture]['operations'].extend(operations)
    for task in selected.values():
        task['decisions'] = len(task['operations']) * len(task['methods']) * 2
        task['cases'] = task['decisions'] * 4
    assert sum(t['cases'] for t in selected.values()) == 48
    return list(selected.values())


def measure(inputs, out, phase, shard, diagnostic=None):
    import psutil
    plan = read(PLAN)
    proof = read(inputs/'provenance.json')
    assert proof['plan_sha256'] == digest(PLAN) and proof['sources'] == plan['sources']
    assert manifest(inputs/'common') == proof['common_manifest']
    assert manifest(inputs/'evidence') == proof['evidence_manifest']
    assert proof['sdk'] == subprocess.check_output(['dotnet','--version'],text=True).strip()
    tasks = phase_tasks(plan, phase, shard)
    diagnostic_env = {}
    if diagnostic == 'trix-preflight':
        assert phase == 'successor' and shard == 0
        tasks = trix_preflight_tasks(plan)
    elif diagnostic:
        library = diagnostic.startswith('trix-library-')
        assert diagnostic in ('trix-jit', 'trix-no-pgo', 'trix-library-jit', 'trix-library-no-tiering', 'trix-library-warmup')
        assert phase == 'successor' and shard == (1 if library else 6)
        tasks = trix_diagnostic_tasks(plan, library)
        if diagnostic != 'trix-library-warmup':
            diagnostic_env = dict(DOTNET_JitDisasm='Smooth Round Ratio TryRatio Update TrySmooth SmoothSameBinade FromDouble TryDouble Mean Append Fill AddExact Multiply Promote Normalize RoundedDouble CompactDivRem Bits',
                DOTNET_JitDisasmAssemblies='OoplesFinance.StockIndicators',
                DOTNET_JitPrintInlinedMethods='1', DOTNET_TieredPGO='0' if diagnostic == 'trix-no-pgo' else '1')
            if diagnostic == 'trix-library-no-tiering':
                diagnostic_env['DOTNET_TieredCompilation'] = '0'
    labels = phase_labels(phase)
    assert not out.exists(), 'Every shard attempt must start fresh on one VM'
    out.mkdir(parents=True)
    runtimes = {}
    manifests = {}
    for arm, label in zip(('old','new'), labels):
        production = inputs/'production'/(label+'.dll')
        assert digest(production) == proof['production_sha256'][label]
        runtime = out.parent / (out.name + '-runtime-' + arm)
        shutil.copytree(inputs/'common', runtime)
        shutil.copy2(production, runtime/DLL)
        runtimes[arm] = runtime
        manifests[arm] = manifest(runtime)
    assert manifests['old'].keys() == manifests['new'].keys()
    expected_changes = [] if phase == 'coverage' else [DLL]
    assert [p for p in manifests['old'] if manifests['old'][p] != manifests['new'][p]] == expected_changes
    identity = dict(machine=platform.node(), platform=platform.platform(), cpu=platform.processor(),
                    logical_cpus=psutil.cpu_count(), runner_image=os.environ.get('ImageVersion'),
                    run_id=os.environ.get('GITHUB_RUN_ID'), run_attempt=os.environ.get('GITHUB_RUN_ATTEMPT'),
                    job=os.environ.get('GITHUB_JOB'), instance_nonce=uuid.uuid4().hex)
    write(out/'provenance.json', dict(phase=phase, shard=shard, raw_root=str(out), host=identity,
        sources=phase_sources(plan, phase), input_provenance=proof, input_provenance_sha256=digest(inputs/'provenance.json'),
        plan_sha256=digest(PLAN), binary_manifest=manifests, diagnostic=diagnostic,
        diagnostic_environment=diagnostic_env, started_utc=datetime.datetime.now(datetime.timezone.utc).isoformat()))
    state = dict(status='starting', phase=phase, shard=shard, order=['old','new','new','old'], rounds=[])
    last_print = [0.0]
    waiting_since = [None]

    def save():
        now = time.monotonic()
        if state['status'] == 'waiting-quiet-window':
            if waiting_since[0] is None:
                waiting_since[0] = now
            if now - waiting_since[0] > 15*60:
                raise TimeoutError('No qualifying idle window within 15 minutes; gates unchanged')
        else:
            waiting_since[0] = None
        state['updated_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        write(out/'status.json',state)
        if now - last_print[0] >= 50:
            print(json.dumps({k:state.get(k) for k in ('status','round','fixture','operation','checkpointed_operations','waiting_build_pids')}),flush=True)
            last_print[0] = now

    def builds():
        found = []
        for process in psutil.process_iter(['name','cmdline']):
            args = process.info['cmdline'] or []
            if (process.info['name'] or '').lower() in ('dotnet.exe','dotnet') and any(a in args for a in ('build','test','vstest','publish','restore','pack')):
                found.append(process.pid)
        return found

    def assert_binaries():
        assert shutil.disk_usage(out).free > 2*1024**3, 'Below 2 GB timing reserve'
        assert all(manifest(runtimes[a]) == manifests[a] for a in runtimes)

    env = os.environ.copy()
    env.update(diagnostic_env)
    for key in ('COMPARISON_PAIR','COMPARISON_SHARD','COMPARISON_SHARDS','COMPARISON_FAMILY'):
        env.pop(key,None)
    try:
        for index, arm in enumerate(state['order']):
            for task in tasks:
                record = run_checkpointed(out=out,index=index,arm=arm,fixture=task['fixture'],
                    pairs=[op['raw'] for op in task['operations']],methods=methods_for(task,index),
                    runtime=runtimes[arm],env=env,state=state,save=save,builds=builds,
                    assert_binaries=assert_binaries,implicit=task.get('implicit_pair',False),variant=task.get('variant',False),
                    context=dict(host_nonce=identity['instance_nonce'],production_sha256=manifests[arm][DLL],phase=phase,shard=shard,
                        **warmup_context(plan, task['fixture']),
                        **({'diagnostic_warmups':1000} if diagnostic == 'trix-library-warmup' else {})),
                    warmups=1000 if diagnostic == 'trix-library-warmup' else warmups_for(plan, task['fixture']))
                state['rounds'].append(record)
                state['status'] = 'round-complete'
                save()
        assert sum(r['cases'] for r in state['rounds']) == sum(t['cases'] for t in tasks)
        assert_binaries()
        state['status'] = 'raw-measurements-complete'
        save()
    except BaseException as error:
        state.update(status='failed',error=repr(error))
        save()
        raise


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('mode',choices=['prepare','measure','collect','collect-preflight'])
    parser.add_argument('--inputs',type=Path,default=Path('hosted-inputs'))
    parser.add_argument('--reuse-from',type=Path)
    parser.add_argument('--prepared',type=Path,default=Path('hosted-inputs'))
    parser.add_argument('--out',type=Path,required=True)
    parser.add_argument('--phase',choices=['successor','variants','coverage'],default='successor')
    parser.add_argument('--shard',type=int,default=0)
    parser.add_argument('--diagnostic', choices=['trix-jit', 'trix-no-pgo', 'trix-library-jit', 'trix-library-no-tiering', 'trix-library-warmup', 'trix-preflight'])
    args = parser.parse_args()
    if args.mode == 'prepare':
        prepare(args.out.resolve(), args.reuse_from)
        if os.environ.get('GITHUB_OUTPUT'):
            with open(os.environ['GITHUB_OUTPUT'],'a',encoding='utf-8') as output:
                output.write('trix_preflight=' + str(read(PLAN).get('trix_preflight', False)).lower() + '\n')
                output.write('standalone_variants=' + str(bool(read(PLAN).get('standalone_variants'))).lower() + '\n')
                output.write('coverage_only=' + str(bool(read(PLAN).get('coverage_reference'))).lower() + '\n')
    elif args.mode == 'measure':
        measure(args.inputs.resolve(),args.out.resolve(),args.phase,args.shard,args.diagnostic)
    else:
        from .verify import collect, collect_preflight
        verify_inputs(args.prepared.resolve())
        summary = collect_preflight(args.inputs.resolve()) if args.mode == 'collect-preflight' else collect(args.inputs.resolve(),args.phase)
        assert summary['input_provenance_sha256'] == digest(args.prepared/'provenance.json')
        write(args.out.resolve(),summary)
        fields = ['case_count', 'decision_count', 'accepted', 'verdicts']
        if args.phase == 'coverage':
            fields.append('coverage_complete')
        print(json.dumps({k:summary[k] for k in fields}))
        if os.environ.get('GITHUB_OUTPUT'):
            with open(os.environ['GITHUB_OUTPUT'],'a',encoding='utf-8') as output:
                output.write('accepted=' + str(summary['accepted']).lower() + '\n')
                output.write('run_variants=' + str(args.mode == 'collect' and summary['accepted'] and read(PLAN).get('qualify_variants', True)).lower() + '\n')
