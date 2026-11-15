"""Source-bound, aligned-harness AdOsc diagnostics; never source qualification."""
import gzip
import hashlib
import importlib.util
import io
import os
from pathlib import Path
import re
import subprocess
import tempfile

from . import pipeline as p

APPROVAL = 'benchmarks/plans/evidence/adosc-published-columns-scope-20261115.json'
PUBLISHED = 'e7112835a8f3f29fc27363bcd0c553e77bc7c2c5'
SOURCES = dict(published=PUBLISHED, old='a458350088fff173c3cba1f74f627b16bf1b8a7a',
               successor='1edfc5a9f7a2a0f466e5d96e62501522e4bfdeee', variants='ac91ae98890678982fac7efb88a49be08e53699b')
PARENTS = dict(old=PUBLISHED, successor='5d3b5125c56cec671a1ca7fe0547b1b1a3b7f6eb',
               variants='ce994435f670a1c2c8d5424e167316914cfe9b51')
RUNS = dict(old='43676943941', successor='43676961401', variants='43676924926')
PRODUCER = '0857eefc242585a9f44bfa3c134ea0bd3eb1b30a'
PAIRS = ['Skender.GetChaikinOsc', 'TaLib.Functions.AdOsc']
UNITS = dict(old=['CompactSmoothedAccumulationTests'],
             successor=['SmoothedCheckedRegionTests', 'CompactSmoothedAccumulationTests'],
             variants=['CompactMacdWordEmaTests', 'CompactClassicalWindowTests', 'ExactMeanUnitWeightTests'])
PARITY = dict(old=['SmoothedAccumulationComparisonTests', 'SharedSmoothedAccumulationFixtureTests', 'SmoothedAccumulationPublisherTests'],
              successor=['SmoothedAccumulationComparisonTests', 'SharedSmoothedAccumulationFixtureTests', 'SmoothedAccumulationPublisherTests'],
              variants=['SharedClassicalWindowVariantBenchmarkTests', 'SmoothedAccumulationPublisherTests'])
COUNTS = dict(old=(15, 22), successor=(21, 22), variants=(54, 15))
POLICY = dict(order=['old', 'new', 'new', 'old'], quiet_samples=12, max_cpu_percent=15,
              warmups=10, iterations=10, fixed_invocation_warmups=1000)
REPAIRED = {'benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/LibraryComparisons/SmoothedAccumulationComparison.cs',
            'benchmarks/OoplesFinance.StockIndicators.CompetitorTests/SmoothedAccumulationPublisherTests.cs'}
PATHS = ['src', 'benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks',
         'benchmarks/OoplesFinance.StockIndicators.CompetitorTests', 'scripts/run-competitor-contracts.py',
         'scripts/verify-competitor-local-gpu.py', 'scripts/competitor-gpu-methods.json',
         'Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props',
         'global.json', 'NuGet.Config', 'nuget.config']


def validate_scope(plan):
    approval = p.read(p.REPO / APPROVAL)
    assert approval['approved'] and approval['diagnostic_only'] and not approval['qualification']
    assert approval['sources'] == SOURCES and approval['cases'] == 20 and approval['max_parallel'] == 1
    assert plan['aligned_roi_approval'] == dict(path=APPROVAL, sha256=p.digest(p.REPO / APPROVAL))
    assert plan['sources'] == SOURCES and plan['gpu_sources'] == {k:SOURCES[k] for k in UNITS}
    assert plan['cpu_test_classes'] == UNITS
    assert plan['diagnostic_only'] and plan['diagnostic_kind'] == 'adosc-published-columns'
    assert plan['runtime_policy_overrides'] == {} and plan['measurement_policy'] == POLICY
    assert plan['runtime_dependency_alignment'] == dict(tensor_version='0.150.0')
    assert plan['proposed_dotnet_sdk'] == '10.0.403' and plan['require_fixture_inventory']
    assert plan['cpu_diagnostic'] == dict(after_measurement=True,optional=True,tool='PerfView4.0.0',
        sha256='270e2ba419b0b2d93c4b15b0851f257edf655c92fe0ad95add3d0b5f6d6aad48',measured_cases=0)
    assert (plan['cases'], plan['decisions'], plan['matrix']['shards'], plan['matrix']['max_parallel']) == (20, 4, 1, 1)
    task = dict(fixture='LibraryPairBenchmarks', operations=[dict(raw=x, pair=x) for x in PAIRS],
                methods=['Ooples', 'Competitor'], implicit_pair=False, cases=20, decisions=4)
    assert plan['diagnostic_shards'] == plan['targeted_shards'] == [dict(index=0, tasks=[task], cases=20, decisions=4)]
    assert not any(plan.get(k) for k in ('targeted_reference', 'coverage_reference', 'reuse_inputs', 'qualify_variants', 'trix_preflight'))
    assert set(plan['ci_inputs']) == set(UNITS)
    for label, spec in plan['ci_inputs'].items():
        assert spec['run_id'] == RUNS[label] and spec['source'] == SOURCES[label] and spec['producer'] == PRODUCER
        assert re.fullmatch('[a-f0-9]{40}', spec['evidence_source'])
        assert all(re.fullmatch('[a-f0-9]{64}', spec[k]) for k in ('manifest_sha256', 'gpu_receipt_sha256'))


def prefetch_source_blobs(source):
    promisor = subprocess.run(['git', 'config', '--get', 'remote.origin.promisor'], cwd=p.REPO, capture_output=True, text=True)
    if promisor.returncode or promisor.stdout.strip() != 'true': return
    entries = p.git('ls-tree', '-r', source, '--', *PATHS).splitlines()
    blobs = sorted({line.split()[2] for line in entries if line.split()[1] == 'blob'})
    assert blobs and all(re.fullmatch('[a-f0-9]{40}', x) for x in blobs)
    subprocess.run(['git', '-c', 'fetch.negotiationAlgorithm=noop', 'fetch', '--no-tags', '--no-write-fetch-head', 'origin', '--stdin'],
                   cwd=p.REPO, input=('\n'.join(blobs)+'\n').encode(), check=True, timeout=180)


def source_fingerprint(source):
    prefetch_source_blobs(source)
    names = sorted(set(p.git('ls-tree', '-r', '--name-only', source, '--', *PATHS).splitlines()))
    raw = subprocess.check_output(['git', 'cat-file', '--batch'], cwd=p.REPO, input=''.join(source+':'+n+'\n' for n in names).encode())
    stream, digest = io.BytesIO(raw), hashlib.sha256()
    for name in names:
        header = stream.readline().split(); assert len(header) == 3 and header[1] == b'blob'
        content = stream.read(int(header[2])).replace(b'\r\n', b'\n'); assert stream.read(1) == b'\n'
        digest.update(name.encode()+b'\0'+hashlib.sha256(content).digest())
    assert not stream.read()
    return digest.hexdigest()


def authenticate(folder, label, plan):
    folder = Path(folder).resolve(); spec = plan['ci_inputs'][label]
    def read(name):
        path = (folder/name).resolve(); assert path.is_relative_to(folder) and path.is_file()
        return path.read_bytes()
    loader = importlib.util.spec_from_file_location('aligned_ci_inputs', p.REPO/'scripts/build-candidate-correctness.py')
    ci = importlib.util.module_from_spec(loader); loader.loader.exec_module(ci)
    raw = read('manifest.json'); assert hashlib.sha256(raw).hexdigest() == spec['manifest_sha256']
    import json
    manifest = json.loads(raw)
    assert (manifest['run_id'], manifest['workflow_head']) == (RUNS[label], PRODUCER)
    assert manifest['classes'] == dict(unit=UNITS[label], parity=PARITY[label])
    fingerprint = source_fingerprint(SOURCES[label])
    production = ci.validate_layout(manifest, read, SOURCES[label], fingerprint)
    for kind, classes, count in [('unit', UNITS[label], COUNTS[label][0]), ('parity', PARITY[label], COUNTS[label][1])]:
        with tempfile.TemporaryDirectory(prefix='si-aligned-trx-') as tmp:
            directory = Path(tmp).resolve(); assert directory.parent == Path(tempfile.gettempdir()).resolve()
            path = directory/'cases.trx'; path.write_bytes(read('evidence/'+kind+'.trx'))
            actual = p.verify_trx(path, classes)
        assert actual == manifest['checks'][kind] and actual['passed'] == count
    evidence = spec['evidence_source']
    assert not p.git('diff', '--name-only', SOURCES[label], evidence, '--', *PATHS, 'tests')
    base = 'benchmarks/results/shared-rollout/local-gpu-contracts/'
    gpu_raw = subprocess.check_output(['git', 'show', evidence+':'+base+'receipt.json'], cwd=p.REPO)
    gpu_trx = subprocess.check_output(['git', 'show', evidence+':'+base+'gpu.trx.gz'], cwd=p.REPO)
    assert hashlib.sha256(gpu_raw).hexdigest() == spec['gpu_receipt_sha256']
    receipt = json.loads(gpu_raw); gpu = ci.gpu_module(p.REPO)
    methods = json.loads(p.git('show', SOURCES[label]+':scripts/competitor-gpu-methods.json'))
    assert gpu.verify_evidence(receipt, gzip.decompress(gpu_trx), methods, fingerprint) == 100
    assert receipt['build_origin']['source'] == SOURCES[label] and receipt['build_origin']['run_id'] == RUNS[label]
    assert receipt['build_origin']['workflow_head'] == PRODUCER
    assert receipt['test_assembly_sha256'] == manifest['runtime_files'][ci.ROOTS[-1]+'/OoplesFinance.StockIndicators.CompetitorTests.dll']
    from .runtime_dependencies import tensor_closure
    with tempfile.TemporaryDirectory(prefix='si-aligned-unit-') as tmp:
        runtime = Path(tmp).resolve(); assert runtime.parent == Path(tempfile.gettempdir()).resolve()
        prefix = ci.ROOTS[1]+'/'
        for name, sha in manifest['runtime_files'].items():
            if name.startswith(prefix):
                target = runtime/name[len(prefix):]; target.parent.mkdir(parents=True, exist_ok=True); target.write_bytes(read('blobs/'+sha))
        closure = tensor_closure(runtime, 'OoplesFinance.StockIndicators.Tests.Unit.deps.json')
    return dict(manifest=manifest, manifest_raw=raw, read=read, production=production, fingerprint=fingerprint,
                gpu_receipt=gpu_raw, gpu_trx=gpu_trx, closure=closure, roots=ci.ROOTS)


def verify_authentication(folder, plan, proof):
    validate_scope(plan)
    assert proof['source_authentication'] == plan['ci_inputs']
    for label in UNITS:
        spec = plan['ci_inputs'][label]; path = folder/'evidence'/(label+'-ci-manifest.json')
        assert p.digest(path) == spec['manifest_sha256']
        manifest = p.read(path)
        assert manifest['source'] == SOURCES[label] and manifest['run_id'] == RUNS[label] and manifest['workflow_head'] == PRODUCER
        assert manifest['classes'] == dict(unit=UNITS[label], parity=PARITY[label])
        assert p.digest(folder/'production'/(label+'.dll')) == manifest['runtime_files']['src/bin/Release/net10.0/'+p.DLL]
        for kind, classes, expected in [('unit',UNITS[label],COUNTS[label][0]),('parity',PARITY[label],COUNTS[label][1])]:
            actual = p.verify_trx(folder/'evidence'/(label+('-parity' if kind=='parity' else '')+'.trx'), classes)
            assert actual == manifest['checks'][kind] and actual['passed'] == expected
        for name, sha in manifest['evidence'].items():
            assert p.digest(folder/'evidence'/('ci-'+label)/name) == sha
        assert p.digest(folder/'evidence'/(label+'-original-gpu-receipt.json')) == spec['gpu_receipt_sha256']
        for name, sha in proof['tested_runtime_dependencies'][label]['files'].items():
            assert manifest['runtime_files']['tests/bin/Release/net10.0/'+name] == sha


def prepare(folders, out):
    plan = p.read(p.PLAN); validate_scope(plan); out = Path(out).resolve(); assert not out.exists()
    assert subprocess.check_output(['dotnet','--version'],text=True).strip() == '10.0.403'
    refs = set(SOURCES.values()) | set(PARENTS.values()) | {s['evidence_source'] for s in plan['ci_inputs'].values()}
    for source in sorted(refs):
        if subprocess.run(['git','cat-file','-e',source+'^{commit}'],cwd=p.REPO,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL).returncode:
            subprocess.run(['git','fetch','origin',source],cwd=p.REPO,check=True)
    for label in UNITS:
        assert set(p.git('diff','--name-only',PARENTS[label],SOURCES[label]).splitlines()) == REPAIRED
        assert not p.git('diff','--name-only',PARENTS[label],SOURCES[label],'--','src','tests')
    target = 'benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/LibraryComparisons/'
    for name in ('LibraryPairBenchmarks.cs','SmoothedAccumulationComparison.cs','SharedSmoothedAccumulationBenchmarks.cs','RetrospectivePriceComparison.cs'):
        assert len({p.git('rev-parse',SOURCES[label]+':'+target+name) for label in UNITS}) == 1
    data = {label:authenticate(folders[label], label, plan) for label in UNITS}
    (out/'evidence').mkdir(parents=True); (out/'production').mkdir(); (out/'common').mkdir()
    checks = {}
    for label, item in data.items():
        read = item['read']; (out/'evidence'/(label+'-ci-manifest.json')).write_bytes(item['manifest_raw'])
        for name in item['manifest']['evidence']:
            dest = out/'evidence'/('ci-'+label)/name; dest.parent.mkdir(parents=True,exist_ok=True); dest.write_bytes(read(name))
        for name, path in [('unit',label),('parity',label+'-parity')]:
            (out/'evidence'/(path+'.trx')).write_bytes(read('evidence/'+name+'.trx'))
        (out/'evidence'/(label+'-original-gpu-receipt.json')).write_bytes(item['gpu_receipt'])
        (out/'evidence'/(label+'-original-gpu-gpu.trx.gz')).write_bytes(item['gpu_trx'])
        (out/'production'/(label+'.dll')).write_bytes(read('blobs/'+item['production']))
        checks[label] = dict(item['manifest']['checks']['unit'],original_gpu_source=SOURCES[label])
    variant = data['variants']; prefix = variant['roots'][2]+'/'
    for name, sha in variant['manifest']['runtime_files'].items():
        if name.startswith(prefix):
            dest = (out/'common'/name[len(prefix):]).resolve(); assert dest.is_relative_to(out/'common')
            dest.parent.mkdir(parents=True,exist_ok=True); dest.write_bytes(variant['read']('blobs/'+sha))
    (out/'evidence/variant-harness.trx').write_bytes(variant['read']('evidence/parity.trx'))
    checks['variant-harness'] = variant['manifest']['checks']['parity']
    from .runtime_dependencies import tensor_closure, verify_alignment
    common = tensor_closure(out/'common','OoplesFinance.StockIndicators.CompetitorBenchmarks.deps.json')
    for item in data.values():verify_alignment(common,item['closure'],'0.150.0')
    proof = dict(plan_sha256=p.digest(p.PLAN),sources=SOURCES,source_trees={k:p.git('rev-parse',SOURCES[k]+':src') for k in UNITS},
                 production_sha256={k:v['production'] for k,v in data.items()},common_manifest=p.manifest(out/'common'),
                 evidence_manifest=p.manifest(out/'evidence'),checks=checks,sdk='10.0.403',
                 dotnet_info=subprocess.check_output(['dotnet','--info'],text=True),workflow_head=p.git('rev-parse','HEAD'),
                 run_id=os.environ['GITHUB_RUN_ID'],run_attempt=os.environ['GITHUB_RUN_ATTEMPT'],
                 tested_runtime_dependencies={k:v['closure'] for k,v in data.items()},
                 preparation_mode='authenticated-aligned-diagnostic',diagnostic_only=True,source_authentication=plan['ci_inputs'])
    from .fixture_inventory import record_inventory
    proof['fixture_inventory_sha256'] = record_inventory(out,plan,p.digest(p.PLAN))
    p.write(out/'provenance.json',proof); p.verify_inputs(out)
    return proof


def collect(folder):
    from .verify import _collect
    result = _collect(Path(folder),'diagnostic',preflight=False,diagnostic=True)
    assert not result['accepted'] and (result['case_count'],result['decision_count']) == (20,4)
    result.update(diagnostic_only=True,qualification=False,production_integrated=False,report_imported=False,policy_changed=False)
    return result
