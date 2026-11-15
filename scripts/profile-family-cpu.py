"""One-runner, immutable-binary CPU screen; never source/timing qualification."""
import argparse,ctypes,hashlib,json,subprocess,urllib.request
from datetime import datetime
from pathlib import Path,PurePosixPath

MANIFEST='00aa40384d25287226a6c0165a5e55cdb6b3156327f2b919da05b194cd566733'
PRODUCTION='fa986f1b10d309ff68a6d52427cc4f26ca1881c63f63c7448f2543edc087a9d1'
FINGERPRINT='2cfc0cb9ec476ec6232dbbaf1e545f6d313758cfb54d4f3ca6fc7cc08361ed83'
PERFVIEW='270e2ba419b0b2d93c4b15b0851f257edf655c92fe0ad95add3d0b5f6d6aad48'
PAIRS=['TaLib.Functions.Add','TaLib.Functions.Floor','TaLib.Functions.Sin',
       'TaLib.Candles.MatchingLow','TaLib.Candles.OnNeck','TaLib.Candles.TasukiGap']
def digest(path):
    with path.open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
def valid_log(path):
    text=path.read_text(errors='replace');assert ' SUCCESS:' in text and '[Error:' not in text,path
def validate_probe(data,pair,role,seconds=10):
    assert data['production_sha256']==PRODUCTION and data['pair']==pair and data['role']==role
    assert data['bars']==10000 and data['profile_seconds']==seconds and data['iterations']>0
    assert isinstance(data['process_id'],int) and data['process_id']>0
    assert datetime.fromisoformat(data['profile_start_utc'])<datetime.fromisoformat(data['profile_end_utc'])
def authenticate(inputs,out):
    path=inputs/'manifest.json';assert digest(path)==MANIFEST
    manifest=json.loads(path.read_text())
    assert manifest['source']=='719c8c86c9e7f55a2be04484086dd6941c01e2fd'
    assert manifest['source_sha256']==FINGERPRINT and manifest['run_id']=='43671778311'
    assert manifest['sdk']=='10.0.403' and manifest['status']=='cpu-correctness-complete'
    for sha in set(manifest['runtime_files'].values()):assert digest(inputs/'blobs'/sha)==sha
    for name,sha in manifest['evidence'].items():assert digest(inputs/name)==sha
    runtime=out/'runtime';runtime.mkdir()
    prefix='benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0/'
    for name,sha in manifest['runtime_files'].items():
        if not name.startswith(prefix):continue
        relative=PurePosixPath(name[len(prefix):]);assert not relative.is_absolute() and '..' not in relative.parts
        target=runtime/relative;target.parent.mkdir(parents=True,exist_ok=True)
        target.write_bytes((inputs/'blobs'/sha).read_bytes())
    assert digest(runtime/'OoplesFinance.StockIndicators.dll')==PRODUCTION
    (out/'authenticated-manifest.json').write_bytes(path.read_bytes())
    return runtime
def main():
    p=argparse.ArgumentParser();p.add_argument('--inputs',type=Path,required=True);p.add_argument('--out',type=Path,required=True);p.add_argument('--authenticate-only',action='store_true');a=p.parse_args()
    a.out=a.out.resolve();a.out.mkdir(parents=True,exist_ok=False)
    runtime=authenticate(a.inputs.resolve(),a.out)
    if a.authenticate_only:return
    (a.out/'dotnet-info.log').write_bytes(subprocess.check_output(['dotnet','--info']))
    assert ctypes.windll.shell32.IsUserAnAdmin(),'Kernel CPU screen requires elevated runner'
    tool=a.out/'PerfView.exe';urllib.request.urlretrieve('https://github.com/microsoft/perfview/releases/download/v4.0.0/PerfView.exe',tool);assert digest(tool)==PERFVIEW
    probe=Path(__file__).with_name('probe-family-cpu.ps1').resolve();rows=[]
    for pair in PAIRS:
        for role in ['Pair','Native']:
            folder=a.out/(pair+'-'+role);folder.mkdir()
            trace=folder/'sample.etl';log=folder/'collection.log';record=folder/'probe.json'
            cmd=[str(tool),'/AcceptEula','/NoGui','/NoView','/DataFile:'+str(trace),'/LogFile:'+str(log),
                 '/KernelEvents:Process,Thread,ImageLoad,Profile','/BufferSizeMB:64','/CircularMB:128','/Zip:true','/MaxCollectSec:40',
                 'run','pwsh','-NoProfile','-File',str(probe),'-Runtime',str(runtime),'-Production',str(runtime/'OoplesFinance.StockIndicators.dll'),
                 '-Expected',PRODUCTION,'-Pair',pair,'-Role',role,'-Bars','10000','-Workers','8','-Warmups','1000','-ProfileSeconds','10','-Out',str(record)]
            with (folder/'launcher.log').open('wb') as output:subprocess.run(cmd,stdout=output,stderr=subprocess.STDOUT,check=True,timeout=110)
            valid_log(log);data=json.loads(record.read_text());validate_probe(data,pair,role)
            archive=trace.with_suffix('.etl.zip');assert archive.is_file()
            export=folder/'export.log'
            subprocess.run([str(tool),'/AcceptEula','/NoGui','/LogFile:'+str(export),'userCommand','SaveCPUStacksAsCsv',str(archive),'pwsh','1000000000'],check=True,timeout=110)
            valid_log(export);csv=folder/'sample.perfView.csv';assert csv.is_file() and '('+str(data['process_id'])+')' in csv.read_text(errors='replace')
            rows.append(dict(pair=pair,role=role,process_id=data['process_id'],window_utc=[data['profile_start_utc'],data['profile_end_utc']],trace_sha256=digest(archive),csv_sha256=digest(csv)))
            (a.out/'progress.json').write_text(json.dumps(rows,indent=2)+'\n')
            print(pair,role,'CPU trace retained',flush=True)
    assert len(rows)==12 and digest(runtime/'OoplesFinance.StockIndicators.dll')==PRODUCTION
    (a.out/'receipt.json').write_text(json.dumps(dict(diagnostic_only=True,qualification=False,source_import=False,production_sha256=PRODUCTION,manifest_sha256=MANIFEST,source_fingerprint=FINGERPRINT,perfview_sha256=PERFVIEW,profiles=rows,
        limits='CPU screen only, not latency measurement or native-win evidence. CSV includes startup; use recorded UTC windows with raw ETL. Unresolved symbols must be resolved before attribution. Existing correctness/GPU source reused; no source or timing-policy changes.'),indent=2)+'\n')
if __name__=='__main__':main()
