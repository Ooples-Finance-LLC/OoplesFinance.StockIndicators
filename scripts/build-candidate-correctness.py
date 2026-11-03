"""Build immutable CPU-checked runtimes for subsequent actual local GPU checks."""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import subprocess
import sys

SDK = '10.0.403'
ROOTS = ('src/bin/Release/net10.0', 'tests/bin/Release/net10.0',
    'benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0',
    'benchmarks/OoplesFinance.StockIndicators.CompetitorTests/bin/Release/net10.0')
DLL = 'OoplesFinance.StockIndicators.dll'


def digest(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def gpu_module(repo):
    spec = importlib.util.spec_from_file_location('candidate_gpu', repo/'scripts/verify-competitor-local-gpu.py')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def validate_layout(manifest, read, source, fingerprint):
    assert manifest['schema'] == 1 and manifest['status'] == 'cpu-correctness-complete'
    assert manifest['source'] == source and manifest['source_sha256'] == fingerprint
    assert manifest['sdk'] == SDK
    files = manifest['runtime_files']
    for name, sha in files.items():
        path = PurePosixPath(name)
        assert not path.is_absolute() and '..' not in path.parts
        assert path.as_posix() == name and ':' not in name and '\\' not in name
        assert any(name.startswith(root+'/') for root in ROOTS)
        assert re.fullmatch('[a-f0-9]{64}', sha)
    production = [files[root+'/'+DLL] for root in ROOTS]
    assert len(set(production)) == 1
    for sha in set(files.values()):
        assert hashlib.sha256(read('blobs/'+sha)).hexdigest() == sha
    for name, sha in manifest['evidence'].items():
        assert re.fullmatch(r'evidence/[a-zA-Z0-9_.-]+', name)
        assert hashlib.sha256(read(name)).hexdigest() == sha
    return production[0]


def build(repo, out, source, unit, parity):
    repo, out = repo.resolve(), out.resolve()
    assert re.fullmatch('[a-f0-9]{40}', source)
    assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=repo,text=True).strip() == source
    assert not subprocess.check_output(['git','status','--porcelain'],cwd=repo)
    assert subprocess.check_output(['dotnet','--version'],cwd=repo,text=True).strip() == SDK
    for name in unit+parity:
        assert re.fullmatch('[a-zA-Z_][a-zA-Z0-9_.]*', name)
    assert unit and parity and not out.exists()
    (out/'evidence').mkdir(parents=True)
    (out/'blobs').mkdir()
    gpu = gpu_module(repo)
    fingerprint = gpu.source_fingerprint(repo)
    env = dict(os.environ,UseSharedCompilation='false',MSBUILDDISABLENODEREUSE='1')
    def run(name, args):
        with (out/'evidence'/name).open('wb') as log:
            subprocess.run(args,cwd=repo,env=env,stdout=log,stderr=subprocess.STDOUT,check=True,timeout=3600)
    run('unit-build.log',['dotnet','build','tests/OoplesFinance.StockIndicators.Tests.Unit.csproj',
        '-c','Release','-p:GeneratePackageOnBuild=false','-m:2'])
    run('comparator-build.log',['dotnet','build','benchmarks/OoplesFinance.StockIndicators.CompetitorTests',
        '-c','Release','-p:GeneratePackageOnBuild=false','-m:2'])
    sys.path.insert(0,str(repo/'scripts'))
    from hosted_timing.pipeline import verify_trx
    checks = {}
    for name, dll, classes in [('unit',ROOTS[1]+'/OoplesFinance.StockIndicators.Tests.Unit.dll',unit),
            ('parity',ROOTS[3]+'/OoplesFinance.StockIndicators.CompetitorTests.dll',parity)]:
        run(name+'-tests.log',['dotnet','vstest',dll,'/TestCaseFilter:'+'|'.join(
            'FullyQualifiedName~'+c for c in classes),'/Logger:trx;LogFileName='+name+'.trx',
            '/ResultsDirectory:'+str(out/'evidence')])
        checks[name] = verify_trx(out/'evidence'/(name+'.trx'),classes)
    assert gpu.source_fingerprint(repo) == fingerprint
    assert not subprocess.check_output(['git','status','--porcelain'],cwd=repo)
    files = {}
    for root in ROOTS:
        for path in (repo/root).rglob('*'):
            if not path.is_file(): continue
            sha = digest(path)
            files[path.relative_to(repo).as_posix()] = sha
            blob = out/'blobs'/sha
            if not blob.exists(): shutil.copyfile(path,blob)
    info = subprocess.check_output(['dotnet','--info'],cwd=repo)
    (out/'evidence/dotnet-info.txt').write_bytes(info)
    manifest = dict(schema=1,status='cpu-correctness-complete',source=source,source_sha256=fingerprint,
        sdk=SDK,runtime_files=files,checks=checks,classes=dict(unit=unit,parity=parity),
        evidence={p.relative_to(out).as_posix():digest(p) for p in (out/'evidence').iterdir() if p.is_file()},
        run_id=os.environ['GITHUB_RUN_ID'],run_attempt=os.environ['GITHUB_RUN_ATTEMPT'],
        workflow_head=os.environ['GITHUB_SHA'],gpu_status='pending actual local execution')
    validate_layout(manifest,lambda name:(out/name).read_bytes(),source,fingerprint)
    (out/'manifest.json').write_bytes((json.dumps(manifest,indent=2)+'\n').encode())
    print(json.dumps(dict(source=source,source_sha256=fingerprint,checks=checks)),flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--repo',type=Path,required=True)
    parser.add_argument('--out',type=Path,required=True)
    parser.add_argument('--source',required=True)
    parser.add_argument('--unit',required=True)
    parser.add_argument('--parity',required=True)
    args = parser.parse_args()
    build(args.repo,args.out,args.source,args.unit.split(','),args.parity.split(','))
