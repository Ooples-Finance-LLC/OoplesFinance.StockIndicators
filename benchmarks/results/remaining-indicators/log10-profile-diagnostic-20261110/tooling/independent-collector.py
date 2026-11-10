"""Collect authenticated diagnostic artifacts directly to the network archive."""
import hashlib,json,stat,subprocess,time,zipfile
from pathlib import Path,PurePosixPath,PureWindowsPath
R=Path(__file__).parent
CHECKPOINT=R/'continuation-latest-20261028.json'
NAME='log10-profile-diagnostic-20261110';RUN='42813611722'
REPO='Ooples-Finance-LLC/OoplesFinance.StockIndicators'
ROOT=Path('Z:/Codex-Archives/StockIndicators-Recovery-20261021/log10-profile-diagnostic-repair-20261110')
ROOT.mkdir(parents=True,exist_ok=True)

def api(path):
 for i in range(5):
  child=subprocess.run(['gh','api','repos/'+REPO+path],capture_output=True)
  if child.returncode==0:return json.loads(child.stdout)
  if i==4 or not any(x in child.stderr.decode(errors='replace') for x in ('HTTP 500','HTTP 502','HTTP 503','HTTP 504','TLS handshake timeout','connection reset')):raise RuntimeError(child.stderr.decode(errors='replace'))
  time.sleep(3)

def digest(path):
 h=hashlib.sha256()
 with path.open('rb') as source:
  for chunk in iter(lambda:source.read(1024*1024),b''):h.update(chunk)
 return h.hexdigest()

def load():return json.loads(CHECKPOINT.read_text())

def verify(profile,folder,q):
 raw_files=json.loads((folder/'files.json').read_text());files={}
 for name,sha in raw_files.items():
  path=PureWindowsPath(name);assert not path.is_absolute() and '..' not in path.parts and ':' not in name
  key=path.as_posix();assert key not in files,'Colliding portable paths';files[key]=sha
 actual={x.relative_to(folder).as_posix() for x in folder.rglob('*') if x.is_file()}
 assert actual==set(files)|{'files.json'}
 for rel,sha in files.items():
  path=(folder/rel).resolve();assert path.is_relative_to(folder.resolve()) and digest(path)==sha
 summary=json.loads((folder/'summary.json').read_text());origin=json.loads((folder/'provenance.json').read_text())
 assert summary['diagnostic_only'] is True and summary['qualification_eligible'] is False and summary['profile']==profile and summary['origin']==origin
 assert origin['tooling_head']==q['tooling_head'] and origin['host']['run_id']==RUN and origin['sources']==q['sources'] and origin['input_provenance_sha256']==q['input_provenance_sha256'] and origin['plan_sha256']==q['plan_sha256']
 for name,sha in origin['tooling_files'].items():
  raw=subprocess.check_output(['git','show',q['tooling_head']+':'+PureWindowsPath(name).as_posix()],cwd=q['tooling_checkout']);lf=raw.replace(b'\r\n',b'\n')
  assert sha in {hashlib.sha256(lf).hexdigest(),hashlib.sha256(lf.replace(b'\n',b'\r\n')).hexdigest()}
 assert [x['arm'] for x in summary['rounds']]==q['order'] and [x['round'] for x in summary['rounds']]==list(range(4))
 for row in summary['rounds']:
  round_path=folder/f"round{row['round']}-{row['arm']}"
  for samples in [row['quiet_samples'],json.loads((round_path/'trace/summary.json').read_text())['quiet_samples']]:assert len(samples)==12 and all(x<15 for x in samples)
  report=PureWindowsPath(row['report']).relative_to(PureWindowsPath(origin['raw_root'])).as_posix();assert digest(folder/report)==row['report_sha256'] and digest(round_path/'bdn.log')==row['log_sha256'] and digest(round_path/'trace/summary.json')==row['trace_summary_sha256']
  replay=json.loads((round_path/'trace/summary.json').read_text());label='old' if row['arm']=='old' else 'successor';assert replay['ready']['production_sha256']==q['production_sha256'][label] and replay['ready']['runtime'].startswith('.NET 10.')
  assert replay['replay']['completed'] and replay['replay']['invocations']==10000 and len(replay['replay']['chunks'])==10
  assert set(replay['instrumentation_only'])=={'DOTNET_JitDisasm','DOTNET_JitDisasmAssemblies','DOTNET_JitStdOutFile'}
 return dict(profile=profile,artifact_root=str(folder),files=len(files)+1,rounds=4,bdn_cases=sum(len(x['rows']) for x in summary['rounds']),host=origin['host'])

previous=None;verified={}
while True:
 q=load()['performance_continuation_20261103'][NAME]
 state=api('/actions/runs/'+RUN);assert state['head_sha']==q['tooling_head']
 jobs=api('/actions/runs/'+RUN+'/jobs?per_page=100')['jobs'];display=[(j['name'],j['status'],j['conclusion'],[s['name'] for s in j.get('steps',[]) if s['status']=='in_progress']) for j in jobs]
 if display!=previous:print(display,flush=True);previous=display
 artifacts=api('/actions/runs/'+RUN+'/artifacts')['artifacts']
 for artifact in artifacts:
  if artifact['name'] not in ('log10-profile-batch','log10-profile-builder'):continue
  profile=artifact['name'].removeprefix('log10-profile-')
  if profile in verified:continue
  assert not artifact['expired']
  archive=ROOT/(artifact['name']+'.zip')
  if not archive.exists():
   with archive.open('wb') as target:subprocess.run(['gh','api','repos/'+REPO+'/actions/artifacts/'+str(artifact['id'])+'/zip'],stdout=target,check=True)
  sha=digest(archive);assert artifact['digest']=='sha256:'+sha,'API archive digest mismatch'
  folder=ROOT/profile
  if not folder.exists():
   folder.mkdir()
   with zipfile.ZipFile(archive) as z:
    for entry in z.infolist():
     path=PurePosixPath(entry.filename);assert not path.is_absolute() and '..' not in path.parts and not stat.S_ISLNK(entry.external_attr>>16) and ':' not in entry.filename and '\\' not in entry.filename
    z.extractall(folder)
  # Failed-job artifacts stay intact; only complete artifacts get a verification receipt.
  if not (folder/'summary.json').exists():raise RuntimeError('Incomplete diagnostic artifact retained at '+str(folder))
  receipt=verify(profile,folder,q);receipt.update(artifact_id=artifact['id'],archive_sha256=sha);verified[profile]=receipt
  (ROOT/(profile+'-verified.json')).write_text(json.dumps(receipt,indent=2)+'\n');print('Verified diagnostic artifact',profile,receipt['bdn_cases'],'cases',flush=True)
 if state['status']=='completed':
  assert state['conclusion']=='success' and set(verified)=={'batch','builder'},'Diagnostic incomplete or failed; artifacts preserved'
  assert verified['batch']['host']['instance_nonce']!=verified['builder']['host']['instance_nonce']
  identities={job['name']:job for job in jobs}
  batch,builder=[identities['observe ('+profile+')'] for profile in ('batch','builder')]
  assert batch['runner_id']!=builder['runner_id'] and all(job['runner_name'].startswith('GitHub Actions ') for job in (batch,builder))
  assert max(job['started_at'] for job in (batch,builder))<min(job['completed_at'] for job in (batch,builder))
  identity_receipt=dict(kind='Distinct concurrent GitHub-hosted runner IDs and distinct instance nonces; template hostname is not unique',run_id=RUN,jobs=[{key:job[key] for key in ('id','name','runner_id','runner_name','started_at','completed_at','labels')} for job in (batch,builder)],hosts={profile:verified[profile]['host'] for profile in verified})
  (ROOT/'hosted-runner-identities.json').write_text(json.dumps(identity_receipt,indent=2)+'\n')
  d=load();q=d['performance_continuation_20261103'][NAME];q.update(status='Complete two-job diagnostic; artifact identities and every file hash verified; analysis pending',run_id=RUN,archive_root=str(ROOT),verified_artifacts=verified,diagnostic_complete=True)
  d['active_task']['status']=q['status'];CHECKPOINT.write_text(json.dumps(d,indent=2)+'\n');print('Complete authenticated20case/8trace diagnostic ready for analysis',flush=True);break
 time.sleep(30)
