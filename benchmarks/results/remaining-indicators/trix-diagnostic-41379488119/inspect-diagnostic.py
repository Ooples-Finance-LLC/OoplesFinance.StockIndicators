import hashlib
import json
import os
from pathlib import Path
import re
import sys

os.environ['HOSTED_TIMING_PLAN'] = 'benchmarks/plans/hosted-repair-20261101.json'
sys.path.insert(0, r'C:\Users\cheat\source\repos\Ooples-Finance-LLC\si-hosted-20261101\scripts')
from hosted_timing.pipeline import PLAN, read, digest, phase_sources, write
from hosted_timing.verify import archived, check_log, sample
from hosted_timing.checkpoints import case_key

run, profile = sys.argv[1:]
root = Path(__file__).parent/'hosted-runs'/run/('diagnostic-'+profile)
proof, state = read(root/'provenance.json'), read(root/'status.json')
assert proof['diagnostic'] == profile and proof['host']['run_id'] == run
assert proof['plan_sha256'] == digest(PLAN)
assert proof['sources'] == phase_sources(read(PLAN), 'successor')
assert state['status'] == 'raw-measurements-complete'
assert state['order'] == ['old', 'new', 'new', 'old'] and len(state['rounds']) == 4
prepared = read(root.parents[1]/'41367778664/hosted-inputs/provenance.json')
assert proof['input_provenance'] == prepared
assert proof['input_provenance_sha256'] == digest(root.parents[1]/'41367778664/hosted-inputs/provenance.json')
rows, code, logs = [], [], []
for record in state['rounds']:
    index, arm = record['index'], record['arm']
    assert arm == state['order'][index] and record['cases'] == 4
    cp, = record['operation_checkpoints']
    log, report = (archived(root, proof['raw_root'], cp[k]) for k in ('log', 'report'))
    assert digest(log) == cp['log_sha256'] and digest(report) == cp['sha256']
    assert len(cp['quiet_cpu_samples']) == 12 and all(0 <= v <= 15 for v in cp['quiet_cpu_samples'])
    assert cp['quiet_build_free_samples'] == 12 and cp['build_monitor_observations'] > 0
    assert cp['execution_context'] == dict(host_nonce=proof['host']['instance_nonce'],
        production_sha256=prepared['production_sha256']['old' if arm == 'old' else 'successor'], phase='successor', shard=6)
    text = log.read_text(encoding='utf-8-sig')
    # JIT writes can interleave with console lines. BDN's own log retains the
    # complete iteration stream; keep its digest alongside the raw console hash.
    bdn_log, = report.parent.parent.glob('*.log')
    check_log(bdn_log.read_text(encoding='utf-8-sig'), 4)
    logs.append(dict(round=index, console_sha256=digest(log), bdn_sha256=digest(bdn_log)))
    cases = read(report)['Benchmarks']
    keys = [case_key(c) for c in cases]
    assert set(keys) == {('Skender.GetTrix', n, m) for n in (1000, 10000) for m in ('BuilderFull', 'BuilderLatestOnly')}
    rows.extend(dict(round=index, arm=arm, bars=key[1], method=key[2], **sample(c)) for key, c in zip(keys, cases))
    blocks = re.findall(r'; Assembly listing for method (.*?; Total bytes of code \d+)', text, re.S)
    assert blocks, 'JIT output is required for this diagnostic'
    for block in blocks:
        header = block.splitlines()[0]
        normalized = re.sub(r'0x[0-9A-Fa-f]{8,}', '<address>', block)
        normalized = re.sub(r'G_M\d+_', 'G_M_', normalized)
        code.append(dict(round=index, method=header, bytes=int(re.search(r'; Total bytes of code (\d+)', block)[1]),
            sha256=hashlib.sha256(normalized.encode()).hexdigest()))
result = dict(run=run, profile=profile, integrity_verified=True, diagnostic_only=True, rows=rows, code=code, logs=logs)
write(root.parent/('inspected-'+profile+'.json'), result)
print(json.dumps(dict(profile=profile, integrity_verified=True, rows=rows,
    code_blocks=len(code), triple_blocks=[c for c in code if 'TripleRateState' in c['method']]), indent=2))
