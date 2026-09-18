import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import {validatePositionCapture} from './Read-LmaxDemoPositionReports.mjs';

// Synthetic contract tests only. These objects are never operational evidence.
function fixture() {
  const hash='a'.repeat(64),i={reportDate:'2026-09-18',startedUtc:'2026-09-18T12:30:00Z',snapshotRoot:'D:\\unit-test-only\\snapshot'};
  const attempt={Stable:true};
  for(const label of ['T0','P1','T1','P2','T2'])attempt[label]={RawSha256:hash,Artifact:{path:path.win32.join(i.snapshotRoot,'attempt-1',label+'.csv'),sha256:hash}};
  const c={ContractVersion:'lmax_portal_bracketed_current_position_snapshot_v2',AccountId:'1754288005',Environment:'LMAX_LONDON_DEMO',SessionMode:'manual-session',
    NoOrder:true,NoFix:true,NoDatabaseWrite:true,AsOfLowerBoundUtc:'2026-09-18T12:30:01Z',AsOfUpperBoundUtc:'2026-09-18T12:30:04Z',
    StableExecutionSet:true,StablePositionSet:true,BrokerDateSequenceStatus:'MONOTONIC_NON_DECREASING',
    TimeAuthorityMode:'BRACKETED_CURRENT_SNAPSHOT_STABLE_EXECUTION_SET',ExplicitTimeZoneStatus:'UNPROVEN',
    OpenPositionsSnapshotSemanticDecision:{CurrentSnapshotStatus:'PROVEN_CURRENT_BRACKETED_SNAPSHOT'},
    ComplementaryAccountEvidence:{SelectedAccountId:'1754288005',AccountRowsObserved:1,AccountRowsMatched:1},PositionCount:0,ExecutionCount:0,Attempts:[attempt]};
  const m={account_id:'1754288005',report_date_from:i.reportDate,report_date_to:i.reportDate,
    acquisition_method:'LMAX_PORTAL_BROWSER_AUTHENTICATED_REPORT_FORM',portal_origin:'https://account.london-demo.lmax.com',
    execute_portal_download:true,navigation_safety_status:'BRACKETED_REPORT_SNAPSHOT_COMPLETED',
    raw_endpoint_fallback_used:false,credentials_recorded:false,secret_values_recorded:false,totp_recorded:false,
    flow_capture_contains_headers:false,flow_capture_contains_cookies:false,flow_capture_contains_credentials:false,
    safety:{order_entry_enabled:false,operational_orders:false,production_live:false,trading_readiness:false,lmax_order_entry_used:false,lmax_fix_order_entry_used:false,lmax_accountapi_used:false},
    authentication_proof:{method:'VALID_ACCOUNT_SCOPED_REPORT_RESPONSE',account_id:'1754288005',http_status:200},bracketed_snapshot:c,
    bracketed_contract_artifact:{path:path.win32.join(i.snapshotRoot,'lmax-portal-bracketed-current-position-snapshot-v2.json'),sha256:hash}};
  const options={now:new Date('2026-09-18T12:30:10Z'),hashFile:()=>hash,readJson:()=>c};
  return {m,i,c,options};
}
test('validated empty position reports keep broker interval and cannot attest working orders or authorize trading',()=>{
  const {m,i,options}=fixture(),r=validatePositionCapture(m,i,options);
  assert.equal(r.position_count,0);assert.equal(r.working_orders_established,false);assert.equal(r.trading_authorized,false);
  assert.equal(r.report_timezone_status,'UNPROVEN');assert.equal(r.broker_capture_interval_utc.from,'2026-09-18T12:30:01Z');
});
test('wrong account/day and reports without authenticated same-account complementary rows fail closed',()=>{
  for(const mutate of [x=>x.m.account_id='other',x=>x.i.reportDate='2026-09-17',x=>x.m.authentication_proof=null,
    x=>x.c.ComplementaryAccountEvidence.AccountRowsObserved=0]) {
    const f=fixture();mutate(f);assert.throws(()=>validatePositionCapture(f.m,f.i,f.options));
  }
});
test('stale, future, prior acquisition and overlong broker intervals are rejected',()=>{
  for(const mutate of [x=>x.options.now=new Date('2026-09-18T12:46:00Z'),x=>x.c.AsOfUpperBoundUtc='2026-09-18T12:30:11Z',
    x=>x.c.AsOfLowerBoundUtc='2026-09-18T12:29:00Z',x=>{x.c.AsOfUpperBoundUtc='2026-09-18T12:31:00Z';x.options.now=new Date('2026-09-18T12:31:01Z');}]) {
    const f=fixture();mutate(f);assert.throws(()=>validatePositionCapture(f.m,f.i,f.options),/TIME_INVALID/);
  }
});
test('changed artifacts, escaped artifact paths and changed contract are rejected',()=>{
  for(const mutate of [x=>x.c.Attempts[0].P2.Artifact.sha256='b'.repeat(64),x=>x.c.Attempts[0].P1.Artifact.path='D:\\outside.csv',
    x=>x.options.readJson=()=>({changed:true})]) {
    const f=fixture();mutate(f);assert.throws(()=>validatePositionCapture(f.m,f.i,f.options));
  }
});
test('unstable reports, non-report routes and unsafe source authority are rejected',()=>{
  for(const mutate of [x=>x.c.StablePositionSet=false,x=>x.c.Attempts[0].Stable=false,x=>x.c.NoOrder=false,
    x=>x.m.safety.lmax_accountapi_used=true,x=>x.m.raw_endpoint_fallback_used=true,x=>x.m.portal_origin='https://web-order.london-demo.lmax.com']) {
    const f=fixture();mutate(f);assert.throws(()=>validatePositionCapture(f.m,f.i,f.options));
  }
});
