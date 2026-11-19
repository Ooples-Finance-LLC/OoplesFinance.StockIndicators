"""Standalone reviewed KAMA input bridge, using authenticated existing binaries only.

This is separate from hosted preparation: it does not build, profile, warm up,
measure, qualify, change either readiness flag, or launch a hosted workflow.
Run the explicit execution option only after root has reviewed the helper.
"""
import argparse,json,os
from pathlib import Path
from hosted_timing import kama_fused_prefix_roi as q

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--old-candidate',required=True)
    parser.add_argument('--candidate',required=True)
    parser.add_argument('--out',required=True)
    parser.add_argument('--execute-reviewed-preflight',action='store_true')
    args=parser.parse_args()
    assert args.execute_reviewed_preflight,'HELD: root helper review before the one local preflight'
    plan=q.p.read(q.p.PLAN);q.validate_scope(plan,require_ready=False)
    out=Path(args.out).resolve();old_path=Path(args.old_candidate).resolve();new_path=Path(args.candidate).resolve()
    assert not out.exists() and not out.is_relative_to(old_path) and not out.is_relative_to(new_path)
    old=q.checked_inputs(old_path,plan['old_candidate']);new=q.checked_inputs(new_path,plan['candidate'])
    assert old['closure']==new['closure']
    alias=q.harness_role_alias(plan,old,new)
    out.mkdir(parents=True);(out/'common').mkdir();(out/'production').mkdir();(out/'evidence').mkdir()
    prefix=q.p.BENCH+'/bin/Release/net10.0/'
    for name,pin in old['manifest']['runtime_files'].items():
        if name.startswith(prefix):
            target=out/'common'/name[len(prefix):];target.parent.mkdir(parents=True,exist_ok=True)
            # Immutable common input hardlinks are copied before replacing the core.
            os.link(old_path/'blobs'/pin,target)
    (out/'production/successor.dll').write_bytes(new['read']('blobs/'+new['production']))
    proof=q.common_harness_bridge(out,plan)
    q.p.write(out/'local-preflight-provenance.json',dict(status='local-common-harness-bridge-only',
        plan_sha256=q.p.digest(q.p.PLAN),source=q.NEW,source_fingerprint=plan['candidate']['source_fingerprint'],
        actual_candidate_harness_sha256=plan['candidate']['actual_harness_sha256'],common_harness_sha256=plan['harness_sha256'],
        production_sha256=new['production'],harness_role_alias=alias,common_harness_bridge=proof,
        builds=0,profiles=0,warmups=0,timings=0,hosted_launches=0,qualification=False,closure_credit=0))
    print(json.dumps(proof,indent=2))

if __name__=='__main__':main()
