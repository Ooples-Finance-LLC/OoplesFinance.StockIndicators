"""Stage hosted successor rows with per-comparison runner provenance."""
from collections import defaultdict
from pathlib import Path
import copy
import datetime
import hashlib
import html
import json
import re
import subprocess
import sys

from .pipeline import PLAN, read, digest, phase_tasks
from .checkpoints import case_key
from .verify import archived, collect


def stage(live, raw, repo, out):
    live, raw, repo, out = (Path(p).resolve() for p in (live, raw, repo, out))
    summary = collect(raw, 'successor')
    assert summary['accepted'], 'Only the complete accepted successor can replace report rows'
    source = summary['sources']['new']
    head = subprocess.check_output(['git','rev-parse','HEAD'],cwd=repo,text=True).strip()
    subprocess.run(['git','diff','--exit-code',source,head,'--','src','tests'],cwd=repo,check=True)
    original = live.read_bytes()
    document = original.decode('utf-8')
    def embedded(name):
        return json.loads(re.search(r'<script[^>]*id="'+name+r'"[^>]*>(.*?)</script>',document,re.S)[1])
    def replace(name,value):
        nonlocal document
        document,n = re.subn(r'(<script[^>]*id="'+name+r'"[^>]*>).*?(</script>)',
            lambda m:m[1]+json.dumps(value,ensure_ascii=True).replace('<','\\u003c')+m[2],document,flags=re.S)
        assert n == 1
    data, builder, status = (embedded(k) for k in ('dataset','current-competitor-data','rollout-status-data'))
    before = copy.deepcopy(data)
    oldcounts = status['measurement_totals'].copy()
    oldhead = status['published_head']
    old_builder_count = builder['full_comparisons']
    old_builder_pairs = len({c['pair'] for receipt in builder['receipts'] for c in receipt['comparisons']})
    baseline = {}
    builder_receipts = []
    run_ids = set()
    for root in sorted(p.parent for p in raw.glob('*/provenance.json')):
        proof, state = read(root/'provenance.json'),read(root/'status.json')
        host, shard = proof['host'],proof['shard']
        run_ids.add(host['run_id'])
        url = f'https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/actions/runs/{host["run_id"]}'
        provenance = dict(measurement_location='GitHub-hosted Windows',measurement_machine=host['machine'],
            measurement_run_id=host['run_id'],measurement_run_attempt=host['run_attempt'],measurement_shard=shard,
            measurement_host_nonce=host['instance_nonce'],measurement_runner_image=host['runner_image'])
        tasks = {t['fixture']:t for t in phase_tasks(read(PLAN),'successor',shard)}
        for record in state['rounds']:
            if record['index'] != 2: continue
            fixture=record['fixture']; task=tasks[fixture]
            mapping={op['raw']:op['pair'] for op in task['operations']}
            implicit=next(iter(mapping)) if task.get('implicit_pair') else None
            path=archived(root,proof['raw_root'],record['report'])
            report=read(path)
            report_file=path.relative_to(raw).as_posix()
            if fixture == 'LibraryPairBenchmarks':
                for case in report['Benchmarks']:
                    pair,bars,method=case_key(case,implicit)
                    key=mapping[pair],bars,method
                    assert key not in baseline
                    baseline[key]=(case,report['HostEnvironmentInfo'],report_file,record['sha256'],provenance,url)
                continue
            cases={(r['pair'],r['bars'],r['method']):r for r in summary['final_candidate_rows'][fixture]
                   if r['provenance']['shard']==shard}
            pairs={key[0] for key in cases}
            assert set(cases)=={(p,n,m) for p in pairs for n in (1000,10000)
                                for m in ('BuilderFull','BuilderLatestOnly','NativeSingle','NativeParallel')}
            def stats(row):
                return {key:row[key] for key in ('mean_ns','lower_ns','upper_ns','samples','allocated_bytes')}
            comparisons=[]
            for pair in sorted(pairs):
                for bars in (1000,10000):
                    native={m:stats(cases[pair,bars,m]) for m in ('NativeSingle','NativeParallel')}
                    for mode in ('BuilderFull','BuilderLatestOnly'):
                        ours=stats(cases[pair,bars,mode])
                        verdict=('faster' if ours['upper_ns']<min(n['lower_ns'] for n in native.values()) else
                                 'slower' if ours['lower_ns']>min(n['upper_ns'] for n in native.values()) else 'inconclusive')
                        comparisons.append(dict(pair=pair,bars=bars,mode=mode,ooples=ours,native=native,
                            fastest_native=min(native,key=lambda m:native[m]['mean_ns']),verdict=verdict,**provenance))
            builder_receipts.append(dict(fixture=fixture,source_sha=source,run_id=host['run_id'],measurement_url=url,
                round=2,report_file=report_file,report_sha256=record['sha256'],complete=True,
                selection='Chronologically final candidate round.',comparisons=comparisons,**provenance))
    assert len(run_ids)==1
    run=next(iter(run_ids))
    url=f'https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/actions/runs/{run}'
    pairs={key[0] for key in baseline}
    overrides=[]
    for row in data['rows']:
        if row['pair'] not in pairs: continue
        for method,arm in (('Ooples','ooples'),('Competitor','competitor')):
            case,host,file,sha,provenance,link=baseline[row['pair'],row['bars'],method]
            stats=case['Statistics']
            row.update({arm+'_mean_ns':stats['Mean'],arm+'_stddev_ns':stats['StandardDeviation'],
                        arm+'_samples':stats['N'],arm+'_allocated_bytes':case['Memory']['BytesAllocatedPerOperation']})
            row['raw'][method]=dict(samples=stats['OriginalValues'],host=host,report=file,confidence_interval=stats['ConfidenceInterval'])
        row.update(competitor_over_ooples=row['competitor_mean_ns']/row['ooples_mean_ns'],source_sha=source,
                   measurement_run_url=link,**provenance)
        if row['bars']==1000:
            overrides.append(dict(pair=row['pair'],sourceSha=source,runUrl=link,reportSha256=sha,
                round=2,selection='Chronologically final candidate round.',measurementLocation='GitHub-hosted Windows',**provenance))
    unchanged=sum(a==b for a,b in zip(before['rows'],data['rows'],strict=True))
    assert unchanged==len(data['rows'])-2*len(pairs)
    data['evidence'].setdefault('baseSourceSha',data['evidence']['sourceSha'])
    data['evidence']['sourceSha']=None
    data['evidence']['rowOverrides']=[r for r in data['evidence'].get('rowOverrides',[]) if r['pair'] not in pairs]+overrides
    replace('dataset',data)
    builder_pairs={c['pair'] for r in builder_receipts for c in r['comparisons']}
    replaced=0
    for receipt in builder['receipts']:
        keep=[c for c in receipt['comparisons'] if c['pair'] not in builder_pairs]
        replaced+=len(receipt['comparisons'])-len(keep)
        receipt['comparisons']=keep
    builder['receipts']=[r for r in builder['receipts'] if r['comparisons']]+builder_receipts
    added=4*len(builder_pairs)-replaced
    rows=[dict(**c,run_id=str(r['run_id']),source_sha=r['source_sha'],measurement_url=r.get('measurement_url',
          'https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/actions/runs/'+str(r['run_id'])))
          for r in builder['receipts'] for c in r['comparisons']]
    assert len(rows)==old_builder_count+added
    assert len({(r['pair'],r['bars'],r['mode']) for r in rows})==len(rows)
    counts={v:sum(r['verdict']==v for r in rows) for v in ('faster','slower','inconclusive')}
    builder.update(full_comparisons=len(rows),verdicts=counts)
    builder['status'].update(commit=source,updated_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),run_url=url)
    builder['status']['completed']=sorted(set(builder['status']['completed'])|{r['fixture'] for r in builder_receipts})
    replace('current-competitor-data',builder)
    status.update(published_head=head,inventory_commit=head,row_overrides=data['evidence']['rowOverrides'],
        inventory_sha256=digest(repo/'benchmarks/results/remaining-indicators/inventory.json'),
        updated_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    status['measurement_totals'].update(counts,measured=len(rows),pending=oldcounts['expected']-len(rows))
    status['builder_measurement_sources'].append(dict(source_sha=source,run_url=url,comparisons=4*len(builder_pairs),
        replaces_pairs=sorted(builder_pairs),round=2,measurement_location='GitHub-hosted Windows; per-row runner provenance'))
    replace('rollout-status-data',status)
    groups=defaultdict(int)
    for row in data['rows']: groups[row['source_sha'],row['measurement_run_url']]+=1
    caption='Measured builds: '+', '.join(f'{sha[:8]} ({n} comparisons)' for (sha,_),n in groups.items())+'.'
    disclosure=' Each row identifies its measured source and run. Results combine earlier measurements and GitHub-hosted Windows measurements; absolute timings are compared only within the same runner.'
    document=re.sub(r'Measured builds: [^<]*?\.',lambda _:caption,document)
    document=re.sub(r'(<section id="explorer"[^>]*>.*?<p class="sub">).*?(</p>)',lambda m:m[1]+caption+disclosure+m[2],document,count=1,flags=re.S)
    oldwins=sum(r['competitor_over_ooples']>1 for r in before['rows'] if r['bars']==10000)
    wins=sum(r['competitor_over_ooples']>1 for r in data['rows'] if r['bars']==10000)
    document=document.replace(f'{oldwins} / 401 APIs at 10,000 bars',f'{wins} / 401 APIs at 10,000 bars').replace('published <code>'+oldhead[:8]+'</code>','published <code>'+head[:8]+'</code>')
    refreshed=sum(r['source_sha']!=data['evidence']['baseSourceSha'] for r in data['rows'])
    intro=f"The table contains {len(data['rows'])} measured comparisons across {len({r['pair'] for r in data['rows']})} API pairs. {refreshed} comparison rows use separately qualified updates."+disclosure
    document=re.sub(r'(<section id="rollout-status".*?<p class="sub">.*?published <code>[^<]+</code>\. ).*?(</p>)',
                    lambda m:m[1]+intro+m[2],document,count=1,flags=re.S)
    document=re.sub(r'(<b id="wins">).*?(</b>)',lambda m:m[1]+f'{wins} / 401'+m[2],document)
    for prefix in ('Builder-mode timing: ',': '):
        def totals(c): return prefix+f"{c['faster']} faster, {c['slower']} slower, {c['inconclusive']} inconclusive, "+('and ' if prefix.startswith('Builder') else '')+f"{c['pending']} pending."
        document=document.replace(totals(oldcounts),totals(status['measurement_totals']))
    document=document.replace(f"{old_builder_count} / {oldcounts['expected']}",f"{len(rows)} / {oldcounts['expected']}").replace(f'{old_builder_pairs} API pairs',f"{len({r['pair'] for r in rows})} API pairs")
    body=[]
    for row in sorted(rows,key=lambda r:(r['pair'],r['bars'],r['mode'])):
        values=[row['pair'],f"{row['bars']:,}",'Full history' if row['mode']=='BuilderFull' else 'Latest only',
                f"{row['ooples']['mean_ns']/1e6:.6f}",f"{row['native']['NativeSingle']['mean_ns']/1e6:.6f}",
                f"{row['native']['NativeParallel']['mean_ns']/1e6:.6f}",
                {'faster':'Faster than both','slower':'Slower than at least one','inconclusive':'Inconclusive'}[row['verdict']]]
        label=row['run_id']
        if 'measurement_machine' in row: label+=f" · {row['measurement_machine']} · shard {row['measurement_shard']} · attempt {row['measurement_run_attempt']}"
        body.append('<tr data-builder-pair="'+html.escape(row['pair'],quote=True)+'" data-bars="'+str(row['bars'])+'" data-mode="'+row['mode']+'">'+''.join('<td>'+html.escape(x)+'</td>' for x in values)+'<td><a href="'+html.escape(row['measurement_url'],quote=True)+'">'+html.escape(label)+'</a></td></tr>')
    document,n=re.subn(r'(<table id="builder-current-results">.*?<tbody>).*?(</tbody>)',lambda m:m[1]+''.join(body)+m[2],document,flags=re.S)
    assert n==1
    source_html='<p>'+' '.join(f'{n} comparisons: <code>{sha}</code>, <a href="{html.escape(link,quote=True)}">measurement evidence</a>.' for (sha,link),n in groups.items())+disclosure+'</p>'
    document,n=re.subn(r"\$\('source'\)\.innerHTML=[^\r\n]*;",lambda _:"$('source').innerHTML="+json.dumps(source_html)+';',document,count=1,flags=re.M)
    assert n==1
    # CSV uses the union because unchanged rows intentionally retain their original fields.
    old="Object.keys(D.rows[0]).filter(k=>k!=='raw')"
    assert old in document
    document=document.replace(old,"[...new Set(D.rows.flatMap(r=>Object.keys(r)))].filter(k=>k!=='raw')")
    old="${esc(r.raw.Ooples.host.ProcessorName)}"
    assert old in document
    document=document.replace(old,"${r.measurement_machine ? esc(r.measurement_location+' / '+r.measurement_machine+' / shard '+r.measurement_shard+' / attempt '+r.measurement_run_attempt)+' · ' : ''}"+old)
    sys.path.insert(0,str(repo/'benchmarks'))
    from primary_baseline_report import replace_primary
    document=replace_primary(document,data)
    out.mkdir(parents=True,exist_ok=True)
    assert not (out/'before.html').exists() or (out/'before.html').read_bytes()==original
    (out/'before.html').write_bytes(original)
    (out/'candidate.html').write_text(document,encoding='utf-8')
    from .pipeline import write
    write(out/'measurement.json',dict(source_sha=source,run_id=run,measurement_url=url))
    write(out/'staged.json',dict(before_sha256=hashlib.sha256(original).hexdigest(),candidate_sha256=digest(out/'candidate.html'),
        changed_baseline_rows=2*len(pairs),unchanged_baseline_rows=unchanged,changed_builder_slots=4*len(builder_pairs),
        added_builder_slots=added,replaced_builder_slots=replaced,builder_rows=rows,verdicts=counts,run_id=run))
    return out/'candidate.html'
