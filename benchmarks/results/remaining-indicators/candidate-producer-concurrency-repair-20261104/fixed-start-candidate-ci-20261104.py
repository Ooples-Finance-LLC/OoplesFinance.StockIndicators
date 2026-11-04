import argparse,json,subprocess
from pathlib import Path
p=argparse.ArgumentParser()
for name in ('repo','name','unit','parity'):p.add_argument('--'+name,required=True)
p.add_argument('--expected-unit',type=int,required=True)
p.add_argument('--baseline',default='be18dbd164629d7fa2f71a6dd9b3d565155f7035')
p.add_argument('--producer-ref',default='perf/crow-soldier-ci-producer-20261104')
a=p.parse_args();r=Path(__file__).parent;repo=Path(a.repo)
source=subprocess.check_output(['git','rev-parse','HEAD'],cwd=repo,text=True).strip()
assert not subprocess.check_output(['git','status','--porcelain'],cwd=repo)
paths=subprocess.check_output(['git','diff','--name-only',a.baseline,source,'--','src','tests'],cwd=repo,text=True).splitlines()
assert paths
producer=subprocess.check_output(['git','ls-remote','origin','refs/heads/'+a.producer_ref],cwd=repo,text=True).splitlines()
assert len(producer)==1 and producer[0].split()[0]=='0857eefc242585a9f44bfa3c134ea0bd3eb1b30a'
payload=dict(ref=a.producer_ref,inputs=dict(suite='build-correctness-inputs',candidate_source=source,candidate_unit_classes=a.unit,candidate_parity_classes=a.parity))
dispatch=r/(a.name+'-ci-dispatch.json');dispatch.write_bytes((json.dumps(payload,indent=2)+'\n').encode())
subprocess.run(['gh','api','repos/Ooples-Finance-LLC/OoplesFinance.StockIndicators/actions/workflows/cpu-kernel-performance.yml/dispatches','--input',str(dispatch)],cwd=repo,check=True)
checkpoint=r/'continuation-latest-20261028.json';d=json.loads(checkpoint.read_text());q=d['performance_continuation_20261103']
assert a.name not in q
q[a.name]=dict(source=source,repo=str(repo),branch=subprocess.check_output(['git','branch','--show-current'],cwd=repo,text=True).strip(),
    status='Bounded source reviewed/committed/pushed; focused CI correctness dispatched',
    baseline=a.baseline,unit=a.unit,parity=a.parity,expected_unit=a.expected_unit,
    actual_gpu_required=100,paths=paths,out=str(r/a.name),build_tooling='0857eefc242585a9f44bfa3c134ea0bd3eb1b30a',build_ref=payload['ref'],
    integration_rule='Complete independent same-baseline timing; no pooling; compose only accepted candidates then fresh combined verification within approved780case156decisioncap.')
checkpoint.write_bytes((json.dumps(d,indent=2)+'\n').encode());print(source)
