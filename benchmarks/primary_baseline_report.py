"""Replace the report's primary comparison dataset using verified raw measurements."""
import gzip
import hashlib
import html
import json
import re
import subprocess
from pathlib import Path

from hosted_performance_results import load_results


def embedded(document, name):
    match = re.search(r'<script[^>]*id="' + re.escape(name) + r'"[^>]*>(.*?)</script>', document, re.S)
    if not match:
        raise ValueError('Missing dataset: ' + name)
    return json.loads(match[1])


def script(name, data):
    return '<script id="' + name + '" type="application/json">' + json.dumps(data, ensure_ascii=True).replace('<', '\\u003c') + '</script>'


def measured_dataset(folder, source, run, manifest, versions):
    verified = load_results(folder, source, run, manifest)
    raw_arms = {}
    for receipt in verified['evidence']['reports']:
        raw = gzip.decompress((Path(folder) / receipt['file']).read_bytes())
        if hashlib.sha256(raw).hexdigest() != receipt['sha256']:
            raise ValueError('Raw evidence changed during import')
        report = json.loads(raw.decode('utf-8-sig'))
        for arm in report['Benchmarks']:
            pair = re.search(r'PairId: "([^"\r\n]+)"', arm['FullName'])[1]
            bars = int(re.search(r'Bars: (\d+)', arm['FullName'])[1])
            raw_arms[pair, bars, arm['Method']] = dict(samples=arm['Statistics']['OriginalValues'],
                host=report['HostEnvironmentInfo'], report=receipt['artifactPath'])
    rows = []
    for row in verified['rows']:
        rows.append(dict(row, library=row['pair'].split('.')[0], source_sha=source,
            measurement_run_url=verified['evidence']['runUrl'], raw={
            arm: raw_arms[row['pair'], row['bars'], arm] for arm in ['Ooples', 'Competitor']}))
    return dict(rows=rows, inventory=json.loads(manifest), evidence=verified['evidence'], versions=versions)


def replace_primary(document, dataset):
    """All existing charts, details, filters and CSV consume this single dataset."""
    original = embedded(document, 'dataset')
    if 'id="archived-baseline-dataset"' not in document:
        document = document.replace('</body>', script('archived-baseline-dataset', original) + '</body>')
    document, count = re.subn(r'<script[^>]*id="dataset"[^>]*>.*?</script>',
        lambda _: script('dataset', dataset), document, flags=re.S)
    if count != 1:
        raise ValueError('Expected exactly one primary dataset')
    source = dataset['evidence']['sourceSha']
    comparisons = len(dataset['rows'])
    pairs = len({r['pair'] for r in dataset['rows']})
    wins = sum(r['bars'] == 10000 and r['competitor_over_ooples'] > 1 for r in dataset['rows'])
    replacements = {
        'October 8 competitor baseline': 'Refreshed competitor baseline',
        'October 8 baseline</a>': 'Competitor baseline</a>',
        'Archived indicators</a>': 'Complete results</a>',
        'href="#current-competitors">Fresh competitor results': 'href="#explorer">Complete competitor results',
        'Complete current competitor results</h2>': 'Builder-mode qualification progress</h2>',
        'is the complete current results view,': 'tracks builder-mode qualification,',
        '<a href="#hosted-measurement-results">Hosted LibraryPair diagnostics</a>': '<a href="#explorer">Refreshed competitor baseline measurements</a>',
        'October 8 paired APIs': 'paired APIs in this measured build',
        '02 / Archived competitor results — October 8, 2026': '02 / Complete competitor results',
        'Explore the October 8 baseline': 'Complete competitor results',
        'Historical results from commit 848a5ec8. These values do not measure the optimized candidate. Click an API for the original samples and runner details.':
            f'Measured source {source[:8]}. All {comparisons} comparisons below use refreshed matched timings. Click an API for samples and runner details.',
        'Export historical CSV': 'Export results CSV',
        'October 8 baseline gaps': 'Measured performance gaps',
        'October 8 methodology and evidence': 'Measurement methodology and evidence',
        'October 8 archived comparison': 'Refreshed baseline comparison',
        '${wins} / 401': '${wins} / ${rows.length}',
        'Most observed Ooples wins are against Trady. These results expose substantial gaps against the other libraries; correctness parity alone does not imply performance parity.':
            'Equal means: <b>${rows.filter(x=>x.competitor_over_ooples===1).length}</b>. The four library cards and complete results below account for every measured pair. Lower means alone do not establish statistical significance.',
        'max=top[0].ooples_mean_ns-top[0].competitor_mean_ns':
            'max=top.length?top[0].ooples_mean_ns-top[0].competitor_mean_ns:1',
        'Report PR #249': 'Optimization PR #266',
        '/pull/249"': '/pull/266"',
        'The 401-pair explorer below retains the October 8 full-library baseline.':
            f'The {pairs}-pair explorer below uses refreshed measurements from {source[:8]}.',
    }
    for old, new in replacements.items():
        document = document.replace(old, new)
    document = re.sub(r'(<section id="explorer"[^>]*>.*?<p class="sub">).*?(</p>)',
        lambda m: m[1] + f'Measured source {source[:8]}. All {comparisons} comparisons below use refreshed matched timings. '
        'Click an API for samples and runner details.' + m[2], document, count=1, flags=re.S)
    document = re.sub(r'<h3>Fresh full-library timing run</h3><p>.*?</p>|<p>(?:Refreshed baseline source:|Base source:).*?</p>',
        lambda _: '<p>Refreshed baseline source: ' + html.escape(source[:8]) + '. '
        '<a href="' + html.escape(dataset['evidence']['runUrl'], quote=True) + '">Measurement run and raw evidence</a>. '
        'All primary baseline tables and charts use these verified results.</p>', document, flags=re.S)
    document = re.sub(r'<div id="hosted-measurement-results">.*?</div>',
        '<p>Refreshed LibraryPair measurements are shown in the <a href="#explorer">complete results table</a>.</p>', document, flags=re.S)
    document = re.sub(r'(<b id="wins">).*?(</b>)', lambda m: m[1] + f'{wins} / {pairs}' + m[2], document)
    for label, value in [('paired APIs in this measured build', pairs),
                         ('pair × input-size comparisons', comparisons), ('measured library arms', comparisons * 2)]:
        document = re.sub(r'<b>[\d,]+</b><span>' + re.escape(label) + '</span>',
            lambda _: f'<b>{value:,}</b><span>{label}</span>', document)
    hero = (f'<section class="hero"><div class="kicker">Refreshed competitor measurements</div>'
        f'<h1>Competitor performance:<br>complete measured results.</h1><p class="lede">'
        f'{pairs} APIs, {comparisons} size comparisons and {comparisons * 2} measured arms. '
        f'Ooples has a lower observed mean for {wins} / {pairs} APIs at 10,000 bars. '
        f'Measured build: {html.escape(source[:8])}. These baseline batch timings are separate from '
        'builder-mode qualification below; they do not establish universal superiority.</p></section>')
    document = re.sub(r'<section class="hero">.*?</section>', lambda _: hero, document, count=1, flags=re.S)
    # Put the requested numbers first; retain detailed rollout evidence afterward.
    sections = []
    for name in ['overview', 'explorer', 'priorities', 'method', 'unavailable']:
        pattern = r'<section\b[^>]*\bid="' + name + r'"[^>]*>.*?</section>'
        match = re.search(pattern, document, re.S)
        if match:
            sections.append(match[0])
            document = document[:match.start()] + document[match.end():]
    if sections:
        end = document.index('</section>', document.index('<section class="hero">')) + len('</section>')
        document = document[:end] + ''.join(sections) + document[end:]
    assert embedded(document, 'dataset') == dataset
    return document


def refresh(document, repo, work):
    folder = Path(work) / 'si-primary-baseline-refresh-20261021'
    if not (folder / 'summary/evidence.json').exists():
        # A later status refresh may run on another evidence root. Keep the
        # already-imported baseline and its visible totals, not builder totals.
        if 'id="archived-baseline-dataset"' in document:
            return replace_primary(document, embedded(document, 'dataset'))
        return document
    receipt = json.loads((folder / 'status.json').read_text(encoding='utf-8'))
    if receipt['status'] != 'completed' or receipt['conclusion'] != 'success':
        raise ValueError('Baseline import requires a successful measurement run')
    source = receipt['source_sha']
    if not re.fullmatch(r'[0-9a-f]{40}', source):
        raise ValueError('Baseline source must be pinned')
    manifest = subprocess.check_output(['git', 'show', source + ':benchmarks/competitor-library-manifest.json'], cwd=repo)
    packages = subprocess.check_output(['git', 'show', source + ':benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks.csproj'], cwd=repo).decode('utf-8-sig')
    versions = {}
    for label, package in [('Skender', 'Skender.Stock.Indicators'), ('TaLib', 'TALib.NETCore'), ('Trady', 'Trady.Analysis'), ('QuanTAlib', 'QuanTAlib')]:
        match = re.search(r'Include="' + re.escape(package) + r'"\s+Version="([^"]+)"', packages, re.I)
        if not match:
            raise ValueError('Missing package version: ' + package)
        versions[label] = match[1]
    dataset = measured_dataset(folder / 'summary', source, receipt['run_id'], manifest, versions)
    return replace_primary(document, dataset)
