import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import crypto from 'node:crypto';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import {parseArgs} from 'node:util';
import {buildRecap,writeBundle} from './Build-LmaxDemoDailyRecap.mjs';

const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const json=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const write=(p,x)=>fs.writeFileSync(p,JSON.stringify(x,null,2),{flag:'wx'});
export function validateDate(date,now=new Date()) {
  if(!/^\d{4}-\d{2}-\d{2}$/.test(date)||new Date(date).toISOString().slice(0,10)!==date||date>now.toISOString().slice(0,10)) throw Error('INVALID_REPORT_DATE');
  return date;
}
export function candidates(root,date) {
  const folders=[path.join(root,'inbox','1754288005',date)];
  const captures=path.join(root,'captures');
  if(fs.existsSync(captures)) for(const d of fs.readdirSync(captures,{withFileTypes:true}).filter(d=>d.isDirectory()).sort((a,b)=>b.name.localeCompare(a.name))) folders.push(path.join(captures,d.name,'inbox','1754288005',date));
  return folders.map(d=>path.join(d,'individual-trades.csv')).filter(p=>fs.existsSync(p)).map(p=>({path:p,sha256:sha(p)}));
}
export function runDaily({root,date,importer,now=new Date(),invoke=spawnSync}) {
  validateDate(date,now);
  fs.mkdirSync(root,{recursive:true});
  const lock=path.join(root,'daily-eod.lock');
  const fd=fs.openSync(lock,'wx'); // Never steal or automatically clear a stale lock.
  const run=path.join(root,'daily-runs',`${date}-${now.toISOString().replace(/[:.]/g,'')}-${crypto.randomUUID()}`);
  try {
    fs.mkdirSync(run,{recursive:true});
    write(path.join(run,'started.json'),{date,started_at_utc:now.toISOString(),pid:process.pid});
    const config={environment:'LMAX_DEMO',account_id:'1754288005',date,trade_candidates:candidates(root,date)};
    let eod=null, error=null;
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
    config.acquisition_status='LOCAL_EXPORTS_ONLY_UNATTENDED_PORTAL_NOT_QUALIFIED';
    config.pipeline_error=error;
    write(path.join(run,'recap-config.json'),config);
    const recap=writeBundle(config,path.join(run,'recap'));
    const receipt={schema:'lmax_demo_daily_pipeline_v1',date,completed_at_utc:new Date().toISOString(),run,
      acquisition:config.acquisition_status,import_performed:eod!==null,blocking_breaks:eod?.blocking_breaks??null,
      report_status:recap.status,error,email_sent:false,trading_started:false};
    write(path.join(run,'pipeline-receipt.json'),receipt);
    fs.writeFileSync(path.join(root,'latest-daily-eod.json'),JSON.stringify(receipt,null,2));
    return receipt;
  } finally {fs.closeSync(fd);fs.unlinkSync(lock);}
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)) {
  try {
    if(process.platform!=='win32'||os.hostname().toUpperCase()!=='EC2AMAZ-1QPHTD8'||os.userInfo().username.toLowerCase()!=='administrator')throw Error('DEMO_OWNER_CONTEXT_REQUIRED');
    const {values}=parseArgs({options:{date:{type:'string'},scheduled:{type:'boolean'}},strict:true});
    const now=new Date();
    if(values.scheduled&&(now.getUTCDay()===0||now.getUTCDay()===6||now.getUTCHours()*60+now.getUTCMinutes()<1215)) {
      console.log(JSON.stringify({status:'OUTSIDE_WEEKDAY_EOD_WINDOW',trading_started:false}));
    } else {
      const here=path.dirname(fileURLToPath(import.meta.url)),pin=json(path.join(here,'runtime-pin.json'));
      for(const f of pin.files)if(sha(path.join(here,f.path))!==f.sha256)throw Error('EOD_RUNTIME_HASH_MISMATCH');
      const result=runDaily({root:'D:\\data\\lmax-eod',date:values.date??now.toISOString().slice(0,10),now,
        importer:{executable:'C:\\deploy\\IntradayPlatform\\toolchains\\dotnet-sdk-10\\dotnet.exe',assembly:path.join(here,'importer','QQ.Production.Intraday.Tools.LmaxDemoEod.dll')}});
      console.log(JSON.stringify(result));
      process.exitCode=result.error||!result.import_performed||result.blocking_breaks!==0?2:0;
    }
  }catch(e){console.error(e.message);process.exitCode=1;}
}
