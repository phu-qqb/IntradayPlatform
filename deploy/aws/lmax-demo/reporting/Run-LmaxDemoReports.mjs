#!/usr/bin/env node
import crypto from 'node:crypto';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import {parseArgs} from 'node:util';

// Invoke the existing report-only application. No new browser or HTTP adapter.
export const SOURCE_COMMIT='6dce3375aaaa7e8042c61a36467e81fe5e97cb49';
export const DOWNLOADER_ROOT='C:\\deploy\\IntradayPlatform\\operator\\lmax-portal-reports-6dce3375-pr61';
export const REPORT_ROOT='D:\\data\\lmax-eod';
const PROFILE='D:\\qq-secure\\lmax-portal\\profiles\\test-1754288005';
const ROLE='arn:aws:sts::761018894194:assumed-role/qq-role-ec2-intraday/i-05626133ca7892fb8';
const SECRET='qq/fund-platform/demo/lmax/portal-reports/1754288005';
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const json=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const required=(ok,code)=>{if(!ok)throw Error(code);};
const write=(p,value)=>fs.writeFileSync(p,JSON.stringify(value,null,2),{flag:'wx'});
export const REPORT_FILES={
  'account-summary':'account-summary.csv','account-statement':'account-statement.pdf',
  'currency-wallets':'currency-wallets.csv','trades':'trades.csv',
  'individual-trades':'individual-trades.csv','open-positions':'open-positions.csv'
};

export function buildReportInvocation({date,execute=false,bootstrap=false,now=new Date(),nonce=crypto.randomUUID(),root=REPORT_ROOT,paths=path.win32}) {
  const reportDate=date??now.toISOString().slice(0,10);
  required(/^\d{4}-\d{2}-\d{2}$/.test(reportDate)&&Number.isFinite(Date.parse(reportDate))
    &&new Date(reportDate).toISOString().slice(0,10)===reportDate,'LMAX_REPORT_DATE_INVALID');
  required(reportDate<=now.toISOString().slice(0,10),'LMAX_REPORT_DATE_IN_FUTURE');
  required(/^[a-zA-Z0-9-]+$/.test(nonce),'LMAX_REPORT_RUN_ID_INVALID');
  required(!bootstrap||execute,'LMAX_BOOTSTRAP_REQUIRES_EXECUTE');
  const runId=`demo-reports-${now.toISOString().replace(/[-:.]/g,'')}-${nonce}`;
  const runRoot=paths.join(root,'logs',runId),manifestPath=paths.join(runRoot,'acquisition-manifest.json');
  const downloadRoot=paths.join(root,'portal-downloads',runId),stagingRoot=paths.join(root,'captures',runId,'inbox');
  const args=[path.win32.join(DOWNLOADER_ROOT,'src','downloader.mjs'),
    '--portal-url','https://account.london-demo.lmax.com/','--account-id','1754288005',
    '--from',reportDate,'--to',reportDate,'--download-root',downloadRoot,'--staging-root',stagingRoot,
    '--auth-mode',bootstrap?'aws-secrets-bootstrap':'manual-session',
    '--session-recovery','aws-secrets','--credential-secret-id',SECRET,'--secrets-region','eu-west-2',
    '--user-data-dir',PROFILE,'--browser-channel','chrome','--auth-origin','https://web-order.london-demo.lmax.com',
    '--output-manifest',manifestPath];
  if(execute)args.push('--execute-portal-download');
  return {runId,runRoot,manifestPath,reportDate,execute,bootstrap,downloadRoot,stagingRoot,startedUtc:now.toISOString(),args};
}

export function roleOnlyEnvironment(original=process.env,exists=fs.existsSync) {
  const env={...original};
  const excluded=new Set(['AWS_ACCESS_KEY_ID','AWS_SECRET_ACCESS_KEY','AWS_SESSION_TOKEN','AWS_PROFILE',
    'AWS_DEFAULT_PROFILE','AWS_SHARED_CREDENTIALS_FILE','AWS_CONFIG_FILE','AWS_EC2_METADATA_DISABLED',
    'AWS_WEB_IDENTITY_TOKEN_FILE','AWS_ROLE_ARN','AWS_CONTAINER_CREDENTIALS_RELATIVE_URI',
    'AWS_CONTAINER_CREDENTIALS_FULL_URI','AWS_CONTAINER_AUTHORIZATION_TOKEN','AWS_CONTAINER_AUTHORIZATION_TOKEN_FILE',
    'AWS_SDK_LOAD_CONFIG','AWS_EC2_METADATA_SERVICE_ENDPOINT','AWS_EC2_METADATA_SERVICE_ENDPOINT_MODE',
    'AWS_REGION','AWS_DEFAULT_REGION']);
  for(const key of Object.keys(env))if(excluded.has(key.toUpperCase())||key.toUpperCase().startsWith('AWS_ENDPOINT_URL'))delete env[key];
  env.AWS_SHARED_CREDENTIALS_FILE='C:\\deploy\\IntradayPlatform\\staging\\role-only-no-credentials';
  env.AWS_CONFIG_FILE='C:\\deploy\\IntradayPlatform\\staging\\role-only-no-config';
  required(!exists(env.AWS_SHARED_CREDENTIALS_FILE)&&!exists(env.AWS_CONFIG_FILE),'ROLE_ONLY_CONFIG_SENTINEL_MUST_BE_ABSENT');
  env.AWS_EC2_METADATA_DISABLED='false';env.AWS_PAGER='';env.PYTHONIOENCODING='utf-8';
  env.AWS_REGION='eu-west-2';env.AWS_DEFAULT_REGION='eu-west-2';
  return env;
}

export function verifyDownloader() {
  const manifest=json(path.win32.join(DOWNLOADER_ROOT,'source-manifest.json'));
  required(manifest.repository==='phu-qqb/QQ.Production.Core'&&manifest.source_commit===SOURCE_COMMIT,'LMAX_REPORT_SOURCE_COMMIT_MISMATCH');
  required(sha(path.win32.join(DOWNLOADER_ROOT,'src','downloader.mjs'))==='495093a578b3ac8f2ab4b202fcf39c713a97512f3b01c9216ed866798770257d','LMAX_REPORT_DOWNLOADER_HASH_MISMATCH');
  required(Array.isArray(manifest.files)&&manifest.files.length>0,'LMAX_REPORT_SOURCE_MANIFEST_EMPTY');
  for(const file of manifest.files) {
    required(/^(src|test)\/[a-zA-Z0-9.-]+$|^(README\.md|package(-lock)?\.json)$/.test(file.path),'LMAX_REPORT_SOURCE_PATH_INVALID');
    required(sha(path.win32.join(DOWNLOADER_ROOT,file.path))===file.sha256,'LMAX_REPORT_SOURCE_HASH_MISMATCH');
  }
}

export function validateAcquisition(manifest,invocation,{paths=path.win32,hashFile=sha,sizeFile=p=>fs.statSync(p).size}={}) {
  const m=manifest,i=invocation;
  required(m.account_id==='1754288005'&&m.report_date_from===i.reportDate&&m.report_date_to===i.reportDate,'ACQUISITION_SCOPE_MISMATCH');
  required(m.acquisition_method==='LMAX_PORTAL_BROWSER_AUTHENTICATED_REPORT_FORM'
    &&m.portal_origin==='https://account.london-demo.lmax.com'&&m.execute_portal_download===true,'ACQUISITION_METHOD_MISMATCH');
  required(Number.isFinite(Date.parse(m.generated_utc))&&Date.parse(m.generated_utc)>=Date.parse(i.startedUtc),'ACQUISITION_STALE_MANIFEST');
  required(m.raw_endpoint_fallback_used===false&&m.credentials_recorded===false&&m.secret_values_recorded===false
    &&m.totp_recorded===false&&m.flow_capture_contains_headers===false&&m.flow_capture_contains_cookies===false
    &&m.flow_capture_contains_credentials===false,'ACQUISITION_SAFETY_MISMATCH');
  required(m.safety?.order_entry_enabled===false&&m.safety?.operational_orders===false&&m.safety?.production_live===false
    &&m.safety?.trading_readiness===false&&m.safety?.lmax_portal_reports_used===true
    &&m.safety?.lmax_order_entry_used===false&&m.safety?.lmax_fix_order_entry_used===false&&m.safety?.lmax_accountapi_used===false,'ACQUISITION_AUTHORITY_MISMATCH');
  required(m.authentication_proof?.method==='VALID_ACCOUNT_SCOPED_REPORT_RESPONSE'
    &&m.authentication_proof?.account_id==='1754288005'&&m.authentication_proof?.http_status===200
    &&/^[a-f0-9]{64}$/.test(m.authentication_proof?.response_sha256??''),'ACQUISITION_AUTHENTICATION_PROOF_MISSING');
  if(i.bootstrap) {
    required(m.qualification_result==='LMAX_PORTAL_AUTOMATED_SESSION_PERSISTED'
      &&m.navigation_safety_status===m.qualification_result&&m.manual_session_reopen_proof?.status==='AUTHENTICATED_REPORT_FORM_PRESENT'
      &&m.manual_session_reopen_proof?.account_id==='1754288005'&&m.manual_session_reopen_proof?.secret_read_during_probe===false,'ACQUISITION_REOPEN_PROOF_MISSING');
    return [];
  }
  required(m.navigation_safety_status==='REPORT_DOWNLOADS_COMPLETED'&&m.files?.length===6,'ACQUISITION_INCOMPLETE');
  const seen=new Set();
  for(const f of m.files) {
    const filename=REPORT_FILES[f.report_type];
    required(filename&&!seen.has(f.report_type),'ACQUISITION_REPORT_TYPE_MISMATCH');seen.add(f.report_type);
    required(f.selected_account_id==='1754288005'&&f.selected_report_date_from===i.reportDate
      &&f.selected_report_date_to===i.reportDate&&f.normalized_filename===filename,'ACQUISITION_FILE_SCOPE_MISMATCH');
    required(f.download_strategy==='portal_report_form'&&f.raw_endpoint_fallback_used===false
      &&f.navigation_safety_status==='KNOWN_REPORT_FORM_ENDPOINT','ACQUISITION_FILE_METHOD_MISMATCH');
    const expected=paths.join(i.stagingRoot,'1754288005',i.reportDate,filename);
    required(paths.resolve(f.staged_path??'')===paths.resolve(expected),'ACQUISITION_FILE_PATH_MISMATCH');
    required(/^[a-f0-9]{64}$/.test(f.sha256)&&hashFile(expected)===f.sha256
      &&Number.isInteger(f.file_size)&&f.file_size>0&&sizeFile(expected)===f.file_size,'ACQUISITION_FILE_HASH_MISMATCH');
  }
  return m.files.map(f=>({report_type:f.report_type,path:f.staged_path,sha256:f.sha256}));
}

export function assertOwner() {
  required(process.platform==='win32'&&os.hostname().toUpperCase()==='EC2AMAZ-1QPHTD8'
    &&os.userInfo().username.toLowerCase()==='administrator'&&process.env.USERDOMAIN?.toUpperCase()==='EC2AMAZ-1QPHTD8','LMAX_REPORT_DEMO_OWNER_CONTEXT_REQUIRED');
}

export function verifyPortalQualification(pin) {
  required(pin.portal_acquisition_qualified===true,'PORTAL_ACQUISITION_QUALIFICATION_REQUIRED');
  for(const mode of ['AUTHENTICATION_BOOTSTRAP','REPORT_DOWNLOAD']) {
    const ref=pin.portal_qualification?.find(x=>x.mode===mode);
    required(ref&&/^[a-f0-9]{64}$/.test(ref.sha256),'PORTAL_QUALIFICATION_RECEIPT_REQUIRED');
    const relative=path.win32.relative(path.win32.join(REPORT_ROOT,'logs'),ref.path);
    required(!relative.startsWith('..')&&!path.win32.isAbsolute(relative)&&relative.endsWith('command-result.json'),'PORTAL_QUALIFICATION_PATH_INVALID');
    required(sha(ref.path)===ref.sha256,'PORTAL_QUALIFICATION_HASH_MISMATCH');
    const r=json(ref.path);
    required(r.schema==='lmax_demo_report_launcher_receipt_v2'&&r.mode===mode&&r.success===true
      &&r.account_id==='1754288005'&&r.source_commit===SOURCE_COMMIT,'PORTAL_QUALIFICATION_SCOPE_MISMATCH');
    required(sha(r.manifest_path)===r.manifest_sha256,'PORTAL_QUALIFICATION_MANIFEST_CHANGED');
    const m=json(r.manifest_path);
    validateAcquisition(m,{reportDate:r.report_date,startedUtc:r.started_utc,bootstrap:mode==='AUTHENTICATION_BOOTSTRAP',
      stagingRoot:path.win32.join(REPORT_ROOT,'captures',r.run_id,'inbox')});
    if(mode==='AUTHENTICATION_BOOTSTRAP')required(m.login_performed===true&&m.secret_fetched===true
      &&m.session_already_active===false,'PORTAL_SECRET_LOGIN_NOT_QUALIFIED');
  }
}

function noExistingProfileProcess() {
  // Inspect command lines locally; never log them or terminate existing processes.
  const command="$n=@(Get-CimInstance Win32_Process -ErrorAction Stop | Where-Object {($_.Name -eq 'node.exe' -or $_.Name -eq 'chrome.exe') -and ($_.CommandLine -like '*test-1754288005*' -or $_.CommandLine -like '*lmax-portal-reports-6dce3375-pr61*')}).Count; Write-Output $n";
  const r=spawnSync('powershell.exe',['-NoProfile','-NonInteractive','-Command',command],{shell:false,encoding:'utf8',timeout:15000});
  required(r.status===0&&r.stdout.trim()==='0','LMAX_REPORT_PROFILE_BUSY_OR_UNVERIFIED');
}

export function runReports({date,execute=false,bootstrap=false}={}) {
  assertOwner();verifyDownloader();
  const i=buildReportInvocation({date,execute,bootstrap});
  fs.mkdirSync(path.win32.join(REPORT_ROOT,'logs'),{recursive:true});fs.mkdirSync(i.runRoot);
  const receipt={schema:'lmax_demo_report_launcher_receipt_v2',run_id:i.runId,started_utc:i.startedUtc,
    source_commit:SOURCE_COMMIT,account_id:'1754288005',report_date:i.reportDate,
    mode:execute?(bootstrap?'AUTHENTICATION_BOOTSTRAP':'REPORT_DOWNLOAD'):'PLAN_ONLY',
    success:false,error:null,manifest_path:i.manifestPath,files:[],worker_started:false,database_import_performed:false};
  const lock=path.win32.join(REPORT_ROOT,'portal-acquisition.lock');
  let fd=null,retainLock=false;
  try {
    fd=fs.openSync(lock,'wx');fs.writeSync(fd,JSON.stringify({run_id:i.runId,pid:process.pid,started_utc:i.startedUtc}));
    let env=process.env;
    if(execute) {
      noExistingProfileProcess();env=roleOnlyEnvironment();
      const identity=spawnSync('aws.exe',['sts','get-caller-identity','--region','eu-west-2','--output','json'],{env,shell:false,encoding:'utf8',timeout:30000});
      required(identity.status===0&&!identity.error,'ROLE_IDENTITY_CHECK_FAILED');
      required(JSON.parse(identity.stdout.replace(/^\uFEFF/,'')).Arn===ROLE,'DEMO_INSTANCE_ROLE_REQUIRED');
      receipt.verified_role=ROLE;
    }
    const logPath=path.win32.join(i.runRoot,'downloader.log'),log=fs.openSync(logPath,'wx');let result;
    try {result=spawnSync(process.execPath,i.args,{env,shell:false,stdio:['ignore',log,log],timeout:300000});}
    finally {fs.closeSync(log);}
    receipt.process_exit_code=result.status;receipt.process_signal=result.signal;receipt.launch_error=result.error?.code??null;receipt.log_path=logPath;
    if(result.error?.code==='ETIMEDOUT'||result.signal||result.status===null)retainLock=true;
    if(result.error||result.status!==0) {
      // Only a closed list of machine codes leaves the private runtime log.
      const code=fs.readFileSync(logPath,'utf8').match(/\b(?:NO_GO_LMAX_PORTAL_[A-Z0-9_]+|AUTH_REQUIRED|AUTH_ORIGIN_NOT_ALLOWED_IN_PHASE|REPORT_RESPONSE_[A-Z0-9_]+)\b/)?.[0];
      throw Error(code??(retainLock?'ACQUISITION_PROCESS_UNCERTAIN_INSPECT_LOCK':'ACQUISITION_PROCESS_FAILED'));
    }
    required(fs.existsSync(i.manifestPath),'ACQUISITION_MANIFEST_MISSING');
    receipt.manifest_sha256=sha(i.manifestPath);
    if(execute) {
      const m=json(i.manifestPath);receipt.files=validateAcquisition(m,i);
      receipt.authentication_proof=m.authentication_proof;
      receipt.login_performed=m.login_performed===true;
      receipt.session_already_active=m.session_already_active===true;
      receipt.secret_fetched=m.secret_fetched===true;
      receipt.mfa_mode=m.mfa_mode??null;
      receipt.manual_session_reopen_proof=m.manual_session_reopen_proof??null;
      receipt.session_recovery_mode=m.session_recovery_mode;
      receipt.qualification_result=m.qualification_result??null;
    }
    receipt.success=true;
  }catch(e){receipt.error=e.code==='EEXIST'?'ACQUISITION_LOCK_HELD':(/^[A-Z0-9_]+$/.test(e.message)?e.message:'ACQUISITION_FAILED_INSPECT_PRIVATE_LOG');}
  finally {if(fd!==null){fs.closeSync(fd);if(!retainLock)fs.unlinkSync(lock);}}
  receipt.lock_retained=retainLock;receipt.completed_utc=new Date().toISOString();
  const receiptPath=path.win32.join(i.runRoot,'command-result.json');receipt.receipt_path=receiptPath;
  write(receiptPath,receipt);return receipt;
}

if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)) {
  try {
    const {values}=parseArgs({options:{date:{type:'string'},execute:{type:'boolean'},bootstrap:{type:'boolean'}},strict:true});
    const receipt=runReports(values);console.log(JSON.stringify(receipt));process.exitCode=receipt.success?0:2;
  }catch(e){console.error(e.message);process.exitCode=1;}
}
