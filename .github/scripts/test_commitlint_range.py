"""Prove the installed CLI includes both ends of a PR range longer than 100 commits."""
import argparse
import os
import subprocess
import tempfile
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('--cli', required=True, type=Path)
p.add_argument('--config', required=True, type=Path)
a = p.parse_args()
cli, config = a.cli.resolve(), a.config.resolve()
env = os.environ.copy()
env.update(GIT_AUTHOR_NAME='Range Test', GIT_COMMITTER_NAME='Range Test',
           GIT_AUTHOR_EMAIL='range@example.invalid', GIT_COMMITTER_EMAIL='range@example.invalid')

with tempfile.TemporaryDirectory(prefix='commitlint-range-') as folder:
    def git(*args, data=None):
        return subprocess.check_output(['git', *args], cwd=folder, env=env, input=data).decode().strip()

    git('init', '--quiet')
    tree = git('mktree', data=b'')
    base = git('-c', 'commit.gpgsign=false', 'commit-tree', tree, data=b'excluded-base-marker\n')
    parent = base
    commits = []
    for index in range(106):
        message = ('old-range-marker' if index == 0 else
                   'new-range-marker' if index == 105 else f'chore: range fixture {index}')
        parent = git('-c', 'commit.gpgsign=false', 'commit-tree', tree, '-p', parent,
                     data=(message + '\n').encode())
        commits.append(parent)
    git('update-ref', 'refs/heads/range-fixture', parent)

    def lint(start, end):
        return subprocess.run(['node', str(cli), '--config', str(config), '--from', start,
                               '--to', end, '--verbose'], cwd=folder, text=True,
                              stdout=subprocess.PIPE, stderr=subprocess.STDOUT)

    full = lint(base, commits[-1])
    assert full.returncode == 1, full.stdout
    assert 'old-range-marker' in full.stdout and 'new-range-marker' in full.stdout
    assert 'excluded-base-marker' not in full.stdout
    valid = lint(commits[0], commits[-2])
    assert valid.returncode == 0, valid.stdout
    early = lint(base, commits[-2])
    assert early.returncode == 1 and 'old-range-marker' in early.stdout, early.stdout
    assert 'new-range-marker' not in early.stdout
    print('3 range checks passed: 106-commit full range, excluded base, exact endpoints')
