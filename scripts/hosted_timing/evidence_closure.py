"""Authenticate transitive repository evidence from git blobs, never local files."""
import gzip
import hashlib
import json
import subprocess
from pathlib import PurePosixPath

def audit(repo,plan_path,*,ref='HEAD'):
    assert ref in ('HEAD','INDEX'),'Use an explicit current tree or staged index'
    visited={};external=[]
    def read(path):
        parts=PurePosixPath(path).parts
        assert parts and not path.startswith('/') and '\\' not in path and ':' not in path
        assert all(p not in ('','.','..') for p in parts)
        name=(':'+path) if ref=='INDEX' else ('HEAD:'+path)
        result=subprocess.run(['git','show',name],cwd=repo,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
        assert result.returncode==0,'Uncommitted or missing '+ref+' evidence: '+path
        return result.stdout
    def reference(path,pin):
        if PurePosixPath(path).is_absolute() or ':' in path or '\\' in path:
            # Historical origin paths are provenance; runtime evidence reads only
            # the separately pinned, portable repository copies.
            external.append(dict(path=path,sha256=pin));return
        assert isinstance(pin,str) and len(pin)==64 and all(c in '0123456789abcdef' for c in pin)
        if path in visited:
            assert visited[path]==pin,'Conflicting transitive evidence pins: '+path
            return
        raw=read(path);assert hashlib.sha256(raw).hexdigest()==pin,'Git blob differs from evidence pin: '+path
        visited[path]=pin
        decode(path,raw)
    def walk(value):
        if isinstance(value,dict):
            if isinstance(value.get('path'),str) and isinstance(value.get('sha256'),str):reference(value['path'],value['sha256'])
            for key,item in value.items():
                if key in ('runner_source_pins','source_catalog_sha256'):
                    assert isinstance(item,dict)
                    for path,pin in item.items():reference(path,pin)
                walk(item)
        elif isinstance(value,list):
            for item in value:walk(item)
    def decode(path,raw):
        if path.endswith('.json.gz'):walk(json.loads(gzip.decompress(raw)))
        elif path.endswith('.json'):walk(json.loads(raw))
    root=read(plan_path);visited[plan_path]=hashlib.sha256(root).hexdigest();decode(plan_path,root)
    return dict(status='committed-transitive-evidence-closure-passed',ref=ref,
        plan_sha256=visited[plan_path],files=visited,absolute_origin_references=external)
