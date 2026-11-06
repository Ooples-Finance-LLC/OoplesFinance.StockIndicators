"""Bind measured tensor dependencies to the runtime used by focused CPU checks."""
import hashlib
import json
from pathlib import Path


def tensor_closure(runtime, deps_file):
    runtime = Path(runtime).resolve()
    data = json.loads((runtime / deps_file).read_text(encoding='utf-8'))
    target = data['targets'][data['runtimeTarget']['name']]
    by_name = {key.split('/', 1)[0]: key for key in target}
    assert len(by_name) == len(target), 'Ambiguous runtime dependency versions'
    pending, packages, files = ['AiDotNet.Tensors'], {}, {}
    while pending:
        name = pending.pop()
        if name in packages:
            continue
        key = by_name[name]
        item = target[key]
        packages[name] = key.split('/', 1)[1]
        pending.extend(item.get('dependencies', {}))
        for group in ('runtime', 'native', 'runtimeTargets'):
            for asset in item.get(group, {}):
                relative = Path(asset)
                assert not relative.is_absolute() and '..' not in relative.parts and ':' not in asset
                choices = [runtime / relative, runtime / relative.name]
                existing = [path for path in choices if path.is_file()]
                assert existing, f'Missing tested dependency asset: {asset}'
                path = existing[0].resolve()
                assert path.is_relative_to(runtime)
                label = path.relative_to(runtime).as_posix()
                files[label] = hashlib.sha256(path.read_bytes()).hexdigest()
    return dict(packages=packages, files=files)


def verify_alignment(common, tested, required_version):
    assert common['packages']['AiDotNet.Tensors'] == required_version
    assert tested == common, 'Timing tensor dependency closure differs from focused CPU correctness'


def verify_input_alignment(folder, plan, proof):
    if not plan.get('runtime_dependency_alignment'):
        return
    common = tensor_closure(Path(folder)/'common', 'OoplesFinance.StockIndicators.CompetitorBenchmarks.deps.json')
    assert set(proof['tested_runtime_dependencies']) == {'old', 'successor', 'variants'}
    for label in ('old', 'successor', 'variants'):
        verify_alignment(common, proof['tested_runtime_dependencies'][label], plan['runtime_dependency_alignment']['tensor_version'])
