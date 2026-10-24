import hashlib
import json
import sys
from pathlib import Path

snapshot = Path(sys.argv[1]).resolve()
manifest = json.loads((snapshot / 'manifest.json').read_text())
for name, digest in manifest['files_sha256'].items():
    path = (snapshot / name).resolve()
    assert path.is_relative_to(snapshot)
    assert hashlib.sha256(path.read_bytes()).hexdigest() == digest, name
print('Verified preserved candidate', manifest['commit'])
