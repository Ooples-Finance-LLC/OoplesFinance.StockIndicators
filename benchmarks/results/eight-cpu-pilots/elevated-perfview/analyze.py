import collections,json,re,sys,zipfile,xml.etree.ElementTree as E
from pathlib import Path
root=Path(sys.argv[1]); rows=[]
for path in root.glob('*.perfView.xml.zip'):
 name=path.name.removesuffix('.perfView.xml.zip')
 log=(root/(name+'.log')).read_text(errors='replace')
 pid=int(re.search(r'PROFILE START .*pid=(\d+)',log)[1])
 with zipfile.ZipFile(path) as archive:
  xml=E.fromstring(archive.read(archive.namelist()[0]))
 source=xml.find('StackSource')
 frames={int(e.attrib['ID']):e.text or '' for e in source.find('Frames')}
 stacks={int(e.attrib['ID']):(int(e.attrib['FrameID']),int(e.attrib['CallerID'])) for e in source.find('Stacks')}
 cache={}
 def chain(index):
  if index<0:return ()
  if index not in cache:
   frame,caller=stacks[index]; cache[index]=(frames[frame],)+chain(caller)
  return cache[index]
 process=[]; loop=[]
 for sample in source.find('Samples'):
  a=sample.attrib; names=chain(int(a['StackID']))
  if not any('dotnet ('+str(pid)+')' in frame for frame in names):continue
  metric=float(a.get('Metric',1)); timestamp=float(a['Time'])
  item=(timestamp,metric,names); process.append(item)
  if any('PilotCostBoundaryProfile' in f and 'Run' in f for f in names) and not any('PilotCostBoundaryBenchmarks.Setup' in f for f in names):loop.append(item)
 assert process and loop,(name,pid,'Missing expected process/loop')
 start=min(t for t,_,_ in loop)+3000; stop=max(t for t,_,_ in loop)
 selected=[s for s in loop if s[0]>=start]
 exc=collections.Counter(); inc=collections.Counter()
 for _,weight,names in selected:
  exc[names[0]]+=weight
  for f in set(names):inc[f]+=weight
 total=sum(exc.values()); assert total>1000,(name,total)
 row={'case':name,'pid':pid,'wholeProcessCpuSampleMs':sum(m for _,m,_ in process),'steadyForegroundCpuSampleMs':total,'approximateSteadyWindowMs':[start,stop],'scope':'Exact logged PID; foreground Run stacks excluding Setup; first 3 seconds removed approximately. Inlining attribution remains inclusive in caller. Whole-process CPU includes warmup/setup/GC threads.','topExclusive':[{'frame':k,'ms':v,'percent':v/total*100} for k,v in exc.most_common(16)],'topInclusive':[{'frame':k,'ms':v,'percent':v/total*100} for k,v in inc.most_common(32)],'unresolvedLeafPercent':sum(v for k,v in exc.items() if k.endswith('!?') or 'UNKNOWN' in k)/total*100}
 rows.append(row)
 print(name,'samples',round(total,1),'unresolved',round(row['unresolvedLeafPercent'],1))
 for entry in row['topExclusive'][:8]:print(round(entry['percent'],1),entry['frame'].split('!',1)[-1][:170])
(root/'cpu-summary.json').write_text(json.dumps(rows,indent=2),encoding='utf-8')
