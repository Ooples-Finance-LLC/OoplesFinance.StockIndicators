"""Authenticate original CI runtimes, then execute only after parent review."""
import argparse
import gzip
import hashlib
import importlib.util
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).parent.resolve()
OPS = ROOT.parent
REPO = Path(r'C:\Users\cheat\source\repos\Ooples-Finance-LLC\si-three-stars-blocks-20261116')
CACHE = Path(r'C:\Users\cheat\source\repos\Ooples-Finance-LLC\si-pointwise-source-study-20261115')
INPUTS = OPS/'active-performance-evidence/hosted-runs/43880487485/inputs'
REMOTE = 'Ooples-Finance-LLC/OoplesFinance.StockIndicators'
SOURCE = '5be1a81d02ad01393f3fa8ede764e914252d0e96'
FROZEN = '9a45372530e49718b01b6524f9a0db993514e1e8'
PRODUCER = '0857eefc242585a9f44bfa3c134ea0bd3eb1b30a'
CI = '43869376457'
MANIFEST_SHA = '8f77a62b0e3c7426569d8f6eb43f025bfe6fed544f127da9c8a9b4cbdc146776'
FINGERPRINT = 'baa78e2eb12116e886c8590b8fd951556040690c180cff61c519f2e57131225f'
PRODUCTION = 'c63770e43fceda43a2286cb5272643ac3850adcc23962cc01eee2c7a9032c6fe'
TEST_DLL = '0560474677efa9c9ffb1190a8dc52e595b06438de7b6ab651727a0d24ceb78d3'
BENCH_DLL = '48229494a1ff1e4e59bddd5a018cbd590cae24523d52dd2f3effdd20bd802723'
PREFIX = 'benchmarks/OoplesFinance.StockIndicators.CompetitorTests/bin/Release/net10.0/'
CLASSES = ['WilderAverageComparisonTests', 'MassNormalizedComparisonTests', 'SampleShapeComparisonTests',
           'McGinleyComparisonTests', 'ExhaustionTests', 'MatchedLinesTests', 'TripleBodyTests', 'AverageRangeDojiTests']
NAMESPACE = 'OoplesFinance.StockIndicators.CompetitorTests.'
DOTNET = ['dotnet', 'exec', r'C:\Program Files\dotnet\sdk\10.0.101\dotnet.dll']
FINGERPRINT_PATHS = ['src', 'benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks',
    'benchmarks/OoplesFinance.StockIndicators.CompetitorTests', 'scripts/run-competitor-contracts.py',
    'scripts/verify-competitor-local-gpu.py', 'scripts/competitor-gpu-methods.json',
    'Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props',
    'global.json', 'NuGet.Config', 'nuget.config']


def digest(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def write(name, value):
    (ROOT/name).write_bytes((json.dumps(value, indent=2)+'\n').encode())


def gh(*args):
    return subprocess.check_output(['gh', *args], cwd=ROOT)


def git(*args):
    return subprocess.check_output(['git', *args], cwd=REPO).decode().strip()


def runtime_manifest(folder):
    return {p.relative_to(folder).as_posix(): digest(p) for p in folder.rglob('*') if p.is_file()}


def safe_target(folder, relative):
    path = PurePosixPath(relative)
    assert not path.is_absolute() and '..' not in path.parts and ':' not in relative and '\\' not in relative
    target = (folder/relative).resolve()
    assert target.is_relative_to(folder.resolve()) and target != folder.resolve()
    return target


def verify_runtime(folder, expected):
    for name in expected:
        safe_target(folder, name)
    assert runtime_manifest(folder) == expected, 'Whole runtime file set or bytes changed'


def authenticate():
    assert not (ROOT/'authentication.json').exists(), 'Inspect completed authentication before repeating'
    proposal = json.loads((OPS/'current-baseline-ten-pair-refresh-proposal-20261116.json').read_text())
    assert proposal['approved'] and proposal['canonical_production_source'] == SOURCE and proposal['frozen_harness_source'] == FROZEN
    state = json.loads(gh('run', 'view', CI, '--repo', REMOTE, '--json', 'status,conclusion,headSha'))
    assert state == dict(status='completed', conclusion='success', headSha=PRODUCER)
    original = OPS/'named-output-projection-refresh-20261116/ci-manifest.json'
    assert digest(original) == MANIFEST_SHA
    manifest = json.loads(original.read_text())
    assert manifest['source'] == SOURCE and manifest['source_sha256'] == FINGERPRINT
    assert manifest['run_id'] == CI and manifest['workflow_head'] == PRODUCER and manifest['sdk'] == '10.0.403'
    assert manifest['runtime_files'][PREFIX+'OoplesFinance.StockIndicators.dll'] == PRODUCTION
    assert manifest['runtime_files'][PREFIX+'OoplesFinance.StockIndicators.CompetitorTests.dll'] == TEST_DLL
    assert not git('diff', '--name-only', SOURCE, proposal['published_pr_head'], '--', *FINGERPRINT_PATHS, 'tests')
    differences = git('diff', '--name-only', SOURCE, FROZEN, '--',
                      'benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks',
                      'benchmarks/OoplesFinance.StockIndicators.CompetitorTests').splitlines()
    assert set(differences) == {
        'benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/LibraryComparisons/SharedClassicalWindowVariantBenchmarks.cs',
        'benchmarks/OoplesFinance.StockIndicators.CompetitorTests/SharedClassicalWindowVariantBenchmarkTests.cs'}

    artifacts = json.loads(gh('api', f'repos/{REMOTE}/actions/runs/{CI}/artifacts'))['artifacts']
    artifacts = [a for a in artifacts if a['name'] == 'candidate-correctness-inputs' and not a['expired']]
    assert len(artifacts) == 1
    artifact = artifacts[0]
    archive = Path(r'Z:\StockIndicators-Archives\hosted-runs-20261116\current-baseline-ten-preflight')/f"canonical-{CI}-artifact{artifact['id']}.zip"
    archive.parent.mkdir(parents=True, exist_ok=True)
    if not archive.exists():
        temporary = archive.with_suffix('.zip.partial')
        assert not temporary.exists(), 'Inspect partial download rather than overwriting it'
        with temporary.open('wb') as output:
            subprocess.run(['gh', 'api', f"repos/{REMOTE}/actions/artifacts/{artifact['id']}/zip"], stdout=output, check=True)
        temporary.replace(archive)
    archive_sha = digest(archive)
    if artifact.get('digest'):
        assert artifact['digest'] == 'sha256:'+archive_sha

    reused = []
    cache = {}
    for name, sha in manifest['runtime_files'].items():
        if not name.startswith(PREFIX):
            continue
        candidate = CACHE/name
        if candidate.is_file() and digest(candidate) == sha:
            cache.setdefault(sha, candidate)
    common_proof = json.loads((INPUTS/'provenance.json').read_text())
    assert digest(INPUTS/'provenance.json') == '92977e9786050146dbe97748c8a28d59e8eb47be5f4b5f85ae064ec4fcacef3a'
    assert common_proof['run_id'] == '43880487485' and common_proof['sources']['successor'] == SOURCE
    assert common_proof['common_manifest']['OoplesFinance.StockIndicators.CompetitorBenchmarks.dll'] == BENCH_DLL
    verify_runtime(INPUTS/'common', common_proof['common_manifest'])
    for name, sha in common_proof['common_manifest'].items():
        cache.setdefault(sha, INPUTS/'common'/name)

    def install(folder, relative, sha, reader):
        target = safe_target(folder, relative)
        if target.exists():
            assert digest(target) == sha
            return
        assert shutil.disk_usage(ROOT).free > 2*1024**3, 'Below C: reserve'
        target.parent.mkdir(parents=True, exist_ok=True)
        temporary = target.with_name(target.name+'.installing')
        assert not temporary.exists()
        if sha in cache:
            source = cache[sha]
            assert digest(source) == sha
            os.link(source, temporary)
            reused.append(dict(path=str(source), sha256=sha))
        else:
            temporary.write_bytes(reader())
        assert digest(temporary) == sha
        # Never write over an existing hardlink. Replace only the new entry.
        temporary.replace(target)
        cache.setdefault(sha, target)

    canonical = ROOT/'canonical-runtime'
    with zipfile.ZipFile(archive) as package:
        names = [n for n in package.namelist() if n == 'manifest.json' or n.endswith('/manifest.json')]
        assert len(names) == 1
        prefix = names[0][:-len('manifest.json')]
        assert hashlib.sha256(package.read(names[0])).hexdigest() == MANIFEST_SHA
        for sha in set(manifest['runtime_files'].values()):
            assert re.fullmatch('[a-f0-9]{64}', sha)
            with package.open(prefix+'blobs/'+sha) as stream:
                assert hashlib.file_digest(stream, 'sha256').hexdigest() == sha
        for name, sha in manifest['evidence'].items():
            assert hashlib.sha256(package.read(prefix+name)).hexdigest() == sha
        expected = {}
        for name, sha in manifest['runtime_files'].items():
            if name.startswith(PREFIX):
                relative = name[len(PREFIX):]
                install(canonical, relative, sha, lambda sha=sha: package.read(prefix+'blobs/'+sha))
                expected[relative] = sha
    verify_runtime(canonical, expected)
    frozen = ROOT/'frozen-runtime'
    composed = dict(common_proof['common_manifest'])
    composed['OoplesFinance.StockIndicators.dll'] = PRODUCTION
    for name, sha in composed.items():
        install(frozen, name, sha, lambda: (_ for _ in ()).throw(AssertionError('Every frozen dependency must already be verified in cache')))
    verify_runtime(frozen, composed)

    # Retain and load only the pure dependency-closure verifier, not build code.
    helper = ROOT/'runtime-dependencies.py'
    helper.write_bytes(subprocess.check_output(['git', 'show', 'c345b715d405e5f2ca80584a8594b5e8df16ae00:scripts/hosted_timing/runtime_dependencies.py'], cwd=REPO))
    spec = importlib.util.spec_from_file_location('preflight_dependencies', helper)
    module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
    canonical_closure = module.tensor_closure(canonical, 'OoplesFinance.StockIndicators.CompetitorTests.deps.json')
    frozen_closure = module.tensor_closure(frozen, 'OoplesFinance.StockIndicators.CompetitorBenchmarks.deps.json')
    assert canonical_closure == frozen_closure and canonical_closure['packages']['AiDotNet.Tensors'] == '0.150.0'
    baseline_plan_raw = subprocess.check_output(['git', 'show',
        '8e25682dbb10dc503efc1ce3be63b5741a644667:benchmarks/plans/hosted-named-projection-refresh-20261116.json'], cwd=REPO)
    assert hashlib.sha256(baseline_plan_raw).hexdigest() == '03e891d5220a12f0983a2f8d8d1b0de2cdc4f751cea0a07625a91dc6e21661b8'
    baseline_plan = json.loads(baseline_plan_raw)
    gpu_helpers = {}
    for name in ('verify-competitor-local-gpu.py', 'run-competitor-contracts.py'):
        raw = subprocess.check_output(['git', 'show', SOURCE+':scripts/'+name], cwd=REPO)
        assert raw == subprocess.check_output(['git', 'show', FROZEN+':scripts/'+name], cwd=REPO)
        target = ROOT/'verified-gpu-helpers/scripts'/name
        target.parent.mkdir(parents=True, exist_ok=True)
        if target.exists():
            assert target.read_bytes() == raw
        else:
            target.write_bytes(raw)
        gpu_helpers[target.relative_to(ROOT).as_posix()] = digest(target)
    spec = importlib.util.spec_from_file_location('preflight_gpu_verifier', ROOT/'verified-gpu-helpers/scripts/verify-competitor-local-gpu.py')
    gpu_verifier = importlib.util.module_from_spec(spec); spec.loader.exec_module(gpu_verifier)
    gpu_evidence = []
    for folder, source, ci, key, label in [('named-output-projection-refresh-20261116', SOURCE, CI, 'candidate', 'old'),
            ('named-projection-frozen-20261116', FROZEN, '43871304806', 'frozen_candidate', 'variants')]:
        path = OPS/folder
        pinned = baseline_plan[key]
        assert pinned['source'] == source and pinned['ci_run'] == ci and pinned['producer'] == PRODUCER
        assert digest(path/'ci-manifest.json') == pinned['manifest_sha256']
        assert digest(path/'actual-local-gpu-receipt.json') == pinned['gpu_receipt_sha256']
        assert (path/'actual-local-gpu-receipt.json').read_bytes() == (INPUTS/'evidence'/(label+'-original-gpu-receipt.json')).read_bytes()
        assert (path/'actual-local-gpu.trx.gz').read_bytes() == (INPUTS/'evidence'/(label+'-original-gpu-gpu.trx.gz')).read_bytes()
        receipt = json.loads((path/'actual-local-gpu-receipt.json').read_text())
        trx = gzip.decompress((path/'actual-local-gpu.trx.gz').read_bytes())
        old_manifest = json.loads((path/'ci-manifest.json').read_text())
        assert (old_manifest['source'], old_manifest['run_id'], old_manifest['workflow_head']) == (source, ci, PRODUCER)
        assert receipt['source_sha256'] == old_manifest['source_sha256']
        assert receipt['passed_cases'] == 100 and hashlib.sha256(trx).hexdigest() == receipt['trx_sha256']
        assert receipt['build_origin']['source'] == source and receipt['build_origin']['run_id'] == ci
        assert receipt['build_origin']['workflow_head'] == PRODUCER
        assert receipt['test_assembly_sha256'] == old_manifest['runtime_files'][PREFIX+'OoplesFinance.StockIndicators.CompetitorTests.dll']
        methods = json.loads(git('show', source+':scripts/competitor-gpu-methods.json'))
        assert gpu_verifier.verify_evidence(receipt, trx, methods, old_manifest['source_sha256']) == 100
        gpu_evidence.append(dict(source=source, run_id=ci, receipt_sha256=digest(path/'actual-local-gpu-receipt.json'), trx_sha256=receipt['trx_sha256']))
    write('authentication.json', dict(status='authenticated', source=SOURCE, frozen_source=FROZEN, canonical_ci=CI,
        canonical_manifest_sha256=MANIFEST_SHA, source_fingerprint=FINGERPRINT, production_sha256=PRODUCTION,
        canonical_test_sha256=TEST_DLL, frozen_benchmark_sha256=BENCH_DLL, classes=CLASSES, pairs=proposal['pairs'],
        canonical_runtime=expected, frozen_runtime=composed, tensor_closure=canonical_closure,
        protected_links=reused, original_artifact=artifact, archive=str(archive), archive_sha256=archive_sha,
        source_harness_diff=differences, reused_gpu_receipts=gpu_evidence,
        gpu_verification_helpers=gpu_helpers, fresh_gpu_execution=False, new_compilation=False))
    print('Authentication complete; execution is a separate parent-reviewed mode.', flush=True)


def execute():
    auth = json.loads((ROOT/'authentication.json').read_text())
    assert auth['status'] == 'authenticated' and auth['source'] == SOURCE and auth['canonical_ci'] == CI
    assert auth['production_sha256'] == PRODUCTION and auth['canonical_test_sha256'] == TEST_DLL
    assert auth['canonical_runtime']['OoplesFinance.StockIndicators.dll'] == PRODUCTION
    assert auth['canonical_runtime']['OoplesFinance.StockIndicators.CompetitorTests.dll'] == TEST_DLL
    assert auth['frozen_runtime']['OoplesFinance.StockIndicators.dll'] == PRODUCTION
    assert auth['frozen_runtime']['OoplesFinance.StockIndicators.CompetitorBenchmarks.dll'] == BENCH_DLL
    assert auth['classes'] == CLASSES and not (ROOT/'receipt.json').exists()
    verify_runtime(ROOT/'canonical-runtime', auth['canonical_runtime'])
    verify_runtime(ROOT/'frozen-runtime', auth['frozen_runtime'])
    settings = ROOT/'serial.runsettings'
    settings.write_bytes(b'<RunSettings><RunConfiguration><MaxCpuCount>1</MaxCpuCount></RunConfiguration><xUnit><ParallelizeTestCollections>false</ParallelizeTestCollections><MaxParallelThreads>1</MaxParallelThreads></xUnit></RunSettings>')
    filters = '|'.join('FullyQualifiedName~'+NAMESPACE+name+'.' for name in CLASSES)
    with (ROOT/'canonical-comparators.log').open('wb') as output:
        subprocess.run([*DOTNET, 'vstest', str(ROOT/'canonical-runtime/OoplesFinance.StockIndicators.CompetitorTests.dll'),
            '/Settings:'+str(settings), '/TestCaseFilter:'+filters, '/Logger:trx;LogFileName=canonical-comparators.trx',
            '/ResultsDirectory:'+str(ROOT/'results')], cwd=ROOT, stdout=output, stderr=subprocess.STDOUT, check=True)
    raw = (ROOT/'results/canonical-comparators.trx').read_bytes()
    document = ET.fromstring(raw)
    definitions = {t.attrib['id']: t.find('{*}TestMethod').attrib for t in document.findall('.//{*}UnitTest')}
    counts = {name: 0 for name in CLASSES}
    for result in document.findall('.//{*}UnitTestResult'):
        assert result.attrib['outcome'] == 'Passed'
        class_name = definitions[result.attrib['testId']]['className'].split(',')[0]
        assert class_name.startswith(NAMESPACE)
        name = class_name[len(NAMESPACE):]
        assert name in counts, class_name
        counts[name] += 1
    assert all(counts.values())
    (ROOT/'canonical-comparators.trx.gz').write_bytes(gzip.compress(raw, mtime=0))
    with (ROOT/'frozen-alignment.log').open('wb') as output:
        subprocess.run(['pwsh', '-NoProfile', '-File', str(ROOT/'frozen-alignment.ps1'), '-Root', str(ROOT)],
                       cwd=ROOT, stdout=output, stderr=subprocess.STDOUT, check=True)
    alignment = json.loads((ROOT/'frozen-canonical-alignment.json').read_text())
    assert alignment['status'] == 'passed' and alignment['production_sha256'] == PRODUCTION
    assert {(r['pair'], r['bars']) for r in alignment['rows']} == {(p, n) for p in auth['pairs'] for n in (1000, 10000)}
    assert len(alignment['rows']) == 20 and all(r['passed'] and r['verify_isolation'] for r in alignment['rows'])
    verify_runtime(ROOT/'canonical-runtime', auth['canonical_runtime'])
    verify_runtime(ROOT/'frozen-runtime', auth['frozen_runtime'])
    assert all(digest(r['path']) == r['sha256'] for r in auth['protected_links'])
    write('receipt.json', dict(status='passed', source=SOURCE, frozen_source=FROZEN, canonical_ci=CI,
        canonical_manifest_sha256=MANIFEST_SHA, production_sha256=PRODUCTION, canonical_test_sha256=TEST_DLL,
        frozen_benchmark_sha256=BENCH_DLL, classes=CLASSES, class_counts=counts, passed_cases=sum(counts.values()),
        pairs=auth['pairs'], alignment_cases=20, authentication_sha256=digest(ROOT/'authentication.json'),
        comparator_trx_sha256=hashlib.sha256(raw).hexdigest(), comparator_trx_gz_sha256=digest(ROOT/'canonical-comparators.trx.gz'),
        frozen_alignment_sha256=digest(ROOT/'frozen-canonical-alignment.json'),
        scripts={name: digest(ROOT/name) for name in ('preflight.py', 'frozen-alignment.ps1', 'runtime-dependencies.py')},
        fresh_gpu_execution=False, new_compilation=False, measurement=False, report_changed=False))
    print(json.dumps(counts), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=['authenticate', 'execute'])
    mode = parser.parse_args().mode
    authenticate() if mode == 'authenticate' else execute()
