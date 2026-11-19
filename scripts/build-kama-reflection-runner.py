"""Build only the reviewed package-free bootstrap tool; never production or benchmarks."""
import argparse,os,subprocess,tempfile
from pathlib import Path

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan',required=True);parser.add_argument('--out',required=True)
    args=parser.parse_args();os.environ['HOSTED_TIMING_PLAN']=str(Path(args.plan).resolve())
    from hosted_timing import kama_fused_prefix_roi as q
    plan=q.p.read(q.p.PLAN);assert q.runner_source_pins()==plan['runner_source_pins']
    project=q.p.REPO/'scripts/kama-preflight/KamaPreflight.csproj'
    assert 'PackageReference' not in project.read_text() and 'ProjectReference' not in project.read_text()
    out=Path(args.out).resolve();assert not out.exists();out.parent.mkdir(parents=True,exist_ok=True)
    sdk=subprocess.check_output(['dotnet','--version'],text=True).strip()
    with tempfile.TemporaryDirectory(prefix='kama-empty-package-source-') as empty:
        command=['dotnet','build',str(project),'--configuration','Release','--output',str(out),'--source',empty,'--nologo',
            '--disable-build-servers','-p:UseSharedCompilation=false',
            '-p:ImportDirectoryBuildProps=false','-p:ImportDirectoryBuildTargets=false']
        process=subprocess.run(command,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
    log=out.with_name(out.name+'-build.log');log.write_bytes(process.stdout)
    assert process.returncode==0,'First bootstrap-tool build failure preserved; no automatic retry'
    manifest=q.verify_runner_runtime(out,plan,require_receipt=False)
    q.p.write(out/'build-receipt.json',dict(runner_source_pins=q.runner_source_pins(),runtime_manifest=manifest,
        sdk=sdk,command=command,exit_code=0,log_sha256=q.p.digest(log),production_or_benchmark_builds=0,packages_added=0))
    q.verify_runner_runtime(out,plan)
    print(q.p.digest(out/'build-receipt.json'))

if __name__=='__main__':main()
