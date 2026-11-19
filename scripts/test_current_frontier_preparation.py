"""Physical preparation→REAL input-verifier regressions; external SDK/auth are unit doubles.

No real SDK403, production build, indicator, timing or GPU execution is claimed
by this synthetic fixture. The separate retained-artifact smoke checks actual
accepted134/86/GPU100 bytes without replacing any verifier.
"""
from contextlib import contextmanager,ExitStack
import copy
import hashlib
import inspect
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

from hosted_timing import pipeline as p,current_frontier_refresh as q

PLAN=p.REPO/'benchmarks/plans/hosted-current-frontier-refresh-20261119.json'


class CurrentFrontierPreparationTests(unittest.TestCase):
    @contextmanager
    def fixture(self):
        with tempfile.TemporaryDirectory(prefix='frontier-prepare-unit-') as temporary,ExitStack() as stack:
            root=Path(temporary);candidate=root/'accepted';(candidate/'evidence').mkdir(parents=True);(candidate/'blobs').mkdir()
            digest=lambda raw:hashlib.sha256(raw).hexdigest()
            core=b'UNIT core bytes; never loaded';harness=b'UNIT harness bytes; never loaded';dependency=b'UNIT dependency bytes'
            prefix=p.BENCH+'/bin/Release/net10.0/'
            files={prefix+p.DLL:digest(core),prefix+q.ASSEMBLY:digest(harness),prefix+'dependency.dll':digest(dependency)}
            for raw in (core,harness,dependency):(candidate/'blobs'/digest(raw)).write_bytes(raw)
            checks={};classes={}
            for kind in ('unit','parity'):
                cls='Synthetic'+kind.title();classes[kind]=[cls]
                raw=('<TestRun><Results><UnitTestResult outcome="Passed" testName="'+cls+'.Case" /></Results></TestRun>').encode()
                path=candidate/'evidence'/(kind+'.trx');path.write_bytes(raw);checks[kind]=p.verify_trx(path,[cls])
            plan=copy.deepcopy(p.read(PLAN));plan.update(inputs_ready=True,parent_review_approved=True)
            # Scope and accepted-artifact authentication are external boundaries
            # in this UNIT fixture; every physical-file verifier runs for real.
            plan.pop('runtime_dependency_alignment')
            manifest=dict(schema=1,status='cpu-correctness-complete',sdk='10.0.403',source=q.CANONICAL,
                run_id=plan['candidate']['ci_run'],workflow_head=plan['candidate']['producer'],runtime_files=files,
                classes=classes,checks=checks,evidence={'evidence/'+kind+'.trx':p.digest(candidate/'evidence'/(kind+'.trx')) for kind in checks})
            p.write(candidate/'manifest.json',manifest)
            plan['candidate'].update(manifest_sha256=p.digest(candidate/'manifest.json'),production_sha256=digest(core),
                actual_harness_sha256=digest(harness),unit_classes=classes['unit'],parity_classes=classes['parity'],unit_passed=1,parity_passed=1)
            plan['cpu_test_classes']={label:classes['unit'] for label in ('old','successor','variants')}
            plan_file=root/'plan.json';p.write(plan_file,plan)
            gpu_receipt=q.evidence_entry(plan['candidate']['gpu_receipt']);gpu_trx=q.evidence_entry(plan['candidate']['gpu_trx'])
            auth=dict(manifest=manifest,production=digest(core),read=lambda name:(candidate/name).read_bytes(),
                closure={'packages':{'AiDotNet.Tensors':'0.151.1'}},gpu_receipt=gpu_receipt,gpu_trx=gpu_trx)
            ids=[dict(Id=row['pair'],Status='paired') for row in q.frozen_catalog()['rows']]
            def command(args,**kwargs):
                if args==['dotnet','--version']:value='10.0.403'  # SDK unit double, NOT a local SDK claim.
                elif args==['dotnet','--info']:value='UNIT external launcher double; no SDK installed or executed'
                elif args[0]=='git':
                    if args[1]=='ls-remote':value='f'*40+'\trefs/heads/perf/remaining-indicator-rollout\n'
                    elif args[1]=='rev-parse':value='a'*40
                    elif args[1]=='diff':value=''
                    else:raise AssertionError(args)
                elif args[0]=='dotnet' and '--list' in args:
                    value='\n'.join('OoplesFinance.StockIndicators.CompetitorBenchmarks.LibraryPairBenchmarks.'+method for method in ('Competitor','Ooples'))
                else:raise AssertionError('Unexpected external command: '+str(args))
                return value if kwargs.get('text') else value.encode()
            def run(args,**kwargs):
                self.assertEqual('dotnet',args[0]);self.assertEqual('--comparison-manifest',args[2])
                Path(args[3]).write_bytes((json.dumps(ids)+'\n').encode())
                return subprocess.CompletedProcess(args,0)
            stack.enter_context(patch.object(p,'PLAN',plan_file))
            stack.enter_context(patch.object(q,'CORE_SHA',digest(core)))
            stack.enter_context(patch.object(q,'HARNESS_SHA',digest(harness)))
            stack.enter_context(patch.object(q,'validate_scope'))
            stack.enter_context(patch.object(q,'checked_inputs',return_value=auth))
            stack.enter_context(patch.object(subprocess,'check_output',side_effect=command))
            stack.enter_context(patch.object(subprocess,'run',side_effect=run))
            stack.enter_context(patch.dict(os.environ,GITHUB_REF_NAME=q.PREPARATION_BRANCH,GITHUB_RUN_ID='12345',GITHUB_RUN_ATTEMPT='1'))
            # Deliberately do NOT patch p.verify_inputs or _verify_input_contents.
            yield root,candidate,plan

    def test_prepare_materializes_files_and_traverses_real_verifier(self):
        with self.fixture() as (root,candidate,plan):
            out=root/'prepared';proof=q.prepare(candidate,out)
            self.assertEqual(proof,p.verify_inputs(out))
            self.assertEqual(q.PREPARATION_BRANCH,proof['workflow_branch'])
            self.assertEqual(q.PREPARATION_REVISION,proof['preparation_revision'])
            self.assertFalse((out/'evidence/variant-harness.trx').exists())
            self.assertEqual(proof['common_manifest'],p.manifest(out/'common'))

    def test_removing_explicit_route_reproduces_legacy_variant_harness_failure(self):
        source=inspect.getsource(p._verify_input_contents)
        selected="if plan.get('current_frontier_refresh'):"
        self.assertEqual(1,source.count(selected))
        namespace=dict(vars(p));exec(compile(source.replace(selected,'if False:',1),'removed-frontier-alias-route','exec'),namespace)
        with self.fixture() as (root,candidate,plan),patch.object(p,'_verify_input_contents',namespace['_verify_input_contents']):
            with self.assertRaises(FileNotFoundError) as failure:q.prepare(candidate,root/'missing-route')
            self.assertIn('variant-harness.trx',str(failure.exception))

    def test_alias_rejects_changed_unit_parity_gpu_core_dependency_and_manifest(self):
        with self.fixture() as (root,candidate,plan):
            out=root/'prepared';proof=q.prepare(candidate,out)
            paths=['evidence/variants.trx','evidence/variants-parity.trx','evidence/variants-original-gpu-receipt.json',
                'evidence/variants-original-gpu-gpu.trx.gz','production/variants.dll','common/dependency.dll','baseline-authentication/manifest.json']
            for relative in paths:
                path=out/relative;original=path.read_bytes();path.write_bytes(original+b'changed')
                with self.subTest(relative=relative),self.assertRaises(AssertionError):q.verify_harness_alias(out,plan,proof)
                path.write_bytes(original)
            for field,value in [('workflow_branch','perf/current-frontier-refresh-20261119'),('preparation_revision','old-revision')]:
                wrong=copy.deepcopy(proof);wrong[field]=value
                with self.subTest(field=field),self.assertRaises(AssertionError):q.verify_harness_alias(out,plan,wrong)


if __name__=='__main__':unittest.main()
