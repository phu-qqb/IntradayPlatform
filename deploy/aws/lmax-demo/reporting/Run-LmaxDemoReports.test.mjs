import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import crypto from 'node:crypto';
import {buildReportInvocation,roleOnlyEnvironment,validateAcquisition,REPORT_FILES} from './Run-LmaxDemoReports.mjs';

function fixture(t) {
  const root=fs.mkdtempSync(path.join(os.tmpdir(),'demo-acquisition-test-'));
  t.after(()=>fs.rmSync(root,{recursive:true,force:true}));
  const i=buildReportInvocation({date:'2026-09-17',execute:true,now:new Date('2026-09-18T09:00:00Z'),nonce:'test-only',root,paths:path});
  const m={account_id:'1754288005',report_date_from:i.reportDate,report_date_to:i.reportDate,
    acquisition_method:'LMAX_PORTAL_BROWSER_AUTHENTICATED_REPORT_FORM',portal_origin:'https://account.london-demo.lmax.com',
    execute_portal_download:true,generated_utc:i.startedUtc,raw_endpoint_fallback_used:false,credentials_recorded:false,
    secret_values_recorded:false,totp_recorded:false,flow_capture_contains_headers:false,flow_capture_contains_cookies:false,
    flow_capture_contains_credentials:false,navigation_safety_status:'REPORT_DOWNLOADS_COMPLETED',
    safety:{order_entry_enabled:false,operational_orders:false,production_live:false,trading_readiness:false,
      lmax_portal_reports_used:true,lmax_order_entry_used:false,lmax_fix_order_entry_used:false,lmax_accountapi_used:false},
    authentication_proof:{method:'VALID_ACCOUNT_SCOPED_REPORT_RESPONSE',account_id:'1754288005',http_status:200,response_sha256:'a'.repeat(64)},files:[]};
  for(const [type,name]of Object.entries(REPORT_FILES)) {
    const p=path.join(i.stagingRoot,'1754288005',i.reportDate,name),bytes=Buffer.from('UNIT TEST FIXTURE ONLY\n');
    fs.mkdirSync(path.dirname(p),{recursive:true});fs.writeFileSync(p,bytes);
    m.files.push({report_type:type,selected_account_id:'1754288005',selected_report_date_from:i.reportDate,
      selected_report_date_to:i.reportDate,normalized_filename:name,staged_path:p,file_size:bytes.length,
      sha256:crypto.createHash('sha256').update(bytes).digest('hex'),download_strategy:'portal_report_form',
      raw_endpoint_fallback_used:false,navigation_safety_status:'KNOWN_REPORT_FORM_ENDPOINT'});
  }
  return {i,m};
}
test('default invocation plans only; explicit execution binds exact Demo secret and report date',()=>{
  const i=buildReportInvocation({date:'2026-09-17'});
  assert.ok(!i.args.includes('--execute-portal-download'));assert.ok(i.args.includes('aws-secrets'));
  assert.ok(i.args.includes('qq/fund-platform/demo/lmax/portal-reports/1754288005'));
  assert.throws(()=>buildReportInvocation({date:'2026-02-30'}),/DATE_INVALID/);
  assert.throws(()=>buildReportInvocation({bootstrap:true}),/REQUIRES_EXECUTE/);
});
test('role isolation is case-insensitive, child-only, and refuses existing sentinel files',()=>{
  const original={aws_access_key_id:'test-only',AWS_PROFILE:'legacy-test',AWS_ENDPOINT_URL:'https://example.invalid',OTHER:'retained'};
  const env=roleOnlyEnvironment(original,()=>false);
  assert.equal(env.aws_access_key_id,undefined);assert.equal(env.AWS_PROFILE,undefined);assert.equal(env.AWS_ENDPOINT_URL,undefined);
  assert.equal(env.OTHER,'retained');assert.equal(original.AWS_PROFILE,'legacy-test');assert.equal(env.AWS_EC2_METADATA_DISABLED,'false');
  assert.throws(()=>roleOnlyEnvironment({},()=>true),/SENTINEL_MUST_BE_ABSENT/);
});
test('only complete exact-scope hashed report files are accepted',t=>{
  const {i,m}=fixture(t);assert.equal(validateAcquisition(m,i,{paths:path}).length,6);
  const wrong=structuredClone(m);wrong.account_id='999';assert.throws(()=>validateAcquisition(wrong,i,{paths:path}),/SCOPE_MISMATCH/);
  const old=structuredClone(m);old.generated_utc='2026-09-17T09:00:00Z';assert.throws(()=>validateAcquisition(old,i,{paths:path}),/STALE_MANIFEST/);
  const wrongDate=structuredClone(m);wrongDate.files[0].selected_report_date_to='2026-09-18';assert.throws(()=>validateAcquisition(wrongDate,i,{paths:path}),/FILE_SCOPE/);
  const incomplete=structuredClone(m);incomplete.files.pop();assert.throws(()=>validateAcquisition(incomplete,i,{paths:path}),/INCOMPLETE/);
  const escaped=structuredClone(m);escaped.files[0].staged_path=path.join(i.runRoot,'outside.csv');assert.throws(()=>validateAcquisition(escaped,i,{paths:path}),/FILE_PATH/);
  fs.appendFileSync(m.files[0].staged_path,'tampered');assert.throws(()=>validateAcquisition(m,i,{paths:path}),/FILE_HASH/);
});
test('download denial, unsafe authority, and bootstrap without reopen proof are rejected',t=>{
  const {i,m}=fixture(t);
  const denied=structuredClone(m);denied.navigation_safety_status='DENIED';assert.throws(()=>validateAcquisition(denied,i,{paths:path}),/INCOMPLETE/);
  const unsafe=structuredClone(m);unsafe.safety.lmax_accountapi_used=true;assert.throws(()=>validateAcquisition(unsafe,i,{paths:path}),/AUTHORITY/);
  const raw=structuredClone(m);raw.raw_endpoint_fallback_used=true;assert.throws(()=>validateAcquisition(raw,i,{paths:path}),/SAFETY/);
  assert.throws(()=>validateAcquisition(m,{...i,bootstrap:true},{paths:path}),/REOPEN_PROOF/);
});
