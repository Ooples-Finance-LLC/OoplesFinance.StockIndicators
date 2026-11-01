"""Archive independently verified hosted evidence without publishing production."""
from pathlib import Path
import gzip
import hashlib
import subprocess

from .pipeline import PLAN, read, write, digest, verify_inputs
from .verify import collect


def archive(raw, prepared, repo, phase):
    inputs = verify_inputs(prepared)
    summary = collect(raw, phase)
    assert summary['input_provenance_sha256'] == digest(prepared/'provenance.json')
    run_id, attempt = inputs['run_id'], inputs['run_attempt']
    name = f'hosted-{phase}-{run_id}-attempt{attempt}'
    destination = repo/'benchmarks/results/remaining-indicators'/name
    assert not destination.exists(), 'Inspect existing archive before retrying'
    blobs = {('raw/'+p.relative_to(raw).as_posix()):p.read_bytes() for p in raw.rglob('*') if p.is_file()}
    blobs.update({('prepared/'+p.relative_to(prepared).as_posix()):p.read_bytes()
                  for p in (prepared/'evidence').rglob('*') if p.is_file()})
    blobs['prepared/provenance.json'] = (prepared/'provenance.json').read_bytes()
    blobs['plan.json'] = PLAN.read_bytes()
    blobs.update({'scripts/'+p.name:p.read_bytes() for p in Path(__file__).parent.glob('*.py')})
    blobs['candidate-source.patch'] = subprocess.check_output(['git','diff','--binary',
        summary['sources']['old'],summary['sources']['new'],'--','src','tests'],cwd=repo)
    assert blobs['candidate-source.patch']
    manifest = {}
    for relative, content in sorted(blobs.items()):
        target = destination/(relative+'.gz')
        target.parent.mkdir(parents=True,exist_ok=True)
        target.write_bytes(gzip.compress(content,mtime=0))
        assert gzip.decompress(target.read_bytes()) == content
        manifest[relative] = dict(sha256=hashlib.sha256(content).hexdigest(),archive=target.relative_to(repo).as_posix())
    write(destination/'verified-summary.json',summary)
    write(destination/'decision.json',dict(status='qualified-for-integration' if summary['accepted'] else 'rejected-or-inconclusive',
        accepted=summary['accepted'],phase=phase,case_count=summary['case_count'],decision_count=summary['decision_count'],
        verdicts=summary['verdicts'],sources=summary['sources'],run_id=run_id,run_attempt=attempt,
        workflow_head=inputs['workflow_head'],input_provenance_sha256=summary['input_provenance_sha256'],
        measurement_url=f'https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/actions/runs/{run_id}',
        archives=manifest,production_integrated=False,report_imported=False,
        note='Hosted CPU timings. Historical source-matched local GPU checks are retained with their original identity. WMA/TRIMA results do not replace default SMA rows.'))
    (destination/'.gitattributes').write_bytes(b'* -text\n')
    return destination
