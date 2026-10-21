"""Add a reconciled status ledger to an existing standalone performance report.

Reads receipts without changing validators or benchmark inputs. Missing evidence
is pending, never a speed win. Existing archived data remains byte-for-byte intact.
"""
import argparse
from collections import Counter, defaultdict
import datetime
import hashlib
import html
import json
from pathlib import Path
import re
import time

from performance_pair_mapping import reconcile


START = '<!-- rollout-status:start -->'
END = '<!-- rollout-status:end -->'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def embedded(document, name):
    match = re.search(r'<script[^>]*id="' + re.escape(name) + r'"[^>]*>(.*?)</script>', document, re.S)
    if not match:
        raise ValueError('Missing embedded dataset: ' + name)
    return json.loads(match[1])


def fresh_results(folder):
    rows = []
    seen = set()
    for path in sorted(folder.glob('Shared*Benchmarks.json')):
        receipt = read(path)
        if hashlib.sha256(Path(receipt['report']).read_bytes()).hexdigest() != receipt['report_sha256']:
            raise ValueError('Raw report hash mismatch: ' + str(path))
        for row in receipt['comparisons']:
            key = (receipt['fixture'], row['operation'], row['bars'], row['mode'])
            if key in seen:
                raise ValueError('Ambiguous fresh comparison: ' + str(key))
            seen.add(key)
            rows.append(dict(row, fixture=receipt['fixture'], commit=Path(receipt['snapshot']).name,
                             measured=receipt['completed_utc'], evidence=str(path)))
    return rows


def formula_rows(inventory):
    rows = []
    for row in inventory['batch_formulas']:
        options = row['options']
        receipts = row.get('contract_receipts', {})
        state = ('all listed options have historical receipts' if options and set(options) <= set(receipts)
                 else 'some options have historical receipts' if receipts else 'no published contract receipt')
        rows.append(dict(formula=row['indicator'], options=options, state=state,
                         missing=[x for x in options if x not in receipts],
                         receipts=sorted({p for paths in receipts.values() for p in paths})))
    assert len(rows) == inventory['counts']['batch_formulas']
    assert len({r['formula'] for r in rows}) == len(rows)
    return rows


def cell(value):
    return '<td>' + html.escape(str(value)) + '</td>'


def measurement_totals(summary):
    totals = dict(expected=0, measured=0, faster=0, slower=0, inconclusive=0, pending=0)
    seen = set()
    for library, mode, *values in summary:
        if (library, mode) in seen:
            raise ValueError('Duplicate library/mode summary')
        seen.add((library, mode))
        if len(values) != 6 or any(type(v) is not int or v < 0 for v in values):
            raise ValueError('Invalid measurement counts')
        expected, measured, faster, slower, inconclusive, pending = values
        if measured != faster + slower + inconclusive or expected != measured + pending:
            raise ValueError('Measurement counts do not reconcile')
        for key, value in zip(totals, values):
            totals[key] += value
    return totals


def table(headers, rows, name):
    return ('<div class="tablewrap" style="max-height:540px;overflow:auto"><table id="' + name + '"><thead><tr>'
            + ''.join('<th>' + html.escape(h) + '</th>' for h in headers)
            + '</tr></thead><tbody>' + ''.join('<tr>' + ''.join(cell(v) for v in row) + '</tr>' for row in rows)
            + '</tbody></table></div>')


def ci_snapshot_html(receipt, published_head):
    if not receipt:
        return '<p>No hosted CI snapshot is recorded for this report.</p>'
    same = receipt.get('head') == published_head
    scope = 'matches the published commit' if same else 'is for a different commit; it does not establish current CI status'
    counts = Counter(row.get('conclusion') or row.get('status', 'unknown') for row in receipt.get('checks', []))
    text = ', '.join(f'{count} {state}' for state, count in sorted(counts.items()))
    content = f'<p>Hosted CI snapshot {html.escape(receipt.get("head", "unknown")[:8])} at {html.escape(receipt.get("checked_utc", "unknown"))} {scope}: {html.escape(text)}.</p>'
    failed = [[row['name'], row.get('conclusion'), row.get('url', '')] for row in receipt.get('checks', [])
              if row.get('conclusion') not in [None, 'success', 'skipped', 'neutral']]
    if failed:
        content += table(['Check', 'Recorded result', 'Evidence URL'], failed, 'status-hosted-ci')
    return content


def render(document, repo, work):
    baseline = embedded(document, 'dataset')
    inventory = read(repo / 'benchmarks/results/remaining-indicators/inventory.json')
    formulas = formula_rows(inventory)
    folder = work / 'si-competitor-refresh-20261021'
    status = read(folder / 'status.json')
    fresh = fresh_results(folder)
    coverage = read(folder / 'coverage-navigation.json')
    fixtures = read(folder / 'cpu-fixtures.json')
    publication = read(work / 'si-pr266-publication-20261021/published.json')
    ci_path = work / 'si-pr266-hosted-ci.json'
    hosted_ci = read(ci_path) if ci_path.exists() else {}
    local_validation = []
    for label, relative in [
        ('Oscillators and Floor aliases publication', 'si-pr266-followons-20261021/status.json'),
        ('Recursive aliases, KAMA, Liquid RSI and compatibility publication', 'si-pr266-recursive-kama-liquid-20261021/status.json'),
        ('Framework compatibility repair', 'si-Z-only-candidates-20261020/rollout-framework-compat-20261021/status.json')]:
        path = work / relative
        if path.exists():
            receipt = read(path)
            local_validation.append([label, receipt.get('status', 'unknown'), receipt.get('step', 'not started'), receipt.get('updated_utc', 'unknown')])
    navigation = read(work / 'si-unmapped-navigation-after-support-resistance-20261020.json')
    chain = read(work / 'si-pending-chain-static-reconciliation-v19-20261020.json')
    now = datetime.datetime.now(datetime.timezone.utc).isoformat(timespec='seconds')
    gpu_status = ('Fresh source-matched GPU validation passed all 100 cases and is published.'
                  if 'Fresh source-matched GPU100passed with master dependencies' in publication['verification']
                  else 'The saved GPU receipt failed the combined-source fingerprint gate; fresh validation is pending.')
    publication_validation = work / 'si-pr266-publication-20261021/validation-status.json'
    validation = read(publication_validation) if publication_validation.exists() else {'status': 'combined C# validation pending'}
    pending_path = work / 'si-pr266-general-filter-20261021/status.json'
    pending_publication = read(pending_path) if pending_path.exists() else {}
    controllers = ('Validation controllers are running; the benchmark retry waits without holding them.'
                   if status['status'] == 'waiting-quiet-validation-resumed'
                   else 'Owned validation controllers are held while the timing runner waits or measures.'
                   if status['status'] in ['waiting-quiet-machine', 'measuring', 'holding-owned-validation-controllers']
                   else 'See the recorded queue stages below; a last-recorded running state is not a live process guarantee.')
    old = defaultdict(dict)
    for row in baseline['rows']:
        assert row['bars'] not in old[row['pair']]
        old[row['pair']][row['bars']] = row
    mapping_path = Path(__file__).resolve().parent / 'results/remaining-indicators/competitor-operation-mapping.json'
    operation_mapping = read(mapping_path) if mapping_path.exists() else {'mappings': []}
    fresh_by_key, unmatched = reconcile(fresh, operation_mapping['mappings'], set(old), operation_mapping.get('snapshot'))
    mapped = defaultdict(list)
    for fixture, pairs in coverage['fixtures'].items():
        for pair in pairs:
            mapped[pair].append(fixture)
    for row in operation_mapping['mappings']:
        if row['fixture'] not in mapped[row['pair']]:
            mapped[row['pair']].append(row['fixture'])
    summary = []
    for library in sorted({r['library'] for r in baseline['rows']}):
        pairs = {r['pair'] for r in baseline['rows'] if r['library'] == library}
        for mode in ['BuilderFull', 'BuilderLatestOnly']:
            outcomes = Counter(fresh_by_key[(p, bars, mode)]['verdict'] for p in pairs for bars in [1000, 10000]
                               if (p, bars, mode) in fresh_by_key)
            completed = sum(outcomes.values())
            assert set(outcomes) <= {'faster', 'slower', 'inconclusive'}
            assert completed <= len(pairs) * 2
            summary.append([library, mode, len(pairs) * 2, completed, outcomes['faster'], outcomes['slower'],
                            outcomes['inconclusive'], len(pairs) * 2 - completed])
    pair_rows = []
    totals = measurement_totals(summary)
    for pair, sizes in sorted(old.items()):
        for bars, historical in sorted(sizes.items()):
            for mode in ['BuilderFull', 'BuilderLatestOnly']:
                current = fresh_by_key.get((pair, bars, mode))
                pair_rows.append([pair, historical['ooples_indicator'], bars, mode,
                    current['verdict'] if current else 'pending measurement',
                    f"{current['builder_mean_ns']/1e6:.6f}" if current else '—',
                    f"{current['native_mean_ns']/1e6:.6f}" if current else '—',
                    f"{current['native_over_builder']:.4f}" if current else '—',
                    current['measured'] if current else '—',
                    current['commit'] if current else 'not measured',
                    ', '.join(mapped[pair]) if pair in mapped else 'fixture mapping needs reconciliation',
                    f"{historical['ooples_mean_ns']/1e6:.6f}" if mode == 'BuilderFull' else 'not comparable',
                    f"{historical['competitor_mean_ns']/1e6:.6f}" if mode == 'BuilderFull' else 'not comparable'])
    queue = []
    for batch in [dict(finisher='si-finish-general-filter-20261020.py', new_factory_types=['GeneralFilterEstimator']), *chain['batches']]:
        slug = Path(batch['finisher']).stem.removeprefix('si-finish-').removesuffix('-20261020')
        stages = []
        for path in [work / 'si-Z-only-candidates-20261020' / slug / 'status.json',
                     work / ('si-' + slug + '-integration-20261020') / 'status.json']:
            if path.exists():
                state = read(path)
                stages.append(state.get('status', 'unknown') + ' / ' + str(state.get('step', ''))
                              + ' (recorded ' + state.get('updated_utc', 'unknown') + ')')
            else:
                stages.append('no status receipt at this path')
        queue.append([slug, ', '.join(batch['new_factory_types']), *stages])
    counts = Counter(r['state'] for r in formulas)
    head = publication['published_head'][:8]
    data = dict(updated_utc=now, published_head=head, candidate=status['commit'][:8],
                formula_counts=dict(counts), formulas=formulas, competitor_summary=summary, measurement_totals=totals,
                baseline_pairs=len(old), baseline_comparisons=len(baseline['rows']), fresh=fresh,
                unmatched_fresh=unmatched, mapped_api_pairs=operation_mapping.get('mapped_pairs', 0),
                runner=status, hosted_ci=hosted_ci, local_validation=local_validation, publication_validation=validation, pending_publication=pending_publication, queue=queue, remaining_assessment=navigation['remaining'])
    content = f'''{START}<section id="rollout-status" class="section">
<div class="kicker">Current status — updated {now}</div><h2>Where the work stands</h2>
<p class="sub"><strong>Not finished. Current performance superiority is unproven.</strong>
<a href="https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/pull/266">Draft PR #266 → master</a>
contains the published integration checkpoint {head}. The timing candidate is {status['commit'][:8]}; these are different builds.</p>
<div class="metrics">
<div class="metric"><b>{totals['measured']} / {totals['expected']}</b><span>Matched API slots measured for the timing candidate</span></div>
<div class="metric"><b>{totals['faster']}</b><span>Candidate slots faster than their matched competitor</span></div>
<div class="metric"><b>{totals['slower']} / {totals['inconclusive']}</b><span>Candidate slots slower / inconclusive</span></div>
<div class="metric"><b>{totals['pending']}</b><span>API slots still awaiting matched measurements</span></div>
</div><p>These totals count API pair × input size × history mode, not unique indicators.
Measured = faster + slower + inconclusive; expected = measured + pending.
Results for a preserved candidate do not qualify a later published build. Each measured row identifies its source snapshot.
Formula correctness receipts and the dated archives below are separate evidence and are not added to these totals.</p>
<p>Fresh timing queue: <strong>{html.escape(status['status'])}</strong>, fixture {html.escape(status.get('fixture', '—'))}.
{len(status['completed'])} / {len(fixtures)} fixture batches completed; {len(status['failed'])} failures recorded.
Quiet window: {status.get('quiet_seconds', 0)} / 240 seconds, last runner update {html.escape(status['updated_utc'])}.
{controllers}</p>
<p>Earlier publication receipt: six inventory tests passed. Recorded validation: <strong>{html.escape(validation['status'])}</strong>,
step {html.escape(validation.get('step', 'pending'))}. {gpu_status}
<a href="https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/pull/266/checks">live GitHub checks</a>
are linked here. Earlier correctness receipts do not establish current performance.</p>
{ci_snapshot_html(hosted_ci, publication["published_head"])}
{table(["Local validation", "Recorded status", "Step", "Updated UTC"], local_validation, "status-local-validation")}
<p>General Filter batch validation: {html.escape(pending_publication.get('status', 'no receipt'))}; step {html.escape(pending_publication.get('step', 'unknown'))}; input {html.escape(pending_publication.get('input_head', 'unknown')[:8])}; last update {html.escape(pending_publication.get('updated_utc', 'unknown'))}. {('This batch is included in published coverage totals.' if pending_publication.get('published_head') else 'This batch is not included in published coverage totals.')}</p>
<h3>Timing candidate competitor coverage</h3><p>Each library total counts API pairs × two sizes. Both Ooples history modes are shown separately.
Each row reconciles: faster + slower + inconclusive + pending = expected. Blank timings mean unmeasured, never zero.</p>'''
    content += table(['Library', 'Mode', 'Expected', 'Measured', 'Faster', 'Slower', 'Inconclusive', 'Pending'],
                     [*summary, ['All libraries', 'Both modes', *totals.values()]], 'status-summary')
    content += f'<p>{len(old)} historical API pairs, {len(baseline["rows"])} size comparisons. The {len(fixtures)} benchmark fixtures are a different unit and do not prove complete API coverage. Explicit source-name matches are navigation only; unmapped families still require reconciliation.</p>'
    content += '<h3>Every competitor pair</h3><p>Search by API, indicator, fixture or verdict. Current timings use matched builder/native runs. The last two columns retain October 8 baseline means for reference only; they cannot establish a before/after speedup across different hosts and builds.</p><input id="status-pair-search" aria-label="Search competitor comparisons" placeholder="Search competitor comparisons" style="width:100%;padding:12px">'
    content += table(['API pair', 'Ooples indicator', 'Bars', 'Mode', 'Candidate verdict', 'Candidate Ooples ms', 'Matched native ms', 'Native / Ooples', 'Measured UTC', 'Measured source snapshot', 'Fixture navigation', 'Oct 8 Ooples ms', 'Oct 8 native ms'], pair_rows, 'status-pairs')
    content += f'<h3>Measurement reconciliation</h3><p>{len(fresh)} fresh measurement rows = {len(fresh_by_key)} matched API slots + {len(unmatched)} supplemental or unresolved rows. Multiple fixtures for a slot require review; the report never chooses the fastest result. Static operation mapping covers {operation_mapping.get("mapped_pairs", 0)} of {len(old)} API pairs; mapping is not measurement.</p>'
    if unmatched:
        content += table(['Fixture', 'Operation', 'Bars', 'Mode', 'Measured verdict', 'Mapping status', 'Evidence'], [[r['fixture'], r['operation'], r['bars'], r['mode'], r['verdict'], r['mapping_status'], r['evidence']] for r in unmatched], 'status-unmatched')
    content += '<h3>All formula correctness receipts</h3><p>' + '; '.join(f'{v} {html.escape(k)}' for k, v in sorted(counts.items())) + f' = {len(formulas)} formulas. These categories are disjoint. Receipt coverage is historical correctness evidence, not proof of optimization or current performance. Options without receipts remain visible.</p><input id="status-formula-search" aria-label="Search formula coverage" placeholder="Search formula, option or evidence" style="width:100%;padding:12px">'
    content += table(['Formula', 'Published evidence', 'Options lacking receipts', 'Receipt files'],
                     [[r['formula'], r['state'], ', '.join(r['missing']) or ('no builder options mapped' if not r['options'] else 'none'), ', '.join(r['receipts']) or 'none'] for r in formulas], 'status-formulas')
    content += '<details><summary>Prepared batch queue — last recorded validation stages</summary><p>Prepared code is not completed integration. A missing status path is unknown, not a failure or pass.</p>'
    content += table(['Batch', 'Indicator types', 'Candidate receipt', 'Integration receipt'], queue, 'status-queue') + '</details>'
    content += f'<details><summary>{len(navigation["remaining"])} formulas still requiring assessment or evidence reconciliation</summary>'
    content += table(['Formula', 'Assessment'], [[r['formula'], r.get('assessment', 'Assessment pending')] for r in navigation['remaining']], 'status-assessment') + '</details>'
    content += '<p class="small">Fresh receipt hashes are verified before rendering. This ledger adds no timings and does not count archived measurements as current wins.</p></section>'
    content += '<script id="rollout-status-data" type="application/json">' + json.dumps(data, ensure_ascii=True).replace('<', '\\u003c') + '</script>'
    content += '''<script>(()=>{for(const [input,table] of [['status-pair-search','status-pairs'],['status-formula-search','status-formulas']]){document.getElementById(input).addEventListener('input',event=>{const query=event.target.value.toLowerCase();for(const row of document.getElementById(table).tBodies[0].rows)row.hidden=!row.textContent.toLowerCase().includes(query);});}})();</script>''' + END
    cleaned = re.sub(re.escape(START) + r'.*?' + re.escape(END), '', document, flags=re.S)
    anchor = '<section id="current-competitors"'
    if anchor not in cleaned:
        raise ValueError('Missing current results anchor')
    result = cleaned.replace(anchor, content + anchor, 1)
    current = embedded(result, 'current-competitor-data')
    current['status'] = status
    result = re.sub(r'(<script id="current-competitor-data" type="application/json">).*?(</script>)',
                    lambda m: m[1] + json.dumps(current, ensure_ascii=True).replace('<', '\\u003c') + m[2], result, flags=re.S)
    section = re.search(r'<section id="current-competitors".*?</section>', result, re.S)
    if section:
        updated = re.sub(r'Status: [^.]+\.', 'Status: ' + html.escape(status['status']) + '.', section[0], count=1)
        result = result[:section.start()] + updated + result[section.end():]
    assert embedded(result, 'dataset') == baseline
    assert result.count('id="rollout-status"') == 1
    return result, data


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report', type=Path, required=True)
    parser.add_argument('--work', type=Path, required=True)
    parser.add_argument('--watch', action='store_true')
    parser.add_argument('--inventory-repo', type=Path, default=Path(__file__).resolve().parents[1],
                        help='Checkout holding the published inventory; may differ from this report generator.')
    args = parser.parse_args()
    repo = args.inventory_repo
    while True:
        try:
            original = args.report.read_text(encoding='utf-8')
            result, data = render(original, repo, args.work)
            if args.report.read_text(encoding='utf-8') == original:
                temporary = args.report.with_suffix('.status.tmp.html')
                temporary.write_text(result, encoding='utf-8', newline='')
                temporary.replace(args.report)
                print(json.dumps({k: data[k] for k in ['updated_utc', 'formula_counts', 'baseline_pairs']}), flush=True)
        except (OSError, ValueError) as error:
            if not args.watch:
                raise
            print('Refresh failed; prior report retained: ' + repr(error), flush=True)
        if not args.watch:
            break
        time.sleep(60)


if __name__ == '__main__':
    main()
