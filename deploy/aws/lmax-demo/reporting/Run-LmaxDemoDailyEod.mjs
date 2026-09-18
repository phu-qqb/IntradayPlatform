import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import crypto from 'node:crypto';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import {parseArgs} from 'node:util';
import {buildRecap,writeBundle} from './Build-LmaxDemoDailyRecap.mjs';
import {runReports,validateAcquisition,verifyPortalQualification} from './Run-LmaxDemoReports.mjs';

const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const json=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const write=(p,x)=>fs.writeFileSync(p,JSON.stringify(x,null,2),{flag:'wx'});
export function validateDate(date,now=new Date()) {
  if(!/^\d{4}-\d{2}-\d{2}$/.test(date)||new Date(date).toISOString().slice(0,10)!==date||date>now.toISOString().slice(0,10)) throw Error('INVALID_REPORT_DATE');
  return date;
}
export function candidates(root,date) {
  const proven=[];
  const captures=path.join(root,'captures');
  if(fs.existsSync(captures)) for(const d of fs.readdirSync(captures,{withFileTypes:true}).filter(d=>d.isDirectory()).sort((a,b)=>b.name.localeCompare(a.name))) {
    // A failed/partial acquisition must never silently become a cache fallback.
    try {
      const log=path.join(root,'logs',d.name),r=json(path.join(log,'command-result.json'));
      const manifest=path.join(log,'acquisition-manifest.json');
      if(r.schema!=='lmax_demo_report_launcher_receipt_v2'||r.success!==true||r.mode!=='REPORT_DOWNLOAD'
        ||r.run_id!==d.name||r.account_id!=='1754288005'||r.report_date!==date||r.manifest_sha256!==sha(manifest))continue;
      const files=validateAcquisition(json(manifest),{reportDate:date,startedUtc:r.started_utc,
        stagingRoot:path.join(captures,d.name,'inbox'),bootstrap:false},{paths:path});
      const f=files.find(f=>f.report_type==='individual-trades');proven.push({path:f.path,sha256:f.sha256});
    }catch{/* Retain rejected files untouched; only proven complete captures are eligible. */}
  }
  const manual=path.join(root,'inbox','1754288005',date,'individual-trades.csv');
  if(fs.existsSync(manual))proven.push({path:manual,sha256:sha(manual)});
  return proven;
}
export function runDaily({root,date,importer,now=new Date(),invoke=spawnSync,acquire=null}) {
  validateDate(date,now);
  fs.mkdirSync(root,{recursive:true});
  const lock=path.join(root,'daily-eod.lock');
  const fd=fs.openSync(lock,'wx'); // Never steal or automatically clear a stale lock.
  const run=path.join(root,'daily-runs',`${date}-${now.toISOString().replace(/[:.]/g,'')}-${crypto.randomUUID()}`);
  try {
    fs.mkdirSync(run,{recursive:true});
    write(path.join(run,'started.json'),{date,started_at_utc:now.toISOString(),pid:process.pid});
    let acquisition=null,acquisitionError=null;
    if(acquire) {
      try {
        acquisition=acquire({date,execute:true});
        if(acquisition?.schema!=='lmax_demo_report_launcher_receipt_v2'||acquisition.account_id!=='1754288005'
          ||acquisition.report_date!==date||acquisition.mode!=='REPORT_DOWNLOAD')throw Error('ACQUISITION_RECEIPT_SCOPE_MISMATCH');
        if(!acquisition.success)acquisitionError=acquisition.error??'ACQUISITION_FAILED';
      }catch(e){acquisitionError=/^[A-Z0-9_]+$/.test(e.message)?e.message:'ACQUISITION_FAILED_INSPECT_PRIVATE_LOG';}
    }
    const config={environment:'LMAX_DEMO',account_id:'1754288005',date,trade_candidates:candidates(root,date)};
    config.acquisition_receipt=acquisition?.receipt_path??null;
    let eod=null,error=null;
    const provisional=buildRecap(config);
    if(provisional.selected_source) {
      try {
        const dir=path.dirname(provisional.selected_source.path);
        const files={individual:provisional.selected_source};
        for(const [key,name]of [['summary','trades.csv'],['wallet','currency-wallets.csv']]) {
          const p=path.join(dir,name); if(fs.existsSync(p))files[key]={path:p,sha256:sha(p)};
        }
        const output=path.join(run,'eod-receipt.json'),input=path.join(run,'eod-input.json');
        write(input,{date,files,output});
        const log=fs.openSync(path.join(run,'importer.log'),'wx');
        let result;
        try { result=invoke(importer.executable,[importer.assembly,input],{shell:false,stdio:['ignore',log,log],timeout:180000}); }
        finally {fs.closeSync(log);}
        if(result.error||result.status!==0) throw Error('IMPORT_FAILED_OR_TIMED_OUT_INSPECT_RECEIPT_AND_DATABASE');
        eod=json(output);
        if(eod.schema!=='lmax_demo_daily_eod_v1'||eod.date!==date||eod.account_id!==config.account_id||eod.individual_sha256!==files.individual.sha256||eod.import_performed!==true)throw Error('IMPORT_RECEIPT_SCOPE_MISMATCH');
      } catch(e) {error=e.message;eod=null;}
    } else error='NO_VALID_LOCAL_EXPORT';
    config.eod_result=eod;
    config.acquisition_status=!acquire?'LOCAL_EXPORTS_ONLY_PORTAL_DISABLED':acquisitionError
      ?(provisional.selected_source?'PORTAL_FAILED_LOCAL_FALLBACK':'PORTAL_FAILED_NO_VALID_EXPORT')
      :(provisional.selected_source?'PORTAL_REPORTS_ACQUIRED':'PORTAL_REPORTS_ACQUIRED_NO_VALID_TRADE_ROWS');
    config.pipeline_error=[acquisitionError,error].filter(Boolean).join(';')||null;
    write(path.join(run,'recap-config.json'),config);
    const recap=writeBundle(config,path.join(run,'recap'));
    const receipt={schema:'lmax_demo_daily_pipeline_v1',date,completed_at_utc:new Date().toISOString(),run,
      launcher_sha256:sha(fileURLToPath(import.meta.url)),
      acquisition:config.acquisition_status,import_performed:eod!==null,blocking_breaks:eod?.blocking_breaks??null,
      acquisition_receipt:config.acquisition_receipt,acquisition_error:acquisitionError,
      report_status:recap.status,error:config.pipeline_error,email_sent:false,trading_started:false};
    write(path.join(run,'pipeline-receipt.json'),receipt);
    fs.writeFileSync(path.join(root,'latest-daily-eod.json'),JSON.stringify(receipt,null,2));
    return receipt;
  } finally {fs.closeSync(fd);fs.unlinkSync(lock);}
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)) {
  try {
    if(process.platform!=='win32'||os.hostname().toUpperCase()!=='EC2AMAZ-1QPHTD8'||os.userInfo().username.toLowerCase()!=='administrator')throw Error('DEMO_OWNER_CONTEXT_REQUIRED');
    const {values}=parseArgs({options:{date:{type:'string'},scheduled:{type:'boolean'},'local-only':{type:'boolean'},'verify-only':{type:'boolean'}},strict:true});
    const now=new Date();
    if(values.scheduled&&(now.getUTCDay()===0||now.getUTCDay()===6||now.getUTCHours()*60+now.getUTCMinutes()<1215)) {
      console.log(JSON.stringify({status:'OUTSIDE_WEEKDAY_EOD_WINDOW',trading_started:false}));
    } else {
      const here=path.dirname(fileURLToPath(import.meta.url)),pin=json(path.join(here,'runtime-pin.json'));
      for(const f of pin.files)if(sha(path.join(here,f.path))!==f.sha256)throw Error('EOD_RUNTIME_HASH_MISMATCH');
      if(!values['local-only'])verifyPortalQualification(pin);
      if(values['verify-only']) {
        console.log(JSON.stringify({status:'RUNTIME_AND_RETAINED_QUALIFICATION_VERIFIED',commit:pin.commit,portal_acquisition_qualified:!values['local-only'],trading_started:false}));
      } else {
      const result=runDaily({root:'D:\\data\\lmax-eod',date:values.date??now.toISOString().slice(0,10),now,
        acquire:values['local-only']?null:runReports,
        importer:{executable:'C:\\deploy\\IntradayPlatform\\toolchains\\dotnet-sdk-10\\dotnet.exe',assembly:path.join(here,'importer','QQ.Production.Intraday.Tools.LmaxDemoEod.dll')}});
      console.log(JSON.stringify(result));
      process.exitCode=result.error||!result.import_performed||result.blocking_breaks!==0?2:0;
      }
    }
  }catch(e){console.error(e.message);process.exitCode=1;}
}
