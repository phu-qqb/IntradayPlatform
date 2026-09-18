import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { buildRecap, writeBundle, renderRecap } from './Build-LmaxDemoDailyRecap.mjs';

const header = 'Execution ID,Account Id,Trade Date,Order ID,Symbol,Units Bought/Sold,Trade Price,Total Commission\n';
const config = { environment: 'LMAX_DEMO', date: '2026-09-17', account_id: '1754288005', expected_order_ids: ['buy', 'sell'] };
function fixture(t) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'qq-recap-test-'));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  const file = (name, content) => { const p = path.join(root, name); fs.writeFileSync(p, content); return p; };
  const good = header + 'e1,1754288005,17-09-2026,buy,EUR/USD,30000,1.14771,-0.86078\ne2,1754288005,17-09-2026,sell,EUR/USD,-30000,1.14761,-0.86\n';
  return { root, file, good };
}

test('fallback skips missing, header-only, wrong account, stale day and incomplete export', t => {
  const f = fixture(t);
  const candidates = [path.join(f.root,'missing'), f.file('empty',header),
    f.file('wrong',f.good.replaceAll('1754288005','999')), f.file('stale',f.good.replaceAll('17-09-2026','16-09-2026')),
    f.file('partial',f.good.split('\n').slice(0,2).join('\n')), f.file('cache',f.good)];
  const r = buildRecap({ ...config, trade_candidates: candidates.map(p => ({path:p})) });
  assert.equal(r.selected_source.path,candidates[5]);
  assert.equal(r.official_trades.length,2);
  assert.equal(r.acquisition_attempts.filter(a => a.status === 'REJECTED').length,5);
  assert.equal(r.official_trade_units_sum,0);
  assert.equal(r.reconciliation.status,'NOT_RECONCILED');
});

test('all sources failing still creates an honest recap and unsent draft', t => {
  const f = fixture(t), out = path.join(f.root,'bundle');
  const r = writeBundle({...config,trade_candidates:[{path:path.join(f.root,'missing')}]},out);
  assert.equal(r.observed_round_trip_net_usd,null);
  assert.equal(r.official_trade_units_sum,null);
  assert.match(fs.readFileSync(path.join(out,'email-draft.txt'),'utf8'),/PROVISOIRE/);
  assert.equal(r.email.sent,false);
  assert.throws(() => writeBundle(config,out),/EEXIST/);
});

test('simulated values stay separate from observed PnL, ledger and economic TCA', t => {
  fixture(t);
  const observation = {account_id:config.account_id,date:config.date,observed_at_utc:'2026-09-17T18:00:00Z',
    source_reference:'test observation',gross_pnl_usd:-3,entry_commission_usd:-0.86078,exit_commission_usd:-0.86,position_units:0,working_orders:0};
  const base = buildRecap({...config,portal_observation:observation});
  const sim = buildRecap({...config,portal_observation:observation,simulated_tca:{label:'SIMULATED_NOT_FOR_ECONOMIC_VALIDATION',owner_authorized:true,
    orders:[{id:'BUY-SIM',units:30000,execution_price:1.14771,benchmark_price:1.14770,fee_usd:0.86},
      {id:'SELL-SIM',units:-30000,execution_price:1.14761,benchmark_price:1.14760,fee_usd:0.86}]}});
  assert.equal(sim.simulated_tca.orders[0].shortfall_usd,0.3);
  assert.equal(sim.simulated_tca.orders[1].shortfall_usd,-0.3);
  assert.equal(sim.simulated_tca.all_in_usd,1.72);
  assert.equal(sim.observed_round_trip_net_usd,base.observed_round_trip_net_usd);
  assert.deepEqual(sim.official_trades,base.official_trades);
  assert.equal(sim.economic_tca.all_in_usd_per_million,null);
  assert.deepEqual(sim.reconciliation,base.reconciliation);
});

test('rejects duplicate executions and corrupted cache; no false success', t => {
  const f=fixture(t);
  const r=buildRecap({...config,trade_candidates:[{path:f.file('duplicate',f.good.replace('e2,','e1,'))},
    {path:f.file('hash',f.good),sha256:'0'.repeat(64)}]});
  assert.equal(r.selected_source,null);
  assert.equal(r.acquisition_attempts[0].code,'DUPLICATE_OR_EMPTY_EXECUTION_ID');
  assert.equal(r.acquisition_attempts[1].code,'SOURCE_HASH_MISMATCH');
  assert.throws(()=>buildRecap({...config,environment:'PRODUCTION'}),/DEMO_ONLY/);
  assert.throws(()=>buildRecap({...config,simulated_tca:{owner_authorized:false}}),/SIMULATION_OPT_IN_REQUIRED/);
});

test('recovered accounting is explicitly distinct from independent FIX confirmation', t => {
  const f=fixture(t), trade_candidates=[{path:f.file('official',f.good)}];
  const source=buildRecap({...config,trade_candidates}).selected_source;
  const eod_result={schema:'lmax_demo_daily_eod_v1',date:config.date,account_id:config.account_id,
    individual_sha256:source.sha256,import_performed:true,report_set_imported:true,blocking_breaks:0,
    official_report_recovered_fills:2,recovered_fill_source:'AUTHENTIC_OFFICIAL_REPORT_NOT_FIX',recovery_ids:['TEST-RECOVERY']};
  const r=buildRecap({...config,trade_candidates,eod_result});
  assert.equal(r.reconciliation.status,'EXECUTIONS_RECOVERED_FROM_OFFICIAL_REPORT');
  assert.equal(r.reconciliation.independent_fix_confirmation,false);
  assert.equal(r.status,'PROVISIONAL');
  assert.equal(r.portal_observation,null);
  assert.match(renderRecap(r),/ne constitue pas une confirmation FIX indépendante/);
  assert.doesNotMatch(renderRecap(r),/réconciliation ouverte/);
  assert.throws(()=>buildRecap({...config,trade_candidates,eod_result:{...eod_result,recovery_ids:[]}}),/RECOVERY_PROVENANCE_INVALID/);
  assert.throws(()=>buildRecap({...config,trade_candidates,eod_result:{...eod_result,official_report_recovered_fills:3}}),/RECOVERY_PROVENANCE_INVALID/);
});
