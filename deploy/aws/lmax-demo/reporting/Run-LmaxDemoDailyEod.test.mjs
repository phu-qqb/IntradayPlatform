import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {runDaily,candidates} from './Run-LmaxDemoDailyEod.mjs';
const header='Execution ID,Account Id,Trade Date,Order ID,Symbol,Units Bought/Sold,Trade Price,Total Commission\n';
const data=header+'e1,1754288005,17-09-2026,o1,EUR/USD,10000,1.1,-0.25\n';
function setup(t){const root=fs.mkdtempSync(path.join(os.tmpdir(),'daily-eod-'));t.after(()=>fs.rmSync(root,{recursive:true,force:true}));return root;}
function source(root,date,bytes=data){const dir=path.join(root,'inbox','1754288005',date);fs.mkdirSync(dir,{recursive:true});fs.writeFileSync(path.join(dir,'individual-trades.csv'),bytes);}
test('missing current-day source never consumes yesterday or starts importer',t=>{
 const root=setup(t);source(root,'2026-09-17');const r=runDaily({root,date:'2026-09-18',importer:{},invoke:()=>assert.fail('unexpected import')});
 assert.equal(r.error,'NO_VALID_LOCAL_EXPORT');assert.equal(r.import_performed,false);assert.ok(fs.existsSync(path.join(r.run,'recap','email-draft.txt')));
});
test('import failure still publishes provisional recap and retains source',t=>{
 const root=setup(t);source(root,'2026-09-17');const r=runDaily({root,date:'2026-09-17',importer:{},invoke:()=>({status:1})});
 assert.match(r.error,/IMPORT_FAILED/);assert.equal(r.import_performed,false);assert.equal(fs.readFileSync(path.join(root,'inbox','1754288005','2026-09-17','individual-trades.csv'),'utf8'),data);
});
test('real receipt scope is required and open reconciliation survives report',t=>{
 const root=setup(t);source(root,'2026-09-17');const r=runDaily({root,date:'2026-09-17',importer:{},invoke:(_e,args)=>{
 const i=JSON.parse(fs.readFileSync(args[1]));fs.writeFileSync(i.output,JSON.stringify({schema:'lmax_demo_daily_eod_v1',date:i.date,account_id:'1754288005',individual_sha256:i.files.individual.sha256,import_performed:true,blocking_breaks:1,report_set_imported:false}));return{status:0};}});
 const recap=JSON.parse(fs.readFileSync(path.join(r.run,'recap','recap.json')));assert.equal(recap.reconciliation.status,'BREAKS_OPEN');assert.equal(recap.reconciliation.official_report_import_performed,true);assert.equal(recap.status,'PROVISIONAL');assert.equal(recap.simulated_tca,undefined);
});
test('concurrent or stale owner is never forced',t=>{
 const root=setup(t);fs.writeFileSync(path.join(root,'daily-eod.lock'),'retained');assert.throws(()=>runDaily({root,date:'2026-09-17',importer:{}}),/EEXIST/);assert.equal(fs.readFileSync(path.join(root,'daily-eod.lock'),'utf8'),'retained');
});
test('portal denial falls back to same-date local data while preserving the blocker',t=>{
 const root=setup(t);source(root,'2026-09-17');let acquisitions=0;
 const r=runDaily({root,date:'2026-09-17',importer:{},acquire:({date,execute})=>{
  acquisitions++;assert.equal(execute,true);return{schema:'lmax_demo_report_launcher_receipt_v2',account_id:'1754288005',report_date:date,mode:'REPORT_DOWNLOAD',success:false,error:'AUTH_REQUIRED'};
 },invoke:(_e,args)=>{
  const i=JSON.parse(fs.readFileSync(args[1]));fs.writeFileSync(i.output,JSON.stringify({schema:'lmax_demo_daily_eod_v1',date:i.date,account_id:'1754288005',individual_sha256:i.files.individual.sha256,import_performed:true,blocking_breaks:1}));return{status:0};}});
 assert.equal(acquisitions,1);assert.equal(r.import_performed,true);assert.equal(r.acquisition,'PORTAL_FAILED_LOCAL_FALLBACK');assert.equal(r.error,'AUTH_REQUIRED');
});
test('partial capture never becomes an official fallback',t=>{
 const root=setup(t),p=path.join(root,'captures','failed-run','inbox','1754288005','2026-09-17');fs.mkdirSync(p,{recursive:true});fs.writeFileSync(path.join(p,'individual-trades.csv'),data);
 assert.deepEqual(candidates(root,'2026-09-17'),[]);
 const r=runDaily({root,date:'2026-09-17',importer:{},acquire:()=>{throw Error('AUTH_REQUIRED');},invoke:()=>assert.fail('partial capture imported')});
 assert.equal(r.import_performed,false);assert.equal(r.acquisition,'PORTAL_FAILED_NO_VALID_EXPORT');assert.match(r.error,/AUTH_REQUIRED/);
});
test('wrong-date acquisition receipt does not qualify the daily pipeline',t=>{
 const root=setup(t);const r=runDaily({root,date:'2026-09-18',importer:{},acquire:()=>({schema:'lmax_demo_report_launcher_receipt_v2',account_id:'1754288005',report_date:'2026-09-17',mode:'REPORT_DOWNLOAD',success:true}),invoke:()=>assert.fail('unexpected import')});
 assert.equal(r.acquisition_error,'ACQUISITION_RECEIPT_SCOPE_MISMATCH');assert.equal(r.import_performed,false);
});
