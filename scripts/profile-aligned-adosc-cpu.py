"""Optional post-measurement kernel CPU diagnostic; never a timing gate/result."""
import argparse
import ctypes
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import urllib.request

from hosted_timing import pipeline as p

URL = 'https://github.com/microsoft/perfview/releases/download/v4.0.0/PerfView.exe'
SHA = '270e2ba419b0b2d93c4b15b0851f257edf655c92fe0ad95add3d0b5f6d6aad48'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def successful_log(path):
    text = path.read_text(errors='replace')
    assert '[Error:' not in text and ' SUCCESS:' in text, 'PerfView log does not report success'


def profile(inputs, out):
    proof = p.verify_inputs(inputs)
    expected = proof['production_sha256']['successor']
    assert ctypes.windll.shell32.IsUserAnAdmin(), 'Kernel CPU sampling requires an elevated runner'
    tool = out/'PerfView.exe'
    urllib.request.urlretrieve(URL,tool)
    assert digest(tool) == SHA, 'PerfView release hash mismatch'
    runtime = out/'runtime'; shutil.copytree(inputs/'common',runtime)
    production = runtime/p.DLL; shutil.copyfile(inputs/'production/successor.dll',production)
    assert digest(production) == expected
    trace = out/'candidate.etl'; collect_log = out/'collection.log'; probe = out/'probe.json'
    command = [str(tool),'/AcceptEula','/NoGui','/NoView','/DataFile:'+str(trace),'/LogFile:'+str(collect_log),
               '/KernelEvents:Process,Thread,ImageLoad,Profile','/BufferSizeMB:64','/CircularMB:128','/Zip:true','/MaxCollectSec:45',
               'run','pwsh','-NoProfile','-File',str(p.REPO/'scripts/probe-aligned-adosc-cpu.ps1'),
               '-Runtime',str(runtime),'-Production',str(production),'-Expected',expected,'-Pair','TaLib.Functions.AdOsc',
               '-Workers','8','-Warmups','1000','-ProfileSeconds','15','-Out',str(probe)]
    with (out/'launcher.log').open('wb') as log:
        subprocess.run(command,stdout=log,stderr=subprocess.STDOUT,check=True,timeout=120)
    successful_log(collect_log)
    data = json.loads(probe.read_text()); assert data['production_sha256'] == expected
    assert data['pair'] == 'TaLib.Functions.AdOsc' and data['bars'] == 10000 and data['profile_seconds'] == 15
    pid = data['process_id']; assert isinstance(pid,int) and pid > 0
    assert data['profile_start_utc'] < data['profile_end_utc']
    archive = trace.with_suffix('.etl.zip'); assert archive.is_file()
    export = out/'cpu-export.log'
    # The probe is the last-started pwsh in this dedicated trace. Confirm its
    # actual PID in the exported process frame before making a CPU attribution.
    subprocess.run([str(tool),'/AcceptEula','/NoGui','/LogFile:'+str(export),'userCommand','SaveCPUStacksAsCsv',
                    str(archive),'pwsh','1000000000'],check=True,timeout=120)
    successful_log(export)
    candidates = list(out.glob('*.perfView.csv')); assert len(candidates) == 1
    csv = candidates[0]; text = csv.read_text(errors='replace')
    assert 'OoplesFinance.StockIndicators' in text and 'pwsh' in text and '('+str(pid)+')' in text, 'CPU process filter needs manual confirmation'
    assert digest(production) == expected
    return dict(available=True,diagnostic_only=True,performance_claim=False,qualification=False,
                production_sha256=expected,process_id=pid,perfview_sha256=SHA,trace_sha256=digest(archive),
                profile_window_utc=[data['profile_start_utc'],data['profile_end_utc']],
                csv_sha256=digest(csv),method='PerfView4.0.0 kernel ETW Profile CPUStacks, filtered to the probe process',
                limits='Collected after all20timingcases in a separate process. CSV includes startup/warmups; raw ETL and recorded UTC window permit hot-window filtering. No thread-time span attribution or timing-policy change.')


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--inputs',type=Path,required=True);parser.add_argument('--out',type=Path,required=True)
    args=parser.parse_args();args.inputs=args.inputs.resolve();args.out=args.out.resolve();args.out.mkdir(parents=True)
    try:
        result=profile(args.inputs,args.out)
    except Exception as error:
        # Profiling was never a qualification gate. Retain an explicit failure;
        # complete, unchanged timing evidence must not be relabeled or discarded.
        result=dict(available=False,diagnostic_only=True,performance_claim=False,qualification=False,error=str(error))
    p.write(args.out/'receipt.json',result)
    print(json.dumps(result),flush=True)


if __name__ == '__main__':main()
