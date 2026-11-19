"""Run each frozen shard once, serially, with a120-minute process-tree deadline."""
import os
import subprocess
import sys
from pathlib import Path
from hosted_timing import pipeline as p
from hosted_timing import kama_full_qualification as q

def main():
    plan=p.read(p.PLAN);q.validate_scope(plan)
    assert os.name=='nt' and os.environ['GITHUB_REF']=='refs/heads/perf/kama-full-qualification-20261119'
    for shard in range(3):
        output=Path('reconciled-qualification/evidence')/('shard'+str(shard))
        assert not output.exists(),'No unchanged shard retry'
        command=[sys.executable,'scripts/run-hosted-timing.py','measure','--inputs','reconciled-inputs',
            '--out',str(output),'--phase','targeted','--shard',str(shard)]
        process=subprocess.Popen(command,creationflags=subprocess.CREATE_NEW_PROCESS_GROUP)
        try:
            code=process.wait(timeout=7200)
        except subprocess.TimeoutExpired:
            termination=None
            try:
                if process.poll() is None:
                    kill=subprocess.run(['taskkill','/PID',str(process.pid),'/T','/F'],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=15)
                    termination=dict(exit_code=kill.returncode,log=kill.stdout.decode(errors='replace'))
                process.wait(timeout=15)
            finally:
                output.mkdir(parents=True,exist_ok=True)
                p.write(output/'outer-timeout.json',dict(shard=shard,deadline_seconds=7200,
                    spawned_pid=process.pid,termination=termination,automatic_retry=False,other_processes_targeted=False))
            raise RuntimeError('First shard deadline exceeded; process tree stopped; no retry')
        assert code==0,'First shard failure preserved; no retry'

if __name__=='__main__':main()
