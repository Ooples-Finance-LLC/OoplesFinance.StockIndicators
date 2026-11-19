import importlib.util,json,os,sys,subprocess,collections,re
from pathlib import Path
R=Path(__file__).parent;checkpoint=R/'continuation-latest-20261028.json';d=json.loads(checkpoint.read_text());q=d['performance_continuation_20261103']['log10-profile-diagnostic-20261110'];assert q['diagnostic_complete']
repo=Path(q['tooling_checkout']);root=Path(q['archive_root']);os.environ['HOSTED_TIMING_PLAN']='benchmarks/plans/hosted-log-parallel-minimum-20261109.json';sys.path.insert(0,str(repo/'scripts'))
s=importlib.util.spec_from_file_location('diagnostic',repo/'scripts/run-log10-profile-diagnostic.py');m=importlib.util.module_from_spec(s);s.loader.exec_module(m);assert m.validate_plan()['diagnostic_only']
result=dict(diagnostic_only=True,qualification_eligible=False,run_id=q['run_id'],tooling_head=q['tooling_head'],plan_sha256=q['plan_sha256'],input_provenance_sha256=q['input_provenance_sha256'],sources=q['sources'],profiles={},limitations=q['limitations'],independent_checks=dict(raw_bdn_reparsed=True,all_eight_speedscope_summaries_recomputed=True,original_rejected_candidate_excluded=True))
for profile in ('batch','builder'):
 folder=root/profile;summary=json.loads((folder/'summary.json').read_text());rounds=[]
 for row in summary['rounds']:
  base=folder/f"round{row['round']}-{row['arm']}";report=m.find_report(base/'bdn');raw=json.loads(report.read_text());rows=m.validate_cases(profile,[raw],(base/'bdn.log').read_text(errors='replace'));assert rows==row['rows']
  trace=json.loads((base/'trace/summary.json').read_text());fresh=m.summarize_speedscope(base/'trace/profile.speedscope.json');assert all(trace[k]==fresh[k] for k in fresh)
  jit=(base/'trace/jit.log').read_text(errors='replace');headers=re.findall(r'^; Assembly listing for method .*$',jit,re.M)
  rounds.append(dict(round=row['round'],arm=row['arm'],rows=rows,host_environment=raw['HostEnvironmentInfo'],trace_ready=trace['ready'],attribution=fresh,replay=trace['replay'],jit_observations=dict(headers=headers,math_log10_call_sites=jit.count('System.Math:Log10'),bytes=(base/'trace/jit.log').stat().st_size),raw_memory=[dict(method=c['Method'],memory=c['Memory']) for c in raw['Benchmarks']]))
 methods={}
 for method in [x['method'] for x in rounds[0]['rows']]:
  values={arm:[next(x for x in row['rows'] if x['method']==method) for row in rounds if row['arm']==arm] for arm in ('old','new')}
  old=sum(x['mean_ns'] for x in values['old'])/2;new=sum(x['mean_ns'] for x in values['new'])/2
  methods[method]=dict(old_rounds_us=[x['mean_ns']/1000 for x in values['old']],new_rounds_us=[x['mean_ns']/1000 for x in values['new']],descriptive_change_percent=(new/old-1)*100,old_allocated_bytes=[x['allocated_bytes'] for x in values['old']],new_allocated_bytes=[x['allocated_bytes'] for x in values['new']])
 result['profiles'][profile]=dict(host=summary['origin']['host'],methods=methods,rounds=rounds)
 print(profile,'HostEnvironmentInfo',json.dumps(rounds[0]['host_environment']),flush=True)
 for method,values in methods.items():print(method,json.dumps(values),flush=True)
 for row in rounds:
  a=row['attribution'];print('trace',profile,row['round'],row['arm'],'DOP',row['trace_ready']['maximum_degree_of_parallelism'],'attributed_ms',round(a['attributed_milliseconds'],2),'top',[(round(100*v/a['attributed_milliseconds'],1),k) for k,v in list(a['exclusive'].items())[:7]],flush=True)
(root/'independent-analysis.json').write_text(json.dumps(result,indent=2)+'\n');(R/'log10-profile-diagnostic-analysis-20261110.json').write_text(json.dumps(result,indent=2)+'\n')
print('All20rawBDNcases and8sampled-stack summaries independently revalidated',flush=True)
