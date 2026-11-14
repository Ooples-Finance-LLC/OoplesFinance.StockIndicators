"""Replace obsolete live-controller status with evidence-backed report coverage."""
import datetime
import hashlib
import html
import json
import re
import subprocess

from primary_baseline_report import embedded, script

REPOSITORY = 'Ooples-Finance-LLC/OoplesFinance.StockIndicators'
STATUS_FILTER_SCRIPT = "(()=>{for(const [input,table] of [['status-pair-search','status-pairs'],['status-formula-search','status-formulas']]){document.getElementById(input).addEventListener('input',event=>{const query=event.target.value.toLowerCase();for(const row of document.getElementById(table).tBodies[0].rows)row.hidden=!row.textContent.toLowerCase().includes(query);});}})();"


def render_status(document, published_head, inventory, production_matches, updated_utc):
    if not re.fullmatch('[0-9a-f]{40}', published_head):
        raise ValueError('A complete published commit is required')
    data = embedded(document, 'dataset')
    source = data['evidence']['sourceSha']
    if not source:
        raise ValueError('Current status refresh requires a verified full baseline')
    builder = embedded(document, 'current-competitor-data')
    totals = dict(builder['verdicts'])
    measured = sum(totals[name] for name in ('faster', 'slower', 'inconclusive'))
    expected = embedded(document, 'rollout-status-data')['measurement_totals']['expected']
    if measured > expected or any(v < 0 for v in totals.values()):
        raise ValueError('Invalid builder coverage totals')
    totals.update(expected=expected, measured=measured, pending=expected-measured)
    formulas = inventory['batch_formulas']
    checked = sum(row['status'] == 'contract-receipts-recorded-performance-pending' for row in formulas)
    pending_facades = sum(row['status'] == 'pending-assessment' for row in inventory['handwritten_facades'])
    pairs = len({row['pair'] for row in data['rows']})
    pr = f'https://github.com/{REPOSITORY}/pull/266'
    status = dict(updated_utc=updated_utc, published_head=published_head, inventory_commit=published_head,
        candidate=source, formula_counts=dict(total=len(formulas), correctness_receipts=checked),
        remaining_assessment=dict(handwritten_facades_pending=pending_facades),
        measurement_totals=totals, baseline_pairs=pairs, baseline_comparisons=len(data['rows']),
        measurement_run_url=data['evidence']['runUrl'], source_matches_published_production=production_matches,
        checks_url=pr+'/checks')
    relationship = ('The measured production source matches the published production tree.' if production_matches else
        'Later production changes in the published PR are not measured by this baseline run.')
    section = (
        '<section id="rollout-status" class="section">'
        '<div class="kicker">Current qualification status</div><h2>Where the work stands</h2>'
        f'<p class="sub"><a href="{pr}">PR #266 &rarr; master</a>, published '
        f'<code>{published_head[:8]}</code>. Measured source: <code>{html.escape(source[:8])}</code>. '
        f'{relationship}</p><div class="metrics">'
        f'<div class="metric"><b>{checked} / {len(formulas)}</b><span>Batch formulas with correctness receipts</span></div>'
        f'<div class="metric"><b>{pending_facades}</b><span>Handwritten facades pending assessment</span></div>'
        f'<div class="metric"><b>{len(data["rows"])}</b><span>Matched baseline comparisons</span></div>'
        f'<div class="metric"><b>{measured} / {expected}</b><span>Builder-mode slots with matched timing</span></div>'
        '</div><p>The baseline counts API pairs at each input size; builder-mode coverage also counts history mode. '
        'Formula receipts describe correctness coverage and do not establish competitor speed.</p>'
        f'<p>Builder-mode timing: {totals["faster"]} faster, {totals["slower"]} slower, '
        f'{totals["inconclusive"]} inconclusive, and {totals["pending"]} pending. '
        'Performance and remaining coverage work are unfinished.</p>'
        f'<p><a href="{html.escape(data["evidence"]["runUrl"], quote=True)}">Full baseline measurement and raw evidence</a> '
        f'&middot; <a href="{pr}/checks">Live PR checks</a></p></section>')
    result, count = re.subn(r'<section\b[^>]*\bid="rollout-status"[^>]*>.*?</section>',
                          lambda _: section, document, flags=re.S)
    if count != 1:
        raise ValueError('Expected one current status section')
    result, count = re.subn(r'<script[^>]*id="rollout-status-data"[^>]*>.*?</script>',
                          lambda _: script('rollout-status-data', status), result, flags=re.S)
    if count != 1:
        raise ValueError('Expected one current status dataset')
    def retire_status_filters(match):
        body = match[1].strip()
        if 'status-pair-search' not in body and 'status-formula-search' not in body:
            return match[0]
        if body != STATUS_FILTER_SCRIPT:
            raise ValueError('Unexpected status-filter script; preserve unrelated UI code')
        return ''
    result = re.sub(r'<script>(.*?)</script>', retire_status_filters, result, flags=re.S)
    for name in ['dataset', 'archived-baseline-dataset', 'current-competitor-data']:
        assert embedded(document, name) == embedded(result, name)
    return result, status


def refresh_status(document, repo):
    pr = json.loads(subprocess.check_output(['gh', 'api', f'repos/{REPOSITORY}/pulls/266'], text=True))
    if pr['base']['ref'] != 'master' or pr['state'] != 'open':
        raise ValueError('Expected open PR266 targeting master')
    head = pr['head']['sha']
    inventory_raw = subprocess.check_output(['git', 'show', head+':benchmarks/results/remaining-indicators/inventory.json'], cwd=repo)
    source = embedded(document, 'dataset')['evidence']['sourceSha']
    changes = subprocess.check_output(['git', 'diff', '--name-only', source, head, '--', 'src',
        'Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props', 'global.json',
        'NuGet.Config', 'nuget.config', 'benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks'], cwd=repo, text=True)
    updated, receipt = render_status(document, head, json.loads(inventory_raw), not changes.strip(),
                                    datetime.datetime.now(datetime.timezone.utc).isoformat())
    receipt['inventory_sha256'] = hashlib.sha256(inventory_raw).hexdigest()
    receipt['changed_production_files'] = changes.splitlines()
    return updated, receipt
