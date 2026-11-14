from pathlib import Path
import psutil,time

def capture_children(process, known):
    try:
        for child in psutil.Process(process.pid).children(recursive=True):
            known[(child.pid, child.create_time())]=child
    except psutil.NoSuchProcess:
        pass

def stop_owned_benchmark(process, known):
    capture_children(process, known)
    # Stop the root first so it cannot create another worker after enumeration.
    if process.poll() is None:
        process.kill()
    children=list(known.values())
    for child in children:
        try:child.kill()
        except psutil.NoSuchProcess:pass
    process.wait(timeout=10)
    _, alive=psutil.wait_procs(children,timeout=10)
    if alive:raise RuntimeError(('Owned benchmark children did not exit',[p.pid for p in alive]))

def archive_with_retry(source,target,root,timeout=30):
    source,target,root=Path(source).resolve(),Path(target).resolve(),Path(root).resolve()
    assert source.is_relative_to(root) and target.is_relative_to(root)
    assert source!=root and target!=root and not target.exists()
    deadline=time.monotonic()+timeout
    while True:
        try:source.rename(target);return
        except PermissionError:
            if time.monotonic()>=deadline:raise
            time.sleep(.25)
