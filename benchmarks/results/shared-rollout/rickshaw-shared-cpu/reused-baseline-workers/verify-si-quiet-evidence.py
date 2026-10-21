import datetime
import json
from pathlib import Path


def verify_quiet_evidence(source, pid, samples):
    quiet = json.loads(Path(str(source) + '.quiet.json').read_text(encoding='utf-8-sig'))
    assert len(quiet) >= 5
    timestamp = lambda row: datetime.datetime.fromisoformat(row['Time'].replace('Z', '+00:00'))
    assert (timestamp(quiet[-1]) - timestamp(quiet[0])).total_seconds() >= 60
    assert 0 <= (timestamp(samples[0]) - timestamp(quiet[-1])).total_seconds() <= 30

    def check(baseline, rows, own_pid=None):
        prior = {p['ProcessId']: p for p in baseline['Processes']}
        for row in rows:
            elapsed = (timestamp(row) - timestamp(baseline)).total_seconds()
            assert elapsed >= 0
            combined_cpu = 0.0
            owned = {own_pid} if own_pid is not None else set()
            while True:
                descendants = {p['ProcessId'] for p in row['Processes'] if p['ParentProcessId'] in owned}
                if descendants <= owned:
                    break
                owned |= descendants
            for process in row['Processes']:
                if process['ProcessId'] == own_pid:
                    assert source.as_posix() in process['CommandLine'].replace('\\', '/')
                    continue
                if process['ProcessId'] in owned:
                    continue
                old = prior[process['ProcessId']]
                assert all(process[key] == old[key] for key in ('CreationTime', 'CommandLine'))
                delta = process['CpuSeconds'] - old['CpuSeconds']
                assert delta >= 0
                combined_cpu += delta
            assert combined_cpu <= 0.1 + 0.01 * elapsed

    check(quiet[0], quiet)
    for before, after in zip(quiet, quiet[1:]):
        check(before, [after])
    check(quiet[-1], samples, pid)
    for before, after in zip(samples, samples[1:]):
        check(before, [after], pid)
