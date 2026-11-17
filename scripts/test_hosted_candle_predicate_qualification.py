"""Pure bounded-scope/identity/runtime fixtures; no builds or measurements."""
import copy
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock
from hosted_timing import pipeline as p
from hosted_timing import candle_predicate_qualification as q


class PredicateQualificationTests(unittest.TestCase):
    def plan(self):
        old=p.read(p.REPO/q.OLD_PLAN)
        plan=copy.deepcopy(old)
        for key in ('matchinglow_grid_qualification','native_crossover_gate','baseline_continuation','preflight'):
            plan.pop(key,None)
        plan.update(candle_predicate_order_qualification=True,inputs_ready=False,
            sources=dict(published=q.OLD,old=q.OLD,successor=q.NEW,variants=q.VARIANTS),
            gpu_sources=dict(old=q.OLD_COMPILED,successor=q.NEW,variants=q.VARIANTS),
            cases=180,decisions=36,matrix=dict(shards=1,max_parallel=1,timeout_minutes_per_shard=180),
            targeted_shards=q.shards(),native_progress_gate=copy.deepcopy(q.NATIVE_POLICY),old_candidate=old['candidate'],
            cpu_test_classes=dict(old=q.previous.UNIT,successor=q.UNIT,variants=['OwnedHistoryBatchTests']),
            scope_approval=dict(path='fixture-only-approval.json',sha256='1'*64))
        plan['targeted_reference'].update(scope=q.SCOPE,decision=q.REFERENCE,run_id='43992359476',qualified_source=q.OLD_COMPILED)
        plan['candidate'].update(source=q.NEW,unit_classes=q.UNIT,parity_classes=q.PARITY,unit_passed=24,parity_passed=65,
            ci_run='44005551788',producer='2a1737f30e5e1164189d0209b39ea46bf38c1239',
            evidence_source='f63b014560a4978b7b47755fcd73f5d39c531e12',
            manifest_sha256='9a211abdc551bfa6f0b900ef17e5f305ea417d8546f9f3ba2ec8d9c643391156',
            gpu_receipt_sha256='4f0abd4a553fe0ab1985e1b8faa610d4c680a84084e199d5ae27600ad1366eb9',
            production_sha256='8bac548a15cf862067fffcaab85b0abbabadd2964948ceb0d160db29c3f85f52')
        return plan

    def validate(self,plan,ready=False):
        original_read,original_digest=p.read,p.digest
        approval=(p.REPO/'fixture-only-approval.json').resolve()
        with mock.patch.object(p,'read',side_effect=lambda path:dict(approved=True,scope=q.SCOPE,cases=180)
                if Path(path).resolve()==approval else original_read(path)),\
             mock.patch.object(p,'digest',side_effect=lambda path:'1'*64 if Path(path).resolve()==approval else original_digest(path)):
            q.validate_scope(plan,require_ready=ready)

    def test_exact_six_pair_tasks_and_standard_policy(self):
        plan=self.plan();self.validate(plan)
        self.assertEqual((180,36),(sum(s['cases'] for s in q.shards()),sum(s['decisions'] for s in q.shards())))
        penetration=q.shards()[0]['tasks'][2]
        self.assertEqual('SharedTaReversalContinuationBenchmarks',penetration['fixture'])
        self.assertEqual(['PiercingLine','DarkCloudCover'],[x['raw'] for x in penetration['operations']])
        self.assertEqual(q.METHODS,penetration['methods'])

    def test_qualified_classes_are_allowlisted_only_for_this_scope(self):
        plan=self.plan()
        for label,expected in [('old',q.previous.UNIT),('successor',q.UNIT),('variants',['OwnedHistoryBatchTests'])]:
            self.assertEqual(expected,p.cpu_test_classes(plan,label))
        for kind in ('extra-class','other-scope','no-scope'):
            bad=copy.deepcopy(plan)
            if kind=='extra-class':bad['cpu_test_classes']['successor'].append('Other.Full.Name')
            elif kind=='other-scope':bad['targeted_reference']['scope']='other'
            else:bad.pop('candle_predicate_order_qualification')
            with self.assertRaises(AssertionError):p.cpu_test_classes(bad,'successor')

    def test_ready_requires_actual_ci_gpu_identities(self):
        plan=self.plan()
        with self.assertRaises(AssertionError):self.validate(plan,ready=True)
        plan['inputs_ready']=True;self.validate(plan,ready=True)
        plan['candidate']['gpu_receipt_sha256']='pending'
        with self.assertRaises(AssertionError):self.validate(plan,ready=True)

    def test_old_compiled_core_cannot_be_replaced_by_original_frozen_baseline(self):
        for kind in ('gpu-source','core','class-count'):
            plan=self.plan()
            if kind=='gpu-source':plan['gpu_sources']['old']=q.previous.CANONICAL
            elif kind=='core':plan['old_candidate']['production_sha256']=q.previous.CANONICAL_DLL
            else:plan['candidate']['unit_passed']=20
            with self.assertRaises(AssertionError):self.validate(plan)

    def test_runtime_guard_checks_original_operation_reports_not_only_merged(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder)/'shard0';root.mkdir()
            p.write(root/'provenance.json',dict(raw_root=r'C:\raw'))
            p.write(root/'status.json',dict(rounds=[dict(report=r'C:\raw\merged.json',
                operation_checkpoints=[dict(report=r'C:\raw\original.json')])]))
            for name in ('merged.json','original.json'):
                p.write(root/name,dict(HostEnvironmentInfo=dict(RuntimeVersion='.NET 10.0.15 (10.0.15)')))
            q.verify_runtime(Path(folder),'.NET 10.0.15')
            p.write(root/'original.json',dict(HostEnvironmentInfo=dict(RuntimeVersion='.NET 10.0.14')))
            with self.assertRaises(AssertionError):q.verify_runtime(Path(folder),'.NET 10.0.15')

    def test_matchinglow_frozen_module_and_plan_remain_byte_identical(self):
        for name in ('scripts/hosted_timing/matchinglow_grid_qualification.py',q.OLD_PLAN):
            expected=p.subprocess.check_output(['git','show','fab5e0179feee36f3e0d9205dd0323d1b1e9c2f7:'+name],cwd=p.REPO)
            self.assertEqual(expected,(p.REPO/name).read_bytes().replace(b'\r\n',b'\n'))


def native_fixture():
    def value(mean):return dict(mean_ns=mean,lower_ns=mean-2,upper_ns=mean+2)
    rows=[dict(pair=pair,bars=bars,old_rounds={'0':value(150),'3':value(150)},
        candidate_rounds={'1':value(140),'2':value(140)},
        native_control=dict(mean_ns=100,lower_ns=95,upper_ns=105)) for pair in q.PAIRS for bars in (1000,10000)]
    target=next(row for row in rows if row['pair']==q.PAIRS[0] and row['bars']==10000)
    target['candidate_rounds']={'1':value(70),'2':value(70)}
    decisions=[dict(fixture='LibraryPairBenchmarks',method='Ooples',pair=pair,bars=bars,
        verdict='faster' if (pair,bars)==(q.PAIRS[0],10000) else 'inconclusive') for pair in q.PAIRS for bars in (1000,10000)]
    return rows,decisions


class NativeProgressTests(unittest.TestCase):
    def test_ci_margin_and_own_library_source_faster_are_required(self):
        rows,decisions=native_fixture();result=q.adjudicate_native(rows,decisions)
        self.assertTrue(result['passed']);self.assertEqual(result,json.loads(json.dumps(result,allow_nan=False)))
        for kind in ('ci-equality','margin','wrong-source-faster'):
            r,d=copy.deepcopy(rows),copy.deepcopy(decisions)
            target=next(row for row in r if (row['pair'],row['bars'])==(q.PAIRS[0],10000))
            if kind=='ci-equality':target['candidate_rounds']['1']['upper_ns']=95
            elif kind=='margin':target['candidate_rounds']['1'].update(mean_ns=90.0001,lower_ns=89,upper_ns=94)
            else:
                for decision in d:decision['verdict']='inconclusive'
                d.append(dict(fixture='SharedTripleBodyBenchmarks',method='BuilderFull',pair=q.PAIRS[0],bars=10000,verdict='faster'))
                next(item for item in d if item['fixture']=='LibraryPairBenchmarks' and item['pair']==q.PAIRS[1] and item['bars']==10000)['verdict']='faster'
            self.assertFalse(q.adjudicate_native(r,d)['passed'],kind)
        target=next(row for row in rows if (row['pair'],row['bars'])==(q.PAIRS[0],10000))
        target['candidate_rounds']={key:dict(mean_ns=90,lower_ns=88,upper_ns=94) for key in ('1','2')}
        self.assertTrue(q.adjudicate_native(rows,decisions)['passed'])

    def test_net_progress_uses_worst_new_and_best_old_round_counts(self):
        rows,decisions=native_fixture()
        other=next(row for row in rows if (row['pair'],row['bars'])==(q.PAIRS[1],10000))
        other['old_rounds']['0']=dict(mean_ns=80,lower_ns=78,upper_ns=82)
        other['candidate_rounds']['1']=dict(mean_ns=70,lower_ns=68,upper_ns=72)
        result=q.adjudicate_native(rows,decisions)
        self.assertEqual(1,len(result['new_10000_closures']))
        self.assertEqual({'0':1,'3':0},result['old_10000_point_win_counts'])
        self.assertEqual({'1':2,'2':1},result['candidate_10000_point_win_counts'])
        self.assertFalse(result['net_10000_point_progress']);self.assertFalse(result['passed'])

    def test_prior_confident_native_wins_are_protected_at_both_sizes(self):
        for bars in (1000,10000):
            with self.subTest(bars=bars):
                rows,decisions=native_fixture()
                protected=next(row for row in rows if (row['pair'],row['bars'])==(q.PAIRS[1],bars))
                protected['old_rounds']={key:dict(mean_ns=80,lower_ns=78,upper_ns=82) for key in ('0','3')}
                protected['candidate_rounds']={'1':dict(mean_ns=100,lower_ns=98,upper_ns=102),'2':dict(mean_ns=80,lower_ns=78,upper_ns=82)}
                if bars==10000:
                    extra=next(row for row in rows if (row['pair'],row['bars'])==(q.PAIRS[2],10000))
                    extra['candidate_rounds']={key:dict(mean_ns=70,lower_ns=68,upper_ns=72) for key in ('1','2')}
                    next(d for d in decisions if (d['pair'],d['bars'])==(q.PAIRS[2],10000))['verdict']='faster'
                result=q.adjudicate_native(rows,decisions)
                self.assertTrue(result['net_10000_point_progress'])
                self.assertEqual([dict(pair=q.PAIRS[1],bars=bars,preserved=False)],result['prior_confident_win_reopenings'])
                self.assertFalse(result['passed'])

    def test_only_ten_thousand_new_closures_count_and_ties_are_nonwins(self):
        rows,decisions=native_fixture();target=next(row for row in rows if (row['pair'],row['bars'])==(q.PAIRS[0],10000))
        target['old_rounds']={key:dict(mean_ns=100,lower_ns=98,upper_ns=102) for key in ('0','3')}
        self.assertTrue(q.adjudicate_native(rows,decisions)['passed'])
        target['old_rounds']['0']=dict(mean_ns=99,lower_ns=97,upper_ns=101)
        self.assertFalse(q.adjudicate_native(rows,decisions)['new_10000_closures'])
        rows,decisions=native_fixture();target=next(row for row in rows if (row['pair'],row['bars'])==(q.PAIRS[0],10000))
        target['candidate_rounds']={key:dict(mean_ns=100,lower_ns=98,upper_ns=102) for key in ('1','2')}
        result=q.adjudicate_native(rows,decisions)
        self.assertEqual({'1':0,'2':0},result['candidate_10000_point_win_counts']);self.assertFalse(result['passed'])
        one=next(row for row in rows if (row['pair'],row['bars'])==(q.PAIRS[0],1000))
        one['candidate_rounds']={key:dict(mean_ns=70,lower_ns=68,upper_ns=72) for key in ('1','2')}
        next(d for d in decisions if (d['pair'],d['bars'])==(q.PAIRS[0],1000))['verdict']='faster'
        self.assertFalse(q.adjudicate_native(rows,decisions)['passed'])

    def test_native_guard_is_mandatory_and_standard_acceptance_is_separate(self):
        from hosted_timing import verify
        with mock.patch.object(verify,'read',return_value=dict(candle_predicate_order_qualification=True)):
            with self.assertRaises(AssertionError):verify._collect(Path('unused'),'targeted',preflight=False)
        rows,decisions=native_fixture()
        for source_accepted,native_passed in ((False,True),(True,False)):
            r=copy.deepcopy(rows)
            if not native_passed:
                next(row for row in r if (row['pair'],row['bars'])==(q.PAIRS[0],10000))['candidate_rounds']['1']['upper_ns']=95
            standard=dict(case_count=180,decision_count=36,accepted=source_accepted,decisions=decisions,
                verdicts=dict(faster=1,slower=0 if source_accepted else 1))
            with (mock.patch.object(verify,'_collect',return_value=copy.deepcopy(standard)) as original,
                  mock.patch.object(q,'native_rows',return_value=r),
                  mock.patch.object(q.p,'read',return_value=PredicateQualificationTests().plan())):
                result=q.collect(Path('unused'))
            original.assert_called_once_with(Path('unused'),'targeted',preflight=False,candle_predicate_gate=True)
            self.assertEqual(source_accepted,result['source_gate_accepted'])
            self.assertEqual(native_passed,result['native_progress_gate_passed']);self.assertFalse(result['accepted'])
            self.assertEqual(standard['verdicts'],result['verdicts'])

    def test_parser_uses_only_round_two_library_native_and_string_round_keys(self):
        from hosted_timing import verify,checkpoints
        rows,_=native_fixture();lookup={(row['pair'],row['bars']):row for row in rows}
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder)/'shard0';root.mkdir();p.write(root/'provenance.json',dict(raw_root='C:/raw'))
            records=[]
            for index in range(4):
                cases=[]
                for pair in q.PAIRS:
                    for bars in (1000,10000):
                        row=lookup[pair,bars];values=row['old_rounds'] if index in (0,3) else row['candidate_rounds']
                        cases.append(dict(key=[pair,bars,'Ooples'],stats=values[str(index)]))
                        if index==2:cases.append(dict(key=[pair,bars,'Competitor'],stats=row['native_control']))
                p.write(root/(str(index)+'.json'),dict(Benchmarks=cases))
                records.append(dict(fixture='LibraryPairBenchmarks',index=index,report='C:/raw/'+str(index)+'.json'))
            records.append(dict(fixture='SharedTripleBodyBenchmarks',index=2,report='C:/raw/never-use-builder-control.json'))
            p.write(root/'status.json',dict(rounds=records))
            with (mock.patch.object(checkpoints,'case_key',side_effect=lambda case:tuple(case['key'])),
                  mock.patch.object(verify,'sample',side_effect=lambda case:case['stats'])):
                parsed=q.native_rows(Path(folder));self.assertEqual(rows,parsed)
                report=p.read(root/'0.json');report['Benchmarks'].append(dict(key=[q.PAIRS[0],1000,'Competitor'],stats=rows[0]['native_control']))
                p.write(root/'0.json',report)
                with self.assertRaises(AssertionError):q.native_rows(Path(folder))

    def test_exact_native_policy_cannot_be_relaxed(self):
        helper=PredicateQualificationTests()
        for key,value in [('minimum_margin',0),('new_closure_bars',1000),('require_own_source_faster',False),('protect_prior_confident_wins_both_sizes',False)]:
            plan=helper.plan();plan['native_progress_gate'][key]=value
            with self.assertRaises(AssertionError):helper.validate(plan)


class BreakawayReuseTests(unittest.TestCase):
    def plan(self):
        return p.read(p.REPO/'benchmarks/plans/hosted-breakaway-predicate-order-20261116.json')

    def test_exact_three_consumer_matrix_and_ready_identity(self):
        from hosted_timing import targeted
        plan=self.plan();q.validate_scope(plan)
        scope=q._scope(plan)
        self.assertEqual((90,18),(scope.cases,scope.decisions))
        tasks=plan['targeted_shards'][0]['tasks']
        self.assertEqual(['LibraryPairBenchmarks','SharedTaMultiBarNativeBenchmarks'],[t['fixture'] for t in tasks])
        self.assertEqual(['Breakaway','ConcealingBabySwallow','LadderBottom'],[op['raw'] for op in tasks[1]['operations']])
        self.assertEqual(q.METHODS,tasks[1]['methods'])
        self.assertEqual((60,12),(tasks[1]['cases'],tasks[1]['decisions']))
        self.assertEqual(list(scope.pairs),[op['pair'] for op in tasks[1]['operations']])
        self.assertEqual(list(scope.unit),p.cpu_test_classes(plan,'successor'))
        self.assertEqual(scope.paths,targeted.SCOPES[scope.name]['paths'])
        self.assertEqual((90,18),targeted.expected_totals(plan))
        self.assertEqual(p.read(p.REPO/'benchmarks/plans/hosted-candle-predicate-order-20261116.json')['measurement_policy'],plan['measurement_policy'])
        self.assertEqual({},plan['runtime_policy_overrides'])
        self.assertEqual(q.NATIVE_POLICY,plan['native_progress_gate'])

    def test_unknown_and_cross_scope_substitution_reject_before_io(self):
        six=PredicateQualificationTests().plan();three=self.plan()
        for original,other in ((six,three),(three,six)):
            for key in ('source','unit_classes','parity_classes','unit_passed','parity_passed','ci_run',
                        'evidence_source','manifest_sha256','gpu_receipt_sha256','production_sha256'):
                bad=copy.deepcopy(original);bad['candidate'][key]=copy.deepcopy(other['candidate'][key])
                with self.subTest(scope=original['targeted_reference']['scope'],field=key):
                    with mock.patch.object(p,'digest') as digest, mock.patch.object(p,'read') as read:
                        with self.assertRaises(AssertionError):q.candidate_inputs(Path('must-not-be-opened'),bad)
                        digest.assert_not_called();read.assert_not_called()
        bad=copy.deepcopy(three);bad['targeted_reference']['scope']='unreviewed-scope'
        with mock.patch.object(p,'digest') as digest, mock.patch.object(p,'read') as read:
            with self.assertRaises(AssertionError):q.candidate_inputs(Path('must-not-be-opened'),bad)
            digest.assert_not_called();read.assert_not_called()

    def test_six_scope_defaults_and_archive_note_are_preserved(self):
        import ast
        import re
        six=p.read(p.REPO/'benchmarks/plans/hosted-candle-predicate-order-20261116.json')
        self.assertEqual(six['targeted_shards'],q.shards())
        self.assertEqual(q.shards(),q.shards(scope=q.SIX_SCOPE))
        rows,decisions=native_fixture()
        self.assertEqual(q.adjudicate_native(rows,decisions),q.adjudicate_native(rows,decisions,pairs=q.SIX_SCOPE.pairs))
        original=p.subprocess.check_output(['git','show','938ab3b76e9552c4460e939d6cb968051291150a:scripts/hosted_timing/archive.py'],cwd=p.REPO,text=True)
        note=re.search(r"note=\(('Fresh 180-case[^\n]+?') if read",original).group(1)
        self.assertEqual(ast.literal_eval(note),q.archive_note(six))
        note=q.archive_note(self.plan())
        for expected in ('90-case','18-decision','65a157398 13/91/GPU100','frozen 9a'):
            self.assertIn(expected,note)
        self.assertNotIn('180-case',note);self.assertNotIn('new caef',note)

    def test_three_pair_native_gate_keeps_strict_requirements(self):
        scope=q.BREAKAWAY_SCOPE
        rows,decisions=native_fixture()
        mapping=dict(zip(q.PAIRS,scope.pairs))
        rows=[dict(row,pair=mapping[row['pair']]) for row in rows if row['pair'] in mapping]
        decisions=[dict(row,pair=mapping[row['pair']]) for row in decisions if row['pair'] in mapping]
        result=q.adjudicate_native(rows,decisions,pairs=scope.pairs)
        self.assertTrue(result['passed']);self.assertEqual(6,len(result['rows']))
        self.assertEqual(q.NATIVE_POLICY,result['policy'])
        next(row for row in rows if row['bars']==10000)['candidate_rounds']['1']['upper_ns']=95
        self.assertFalse(q.adjudicate_native(rows,decisions,pairs=scope.pairs)['passed'])
        with self.assertRaises(AssertionError):q.adjudicate_native(rows,decisions)

    def test_three_pair_collection_retains_both_gate_results(self):
        from hosted_timing import verify
        scope=q.BREAKAWAY_SCOPE
        rows,decisions=native_fixture();mapping=dict(zip(q.PAIRS,scope.pairs))
        rows=[dict(row,pair=mapping[row['pair']]) for row in rows if row['pair'] in mapping]
        decisions=[dict(row,pair=mapping[row['pair']]) for row in decisions if row['pair'] in mapping]
        standard=dict(case_count=90,decision_count=18,accepted=True,decisions=decisions,verdicts=dict(faster=1))
        with (mock.patch.object(verify,'_collect',return_value=copy.deepcopy(standard)),
              mock.patch.object(q,'native_rows',return_value=rows) as native,
              mock.patch.object(q.p,'read',return_value=self.plan())):
            result=q.collect(Path('unused'))
        native.assert_called_once_with(Path('unused'),pairs=scope.pairs)
        self.assertTrue(result['source_gate_accepted']);self.assertTrue(result['native_progress_gate_passed'])
        self.assertTrue(result['accepted'])


class GapSideReadyScopeTests(unittest.TestCase):
    def plan(self):
        return p.read(p.REPO/'benchmarks/plans/hosted-gapside-predicate-order-20261117.json')

    def test_exact_matched_lines_scope_binds_actual_receipts(self):
        from hosted_timing import targeted
        plan=self.plan();q.validate_scope(plan)
        scope=q._scope(plan)
        self.assertEqual(q.GAPSIDE_SCOPE,scope)
        self.assertEqual(['MatchedLinesSharedExecutionTests'],plan['candidate']['unit_classes'])
        self.assertEqual(['MatchedLinesTests','SharedMatchedLinesBenchmarkTests'],plan['candidate']['parity_classes'])
        self.assertEqual('44027927822',scope.ci_run)
        tasks=plan['targeted_shards'][0]['tasks']
        self.assertEqual(['LibraryPairBenchmarks','SharedMatchedLinesBenchmarks'],[task['fixture'] for task in tasks])
        self.assertEqual(['TaLib.Candles.GapSideBySideWhiteLines','TaLib.Candles.Counterattack','TaLib.Candles.SeparatingLines'],[op['raw'] for op in tasks[1]['operations']])
        self.assertEqual(q.METHODS,tasks[1]['methods'])
        self.assertEqual((90,18),targeted.expected_totals(plan))
        self.assertEqual(scope.paths,targeted.SCOPES[scope.name]['paths'])
        self.assertEqual(q.NATIVE_POLICY,plan['native_progress_gate'])
        self.assertTrue(plan['inputs_ready'])
        self.assertEqual((12,76),(plan['candidate']['unit_passed'],plan['candidate']['parity_passed']))
        self.assertEqual('591e16719b627d450adf10107af44d212f7cf600',plan['candidate']['evidence_source'])
        self.assertEqual('912e359bf07a0ddfb26a48e7f4c4e994c7a07beecf311c3ceb667da49340aea2',plan['candidate']['production_sha256'])
        self.assertIn('1637b717e 12/76/GPU100',q.archive_note(plan))

    def test_unbound_or_not_ready_correctness_still_refuses_runtime_access(self):
        for key in ('inputs_ready','evidence_source','manifest_sha256','gpu_receipt_sha256','production_sha256','unit_passed','parity_passed'):
            plan=self.plan()
            if key=='inputs_ready':plan[key]=False
            else:plan['candidate'][key]=None
            with self.subTest(field=key):
                with mock.patch.object(p,'digest') as digest, mock.patch.object(p,'read') as read:
                    with self.assertRaises(AssertionError):q.candidate_inputs(Path('must-not-be-opened'),plan)
                    digest.assert_not_called();read.assert_not_called()

    def test_cross_scope_classes_and_proofs_reject_before_io(self):
        for other in (q.SIX_SCOPE,q.BREAKAWAY_SCOPE):
            for key,value in q._candidate_identity(other).items():
                plan=self.plan()
                if plan['candidate'][key] == value:continue
                plan['candidate'][key]=value
                with self.subTest(other=other.name,field=key):
                    with mock.patch.object(p,'digest') as digest, mock.patch.object(p,'read') as read:
                        with self.assertRaises(AssertionError):q.validate_scope(plan,require_ready=False)
                        digest.assert_not_called();read.assert_not_called()


if __name__=='__main__':unittest.main()
