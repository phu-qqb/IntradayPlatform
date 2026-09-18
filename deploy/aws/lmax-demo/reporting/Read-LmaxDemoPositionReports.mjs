import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {spawnSync} from 'node:child_process';
import {fileURLToPath,pathToFileURL} from 'node:url';
import {parseArgs} from 'node:util';
import {assertOwner,verifyDownloader,roleOnlyEnvironment,buildReportInvocation,runReports,validateAcquisition,DOWNLOADER_ROOT,REPORT_ROOT,SOURCE_COMMIT} from './Run-LmaxDemoReports.mjs';

const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const json=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const require=(ok,code)=>{if(!ok)throw Error(code);};
const write=(p,x)=>fs.writeFileSync(p,JSON.stringify(x,null,2),{flag:'wx'});

export function previousReportDay(openingDate) {
  require(/^\d{4}-\d{2}-\d{2}$/.test(openingDate),'OPENING_DATE_INVALID');
  const date=new Date(`${openingDate}T00:00:00Z`);
  require(Number.isFinite(date.getTime())&&date.toISOString().slice(0,10)===openingDate,'OPENING_DATE_INVALID');
  require(date.getUTCDay()!==0&&date.getUTCDay()!==6,'OPENING_WEEKDAY_REQUIRED');
  do {date.setUTCDate(date.getUTCDate()-1);} while(date.getUTCDay()===0||date.getUTCDay()===6);
  return date.toISOString().slice(0,10);
}

export function buildOpeningState({openingDate,reportDate,accountRows,positionSnapshot,acquiredAtUtc}) {
  require(previousReportDay(openingDate)===reportDate,'OPENING_REPORT_DAY_MISMATCH');
  require(accountRows.length===1&&accountRows.every(r=>r['Account Id']==='1754288005'),'OPENING_ACCOUNT_SUMMARY_REQUIRED');
  require(positionSnapshot.records.every(r=>r.AccountId==='1754288005'),'OPENING_POSITION_ACCOUNT_MISMATCH');
  const number=s=>{require(typeof s==='string'&&/^[+-]?\d+(\.\d+)?$/.test(s)&&Number.isFinite(Number(s)),'OPENING_MARGIN_INVALID');return Number(s);};
  const margin=accountRows.reduce((sum,row)=>sum+number(row['Margin on Open Positions']),0);
  const flat=positionSnapshot.records.every(r=>r.Quantity==='0');
  require(!flat||margin===0,'OPENING_EMPTY_POSITIONS_MARGIN_CONFLICT');
  require(Number.isFinite(Date.parse(acquiredAtUtc))&&acquiredAtUtc.slice(0,10)>reportDate,'OPENING_REPORT_ACQUIRED_BEFORE_DAY_CLOSED');
  return {schema:'lmax_demo_opening_position_report_v1',account_id:'1754288005',opening_date:openingDate,
    source_report_date:reportDate,acquired_at_utc:acquiredAtUtc,position_count:positionSnapshot.position_count,
    positions:positionSnapshot.records,flat_in_report:flat,source:'OFFICIAL_PREVIOUS_COMPLETED_REPORT_DAY',
    report_timezone_status:'UNPROVEN',current_account_observation:false,working_orders_established:false,
    trading_authorized:false,required_start_checks:['PREVIOUS_SESSION_RECONCILED','PREVIOUS_ORDERS_TERMINAL',
      'EXCLUSIVE_ACCOUNT_ACTIVITY','NO_UNACCOUNTED_ACTIVITY_AFTER_REPORT_BOUNDARY']};
}

export async function readOpeningReports({openingDate,sourceReceipt}) {
  assertOwner();verifyDownloader();
  require(sha(fileURLToPath(new URL('./Run-LmaxDemoReports.mjs',import.meta.url)))==='4773b43e0b7726125b0a5bf90147f5cce47971786ce27bb8d46bcd37bb43673f','REPORT_LAUNCHER_PIN_MISMATCH');
  const reportDate=previousReportDay(openingDate);
  require(openingDate===new Date().toISOString().slice(0,10),'OPENING_DATE_MUST_BE_TODAY');
  let r;
  if(sourceReceipt) {
    const relative=path.win32.relative(path.win32.join(REPORT_ROOT,'logs'),path.win32.resolve(sourceReceipt));
    require(relative&&!relative.startsWith('..')&&!path.win32.isAbsolute(relative)&&path.win32.basename(sourceReceipt)==='command-result.json','OPENING_RECEIPT_PATH_INVALID');
    r=json(sourceReceipt);
  }else r=runReports({date:reportDate,execute:true});
  require(r.schema==='lmax_demo_report_launcher_receipt_v2'&&r.success===true&&r.mode==='REPORT_DOWNLOAD'
    &&r.account_id==='1754288005'&&r.report_date===reportDate&&r.source_commit===SOURCE_COMMIT
    &&/^demo-reports-[a-zA-Z0-9-]+$/.test(r.run_id),'OPENING_SUCCESSFUL_ACQUISITION_REQUIRED');
  const manifestPath=path.win32.join(REPORT_ROOT,'logs',r.run_id,'acquisition-manifest.json');
  require(r.manifest_path===manifestPath&&sha(manifestPath)===r.manifest_sha256,'OPENING_MANIFEST_CHANGED');
  const files=validateAcquisition(json(manifestPath),{reportDate,startedUtc:r.started_utc,bootstrap:false,
    stagingRoot:path.win32.join(REPORT_ROOT,'captures',r.run_id,'inbox')});
  const positions=files.find(f=>f.report_type==='open-positions'),summary=files.find(f=>f.report_type==='account-summary');
  const {createPositionSnapshot,parseCsv}=await import(pathToFileURL(path.win32.join(DOWNLOADER_ROOT,'src','bracketed-snapshot.mjs')).href);
  const result=buildOpeningState({openingDate,reportDate,accountRows:parseCsv(fs.readFileSync(summary.path,'utf8')),
    positionSnapshot:createPositionSnapshot(fs.readFileSync(positions.path),{expectedAccountId:'1754288005'}),acquiredAtUtc:r.completed_utc});
  const out=path.win32.join(REPORT_ROOT,'opening-runs',`${openingDate}-${crypto.randomUUID()}`);
  fs.mkdirSync(out,{recursive:true});
  Object.assign(result,{built_at_utc:new Date().toISOString(),acquisition_run_id:r.run_id,manifest_path:manifestPath,
    manifest_sha256:r.manifest_sha256,position_report_sha256:positions.sha256,account_summary_sha256:summary.sha256,
    retained_acquisition_used:Boolean(sourceReceipt),reader_sha256:sha(fileURLToPath(import.meta.url)),
    receipt_path:path.win32.join(out,'opening-position-receipt.json'),database_writes:0,trading_started:false,email_sent:false});
  write(result.receipt_path,result);return result;
}

// Calls the already installed report-only application's documented bracket mode.
// It never opens a trading page, asks an account API, or declares no working orders.
export function validatePositionCapture(m,i,{now=new Date(),paths=path.win32,hashFile=sha,readJson=json}={}) {
  require(m.account_id==='1754288005'&&m.report_date_from===i.reportDate&&m.report_date_to===i.reportDate
    &&i.reportDate===now.toISOString().slice(0,10),'POSITION_REPORT_SCOPE_MISMATCH');
  require(m.acquisition_method==='LMAX_PORTAL_BROWSER_AUTHENTICATED_REPORT_FORM'
    &&m.portal_origin==='https://account.london-demo.lmax.com'&&m.execute_portal_download===true
    &&m.navigation_safety_status==='BRACKETED_REPORT_SNAPSHOT_COMPLETED','POSITION_REPORT_METHOD_MISMATCH');
  require(m.raw_endpoint_fallback_used===false&&m.credentials_recorded===false&&m.secret_values_recorded===false
    &&m.totp_recorded===false&&m.flow_capture_contains_headers===false&&m.flow_capture_contains_cookies===false
    &&m.flow_capture_contains_credentials===false,'POSITION_REPORT_SAFETY_MISMATCH');
  require(m.safety?.order_entry_enabled===false&&m.safety?.operational_orders===false&&m.safety?.production_live===false
    &&m.safety?.trading_readiness===false&&m.safety?.lmax_order_entry_used===false
    &&m.safety?.lmax_fix_order_entry_used===false&&m.safety?.lmax_accountapi_used===false,'POSITION_REPORT_AUTHORITY_MISMATCH');
  require(m.authentication_proof?.method==='VALID_ACCOUNT_SCOPED_REPORT_RESPONSE'
    &&m.authentication_proof?.account_id==='1754288005'&&m.authentication_proof?.http_status===200,'POSITION_REPORT_AUTHENTICATION_MISSING');
  const c=m.bracketed_snapshot,a=m.bracketed_contract_artifact;
  require(c?.ContractVersion==='lmax_portal_bracketed_current_position_snapshot_v2'
    &&c.AccountId==='1754288005'&&c.Environment==='LMAX_LONDON_DEMO'&&c.SessionMode==='manual-session'
    &&c.NoOrder===true&&c.NoFix===true&&c.NoDatabaseWrite===true,'POSITION_REPORT_CONTRACT_MISMATCH');
  const contractPath=paths.join(i.snapshotRoot,'lmax-portal-bracketed-current-position-snapshot-v2.json');
  require(a?.path===contractPath&&a.sha256===hashFile(contractPath)
    &&JSON.stringify(readJson(contractPath))===JSON.stringify(c),'POSITION_REPORT_CONTRACT_HASH_MISMATCH');
  const lower=Date.parse(c.AsOfLowerBoundUtc),upper=Date.parse(c.AsOfUpperBoundUtc),started=Date.parse(i.startedUtc);
  require(Number.isFinite(lower)&&Number.isFinite(upper)&&Number.isFinite(started)&&lower>=started-1000
    &&upper>=lower&&upper-lower<=30000&&upper<=now.getTime()&&now.getTime()-lower<=900000,'POSITION_REPORT_TIME_INVALID');
  require(c.StableExecutionSet===true&&c.StablePositionSet===true&&c.BrokerDateSequenceStatus==='MONOTONIC_NON_DECREASING'
    &&c.TimeAuthorityMode==='BRACKETED_CURRENT_SNAPSHOT_STABLE_EXECUTION_SET'
    &&c.OpenPositionsSnapshotSemanticDecision?.CurrentSnapshotStatus==='PROVEN_CURRENT_BRACKETED_SNAPSHOT'
    &&c.ComplementaryAccountEvidence?.SelectedAccountId==='1754288005'
    &&c.ComplementaryAccountEvidence?.AccountRowsObserved>0
    &&c.ComplementaryAccountEvidence.AccountRowsMatched===c.ComplementaryAccountEvidence.AccountRowsObserved,'POSITION_REPORT_COMPLETENESS_UNPROVEN');
  require(Number.isInteger(c.PositionCount)&&c.PositionCount>=0&&Number.isInteger(c.ExecutionCount)&&c.ExecutionCount>=0
    &&Array.isArray(c.Attempts)&&c.Attempts.length>=1&&c.Attempts.length<=3,'POSITION_REPORT_COUNTS_INVALID');
  const attempt=c.Attempts.at(-1);
  require(attempt.Stable===true,'POSITION_REPORT_NOT_STABLE');
  for(const label of ['T0','P1','T1','P2','T2']) {
    const item=attempt[label],relative=paths.relative(i.snapshotRoot,item?.Artifact?.path??'');
    require(relative&&!relative.startsWith('..')&&!paths.isAbsolute(relative)
      &&item.RawSha256===item.Artifact.sha256&&hashFile(item.Artifact.path)===item.RawSha256,'POSITION_REPORT_ARTIFACT_CHANGED');
  }
  return {schema:'lmax_demo_position_report_observation_v1',account_id:c.AccountId,report_date:i.reportDate,
    broker_capture_interval_utc:{from:c.AsOfLowerBoundUtc,to:c.AsOfUpperBoundUtc},
    report_timezone_status:c.ExplicitTimeZoneStatus,position_count:c.PositionCount,execution_count:c.ExecutionCount,
    stable_positions:true,stable_executions:true,contract_path:contractPath,contract_sha256:a.sha256,
    source:'AUTHENTIC_OFFICIAL_REPORTS',working_orders_established:false,trading_authorized:false};
}

export async function readPositions() {
  assertOwner();verifyDownloader();
  require(sha(fileURLToPath(new URL('./Run-LmaxDemoReports.mjs',import.meta.url)))==='4773b43e0b7726125b0a5bf90147f5cce47971786ce27bb8d46bcd37bb43673f','REPORT_LAUNCHER_PIN_MISMATCH');
  const i=buildReportInvocation({execute:true});
  i.snapshotRoot=path.win32.join(REPORT_ROOT,'position-snapshots',i.runId);
  i.args.push('--capture-bracketed-snapshot','--evidence-root',i.snapshotRoot);
  fs.mkdirSync(i.runRoot,{recursive:true});
  const receipt={schema:'lmax_demo_position_report_reader_v1',run_id:i.runId,started_utc:i.startedUtc,
    account_id:'1754288005',report_date:i.reportDate,source_commit:SOURCE_COMMIT,reader_sha256:sha(fileURLToPath(import.meta.url)),
    success:false,error:null,manifest_path:i.manifestPath,working_orders_established:false,trading_started:false,email_sent:false};
  const lock=path.win32.join(REPORT_ROOT,'portal-acquisition.lock');let fd=null,retainLock=false;
  try {
    fd=fs.openSync(lock,'wx');fs.writeSync(fd,JSON.stringify({run_id:i.runId,pid:process.pid,started_utc:i.startedUtc}));
    const check=spawnSync('powershell.exe',['-NoProfile','-NonInteractive','-Command',
      "$n=@(Get-CimInstance Win32_Process -ErrorAction Stop | Where-Object {($_.Name -eq 'node.exe' -or $_.Name -eq 'chrome.exe') -and ($_.CommandLine -like '*test-1754288005*' -or $_.CommandLine -like '*lmax-portal-reports-6dce3375-pr61*')}).Count; Write-Output $n"],{shell:false,encoding:'utf8',timeout:15000});
    require(check.status===0&&check.stdout.trim()==='0','LMAX_REPORT_PROFILE_BUSY_OR_UNVERIFIED');
    const env=roleOnlyEnvironment();
    const identity=spawnSync('aws.exe',['sts','get-caller-identity','--region','eu-west-2','--output','json'],{env,shell:false,encoding:'utf8',timeout:30000});
    require(identity.status===0&&!identity.error&&JSON.parse(identity.stdout.replace(/^\uFEFF/,'')).Arn==='arn:aws:sts::761018894194:assumed-role/qq-role-ec2-intraday/i-05626133ca7892fb8','DEMO_INSTANCE_ROLE_REQUIRED');
    const logPath=path.win32.join(i.runRoot,'downloader.log'),log=fs.openSync(logPath,'wx');let result;
    try {result=spawnSync(process.execPath,i.args,{env,shell:false,stdio:['ignore',log,log],timeout:300000});}
    finally {fs.closeSync(log);}
    retainLock=!!(result.error?.code==='ETIMEDOUT'||result.signal||result.status===null);
    receipt.process_exit_code=result.status;receipt.log_path=logPath;
    if(result.error||result.status!==0) {
      const code=fs.readFileSync(logPath,'utf8').match(/\b(?:NO_GO_[A-Z0-9_]+|ARCH7B_[A-Z0-9_]+|AUTH_REQUIRED|AUTH_ORIGIN_NOT_ALLOWED_IN_PHASE|REPORT_RESPONSE_[A-Z0-9_]+)\b/)?.[0];
      throw Error(code??(retainLock?'ACQUISITION_PROCESS_UNCERTAIN_INSPECT_LOCK':'ACQUISITION_PROCESS_FAILED'));
    }
    const m=json(i.manifestPath);
    receipt.manifest_sha256=sha(i.manifestPath);receipt.observation=validatePositionCapture(m,i);
    const {createPositionSnapshot}=await import(pathToFileURL(path.win32.join(DOWNLOADER_ROOT,'src','bracketed-snapshot.mjs')).href);
    const snapshot=createPositionSnapshot(fs.readFileSync(m.bracketed_snapshot.Attempts.at(-1).P2.Artifact.path),{expectedAccountId:'1754288005'});
    require(snapshot.position_count===receipt.observation.position_count,'POSITION_REPORT_COUNT_MISMATCH');
    receipt.positions=snapshot.records;
    receipt.flat_in_report=snapshot.records.every(row=>row.Quantity==='0');
    receipt.success=true;
  }catch(e){receipt.error=e.code==='EEXIST'?'ACQUISITION_LOCK_HELD':(/^[A-Z0-9_]+$/.test(e.message)?e.message:'ACQUISITION_FAILED_INSPECT_PRIVATE_LOG');}
  finally {if(fd!==null){fs.closeSync(fd);if(!retainLock)fs.unlinkSync(lock);}}
  receipt.lock_retained=retainLock;receipt.completed_utc=new Date().toISOString();
  receipt.receipt_path=path.win32.join(i.runRoot,'position-reader-receipt.json');write(receipt.receipt_path,receipt);
  return receipt;
}

if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)) {
  try {
    const {values}=parseArgs({options:{execute:{type:'boolean'},'opening-date':{type:'string'},'source-receipt':{type:'string'}},strict:true});
    require(values.execute===true,'POSITION_REPORT_READER_REQUIRES_EXECUTE');
    require(!values['source-receipt']||values['opening-date'],'OPENING_DATE_REQUIRED_WITH_RECEIPT');
    const r=values['opening-date']?await readOpeningReports({openingDate:values['opening-date'],sourceReceipt:values['source-receipt']}):await readPositions();
    console.log(JSON.stringify(r));process.exitCode=values['opening-date']?0:(r.success?0:2);
  }catch(e){console.error(e.message);process.exitCode=1;}
}
