import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

// Offline reporting only: no network, database, order entry or mail transport.
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const required = (ok, code) => { if (!ok) throw new Error(code); };
const numeric = value => { required(value !== '' && value != null && Number.isFinite(Number(value)), 'INVALID_NUMBER'); return Number(value); };
const round = value => Math.round(value * 1e8) / 1e8;
const money = value => value == null ? 'indisponible' : value.toFixed(2);
const instruments = Object.freeze({'EUR/USD':'4001','GBP/USD':'4002','AUD/USD':'4007','NZD/USD':'100613',
  'USD/JPY':'4004','USD/CHF':'4010','USD/CAD':'4013','USD/HUF':'100501','USD/MXN':'100507',
  'USD/NOK':'100513','USD/PLN':'100523','USD/RON':'100931','USD/SEK':'100529','USD/ZAR':'100547'});

function reportCurrencies(candidate, config, rows) {
  const summaryPath = path.join(path.dirname(candidate.path), 'trades.csv');
  if (!fs.existsSync(summaryPath)) return {currencies:new Map(), evidence:null};
  const bytes = fs.readFileSync(summaryPath), summary = parseCsv(bytes.toString('utf8'));
  for (const key of ['Date & Time','LMAX Symbol','Currency','Contracts','Commission (full precision)','Account Id'])
    required(summary.headers.includes(key), 'SUMMARY_CURRENCY_SCHEMA');
  required(summary.rows.length > 0 && summary.rows.every(r => r['Account Id'] === config.account_id
    && r['Date & Time'].startsWith(config.date + ' ') && /^[A-Z]{3}$/.test(r.Currency)), 'SUMMARY_CURRENCY_SCOPE');
  const currencies = new Map();
  for (const symbol of new Set(rows.map(r => r.Symbol))) {
    const matching = summary.rows.filter(r => r['LMAX Symbol'] === symbol), trades = rows.filter(r => r.Symbol === symbol);
    if (!matching.length) continue;
    const ccys = new Set(matching.map(r => r.Currency));
    required(ccys.size === 1, 'SUMMARY_AMBIGUOUS_CURRENCY');
    const sum = (xs, f) => xs.reduce((a, r) => a + f(r), 0);
    required(Math.abs(sum(matching,r=>numeric(r.Contracts)*10000)-sum(trades,r=>numeric(r['Units Bought/Sold']))) < 1e-6
      && Math.abs(sum(matching,r=>Math.abs(numeric(r.Contracts))*10000)-sum(trades,r=>Math.abs(numeric(r['Units Bought/Sold'])))) < 1e-6
      && Math.abs(sum(matching,r=>Math.abs(numeric(r['Commission (full precision)'])))-sum(trades,r=>Math.abs(numeric(r['Total Commission'])))) < 1e-5,
      'SUMMARY_TRADE_CURRENCY_BINDING_MISMATCH');
    currencies.set(symbol, matching[0].Currency);
  }
  return {currencies, evidence:{path:summaryPath,sha256:hash(bytes)}};
}

export function parseCsv(text) {
  const rows = []; let row = [], cell = '', quoted = false;
  for (let i = 0; i < text.length; i++) {
    const ch = text[i];
    if (ch === '"') {
      if (quoted && text[i + 1] === '"') { cell += '"'; i++; }
      else quoted = !quoted;
    } else if (!quoted && (ch === ',' || ch === '\n')) {
      row.push(cell.replace(/\r$/, '')); cell = '';
      if (ch === '\n') { rows.push(row); row = []; }
    } else cell += ch;
  }
  required(!quoted, 'CSV_UNCLOSED_QUOTE');
  if (cell || row.length) { row.push(cell.replace(/\r$/, '')); rows.push(row); }
  const headers = rows.shift()?.map(x => x.replace(/^\uFEFF/, '')) ?? [];
  required(new Set(headers).size === headers.length, 'CSV_DUPLICATE_HEADER');
  return { headers, rows: rows.filter(r => r.some(Boolean)).map(r => {
    required(r.length === headers.length, 'CSV_COLUMN_COUNT');
    return Object.fromEntries(headers.map((h, i) => [h, r[i]]));
  }) };
}

function readTrades(candidate, config) {
  const bytes = fs.readFileSync(candidate.path);
  if (candidate.sha256) required(hash(bytes) === candidate.sha256, 'SOURCE_HASH_MISMATCH');
  const csv = parseCsv(bytes.toString('utf8'));
  for (const key of ['Execution ID', 'Account Id', 'Trade Date', 'Order ID', 'Symbol',
    'Units Bought/Sold', 'Trade Price', 'Total Commission']) required(csv.headers.includes(key), 'CSV_SCHEMA');
  required(csv.rows.length > 0, 'HEADER_ONLY_NOT_ZERO_ACTIVITY');
  const date = config.date.split('-').reverse().join('-'), ids = new Set();
  const currencyProof = reportCurrencies(candidate, config, csv.rows);
  const trades = csv.rows.map(r => {
    required(r['Account Id'] === config.account_id, 'ACCOUNT_MISMATCH');
    required(r['Trade Date'] === date, 'DATE_MISMATCH');
    required(r['Execution ID'] && !ids.has(r['Execution ID']), 'DUPLICATE_OR_EMPTY_EXECUTION_ID');
    required(r['Order ID'] && Object.hasOwn(instruments, r.Symbol), 'UNSUPPORTED_OR_EMPTY_INSTRUMENT_ORDER');
    if (csv.headers.includes('Instrument ID')) required(r['Instrument ID'] === instruments[r.Symbol], 'INSTRUMENT_ID_MISMATCH');
    ids.add(r['Execution ID']);
    const units = numeric(r['Units Bought/Sold']), price = numeric(r['Trade Price']);
    required(units !== 0 && price > 0, 'INVALID_TRADE');
    // Retain the qualified historical EURUSD binding. Other currencies require the
    // matching official summary, never an inferred USD label or an invented FX rate.
    const currency = currencyProof.currencies.get(r.Symbol) ?? (r.Symbol === 'EUR/USD' ? 'USD' : null);
    const commission = numeric(r['Total Commission']), pnl = r['Total Profit Loss'] ? numeric(r['Total Profit Loss']) : null;
    return { execution_id: r['Execution ID'], order_id: r['Order ID'], symbol: r.Symbol,
      instrument_id: instruments[r.Symbol], base_currency:r.Symbol.slice(0,3), quote_currency:r.Symbol.slice(4),
      timestamp: r.Timestamp, units, price, report_currency:currency, commission_reported:commission,
      reported_profit_loss:pnl, commission_usd:currency==='USD'?commission:null,
      reported_profit_loss_usd:currency==='USD'?pnl:null, source: 'OFFICIAL_CSV' };
  });
  for (const order of config.expected_order_ids ?? [])
    required(trades.some(t => t.order_id === order), 'EXPECTED_ORDER_MISSING');
  return { source: candidate.path, sha256: hash(bytes), trades, currencyEvidence:currencyProof.evidence };
}

export function buildRecap(config) {
  required(config.environment === 'LMAX_DEMO', 'DEMO_ONLY');
  required(/^\d{4}-\d{2}-\d{2}$/.test(config.date) && new Date(config.date).toISOString().slice(0, 10) === config.date, 'INVALID_DATE');
  required(/^\d+$/.test(config.account_id), 'ACCOUNT_REQUIRED');
  const attempts = []; let selected = null;
  // Candidate order is explicit: fresh capture, same-day cache, operator-supplied export.
  // Never concatenate overlapping exports or fall back to a different day/account.
  for (const candidate of config.trade_candidates ?? []) {
    try { selected = readTrades(candidate, config); attempts.push({ path: candidate.path, status: 'ACCEPTED' }); break; }
    catch (e) { attempts.push({ path: candidate.path, status: 'REJECTED', code: e.code ?? e.message }); }
  }
  const observation = config.portal_observation ?? null;
  if (observation) {
    required(observation.account_id === config.account_id && observation.date === config.date, 'OBSERVATION_SCOPE_MISMATCH');
    required(observation.source_reference && observation.observed_at_utc?.startsWith(config.date), 'OBSERVATION_PROVENANCE_REQUIRED');
    for (const k of ['gross_pnl_usd', 'entry_commission_usd', 'exit_commission_usd', 'position_units', 'working_orders'])
      required(typeof observation[k] === 'number' && Number.isFinite(observation[k]), 'OBSERVATION_NUMERIC_TYPE');
  }
  const trades = selected?.trades ?? [];
  const groups = [...new Set(trades.map(t=>t.symbol))].sort().map(symbol=>{
    const rows=trades.filter(t=>t.symbol===symbol);
    return {symbol,base_currency:rows[0].base_currency,executions:rows.length,
      units_sum:round(rows.reduce((a,t)=>a+t.units,0)),gross_units:round(rows.reduce((a,t)=>a+Math.abs(t.units),0))};
  });
  const csvNetUnits = groups.length===1 ? groups[0].units_sum : null;
  const allUsd = trades.length>0 && trades.every(t=>t.report_currency==='USD');
  const commissions = allUsd ? round(trades.reduce((a,t) => a + t.commission_usd, 0)) : null;
  const monetary = [...new Set(trades.map(t=>t.report_currency).filter(Boolean))].sort().map(currency=>{
    const rows=trades.filter(t=>t.report_currency===currency), pnl=rows.filter(t=>t.reported_profit_loss!==null);
    const fee=round(rows.reduce((a,t)=>a+t.commission_reported,0));
    const gross=pnl.length===rows.length?round(pnl.reduce((a,t)=>a+t.reported_profit_loss,0)):null;
    return {currency,executions:rows.length,commission:fee,reported_gross:gross,net_after_commissions:gross===null?null:round(gross+fee)};
  });
  // Flat trade sum is not proof of flat account: initial inventory/other activity may exist.
  const recap = { schema: 'lmax_demo_daily_recap_v1', date: config.date, account_id: config.account_id,
    generated_at_utc: new Date().toISOString(), environment: config.environment,
    status: 'PROVISIONAL', acquisition_attempts: attempts, selected_source: selected && { path: selected.source, sha256: selected.sha256 },
    official_trades: trades, official_trade_units_sum: csvNetUnits, official_commissions_usd: commissions,
    official_trade_units_by_symbol:groups, selected_rows_amounts_by_currency:monetary,
    currency_evidence:selected?.currencyEvidence??null,
    portal_observation: observation,
    observed_round_trip_net_usd: observation ? round(observation.gross_pnl_usd + observation.entry_commission_usd + observation.exit_commission_usd) : null,
    observed_net_precision: observation ? 'APPROXIMATE_EXIT_FEE_ROUNDED' : 'UNAVAILABLE',
    reconciliation: { status: 'NOT_RECONCILED', internal_evidence: config.internal_evidence ?? null,
      official_report_import_performed: false, ledger_mutation_performed: false },
    economic_tca: { status: 'MISSING_M15_BENCHMARK', all_in_usd_per_million: null },
    breaks: [...(selected ? [] : ['NO_COMPLETE_OFFICIAL_TRADES_EXPORT']), 'REPORT_SET_RECONCILIATION_PENDING', 'M15_BENCHMARK_MISSING'],
    email: { status: 'DRAFT_ONLY', sent: false } };
  if (config.internal_evidence?.status === 'FIX_CONTINUITY_LOST') recap.breaks.push('FIX_CONTINUITY_LOST');
  if(trades.some(t=>t.report_currency===null))recap.breaks.push('REPORT_MONETARY_CURRENCY_UNVERIFIED');
  recap.acquisition_status = config.acquisition_status ?? 'NOT_AUTOMATED';
  if (config.pipeline_error) recap.breaks.push(config.pipeline_error);
  if (config.eod_result) {
    const e = config.eod_result;
    required(e.schema === 'lmax_demo_daily_eod_v1' && e.date === config.date && e.account_id === config.account_id
      && e.individual_sha256 === selected?.sha256 && e.import_performed === true
      && Number.isInteger(e.blocking_breaks) && e.blocking_breaks >= 0, 'EOD_RECEIPT_SCOPE_MISMATCH');
    const recovered=e.official_report_recovered_fills??null;
    if(recovered!==null) required(Number.isInteger(recovered)&&recovered>=0&&recovered<=trades.length
      &&(recovered===0||(e.recovered_fill_source==='AUTHENTIC_OFFICIAL_REPORT_NOT_FIX'&&Array.isArray(e.recovery_ids)&&e.recovery_ids.length>0)), 'RECOVERY_PROVENANCE_INVALID');
    recap.reconciliation = { status: e.blocking_breaks ? 'BREAKS_OPEN' : recovered>0 ? 'EXECUTIONS_RECOVERED_FROM_OFFICIAL_REPORT' : 'EXECUTIONS_MATCHED_SCOPE_LIMITED',
      official_report_import_performed: true, ledger_mutation_performed: false,
      official_report_recovered_fills:recovered, independent_fix_confirmation:recovered>0?false:null, receipt: e };
    if (e.blocking_breaks) recap.breaks.push('BROKER_EXECUTIONS_MISSING_OR_DIFFERENT_INTERNALLY');
    if (e.report_set_imported && e.blocking_breaks === 0) recap.breaks = recap.breaks.filter(x => x !== 'REPORT_SET_RECONCILIATION_PENDING');
  }
  const reported = trades.filter(t => t.reported_profit_loss_usd !== null);
  recap.selected_rows_reported_gross_usd = allUsd && reported.length===trades.length ? round(reported.reduce((a,t)=>a+t.reported_profit_loss_usd,0)) : null;
  recap.selected_rows_net_after_commissions_usd = recap.selected_rows_reported_gross_usd===null ? null : round(recap.selected_rows_reported_gross_usd + commissions);
  if (config.simulated_tca) {
    const s = config.simulated_tca;
    required(s.label === 'SIMULATED_NOT_FOR_ECONOMIC_VALIDATION' && s.owner_authorized === true, 'SIMULATION_OPT_IN_REQUIRED');
    required(Array.isArray(s.orders) && s.orders.length > 0, 'SIMULATED_ORDERS_REQUIRED');
    const orders = s.orders.map(o => {
      required(!o.symbol || o.symbol==='EUR/USD', 'SIMULATED_TCA_USD_QUOTE_FIXTURE_ONLY');
      const qty = numeric(o.units), price = numeric(o.execution_price), benchmark = numeric(o.benchmark_price), fee = numeric(o.fee_usd);
      required(qty !== 0 && price > 0 && benchmark > 0 && fee >= 0, 'INVALID_SIMULATED_ORDER');
      const shortfall = qty * (price - benchmark), notional = Math.abs(qty) * benchmark;
      return { ...o, source: 'SIMULATED', shortfall_usd: round(shortfall), shortfall_pips: round(Math.sign(qty) * (price - benchmark) / 0.0001),
        benchmark_notional_usd: round(notional), all_in_usd: round(shortfall + fee), all_in_usd_per_million: round((shortfall + fee) / notional * 1e6) };
    });
    const total = key => round(orders.reduce((a,o) => a + o[key], 0));
    recap.simulated_tca = { label: s.label, purpose: 'Test de calcul et de présentation uniquement', orders,
      shortfall_usd: total('shortfall_usd'), fees_usd: total('fee_usd'), all_in_usd: total('all_in_usd'),
      all_in_usd_per_million: round(total('all_in_usd') / total('benchmark_notional_usd') * 1e6) };
  }
  return recap;
}

export function renderRecap(r) {
  const o = r.portal_observation;
  let body = `# LMAX Demo — récapitulatif du ${r.date}\n\n**PROVISOIRE — contrôles de clôture incomplets.**\n\n`;
  body += `## Résultat constaté\n\n`;
  body += o ? `Position observée : **${o.position_units} EUR**, ordres actifs : **${o.working_orders}** (${o.observed_at_utc}).\n\nPnL brut : **${money(o.gross_pnl_usd)} USD**. Net après commissions : **≈ ${money(r.observed_round_trip_net_usd)} USD** (sortie arrondie au centime).\n\nSource : ${o.source_reference}.\n\n` : 'État du compte et PnL : indisponibles. Absence de preuve ≠ activité nulle.\n\n';
  body += `CSV validé pour le compte et la date : ${r.selected_source ? r.selected_source.path : 'aucun'}. ${r.official_trades.length} exécution(s). La complétude de la journée et l'état actuel du compte ne sont pas déduits du CSV.\n\n`;
  if(r.official_trade_units_by_symbol.length){
    body+='| Paire | Exécutions | Solde des quantités du rapport | Devise de base |\n|---|---:|---:|---|\n';
    for(const g of r.official_trade_units_by_symbol)body+=`| ${g.symbol} | ${g.executions} | ${g.units_sum} | ${g.base_currency} |\n`;
    body+='\nLes soldes par paire ne sont pas des positions de compte. Aucun total entre devises de base différentes.\n\n';
  }
  if(r.selected_rows_amounts_by_currency.length){
    body+='| Devise du rapport | PnL brut renseigné | Commissions | Net des lignes |\n|---|---:|---:|---:|\n';
    for(const g of r.selected_rows_amounts_by_currency)body+=`| ${g.currency} | ${money(g.reported_gross)} | ${money(g.commission)} | ${money(g.net_after_commissions)} |\n`;
    body+='\nMontants par devise ; aucune conversion USD déduite des prix des trades.\n\n';
  }
  if (r.selected_rows_reported_gross_usd !== null) body += `PnL renseigné dans les lignes sélectionnées : **${r.selected_rows_reported_gross_usd} USD** ; commissions : **${r.official_commissions_usd} USD** ; net après ces commissions : **${r.selected_rows_net_after_commissions_usd} USD**. Périmètre des lignes du rapport, hors autres flux éventuels.\n\n`;
  body += `Acquisition : ${r.acquisition_status}. Import EOD effectué : ${r.reconciliation.official_report_import_performed ? 'oui' : 'non'}. Réconciliation : ${r.reconciliation.status}.\n\n`;
  if(r.reconciliation.official_report_recovered_fills>0) body += `${r.reconciliation.official_report_recovered_fills} exécution(s) interne(s) récupérée(s) depuis le rapport officiel, dont la clôture manuelle. Ce rapprochement vérifie la reprise comptable ; il ne constitue pas une confirmation FIX indépendante.\n\n`;
  if (o?.orders?.length) {
    body += '## Ordres constatés dans le portail\n\n| Ordre LMAX | Sens | EUR | Prix moyen | Statut |\n|---|---|---:|---:|---|\n';
    for (const t of o.orders) body += `| ${t.order_id} | ${t.side} | ${t.units} | ${t.average_price} | ${t.status} |\n`;
    body += '\nCes observations ne remplacent pas le jeu officiel de fills après clôture.\n\n';
  }
  body += '## Replis et écarts\n\n';
  for (const a of r.acquisition_attempts) body += `- ${a.status} : ${a.path}${a.code ? ` — ${a.code}` : ''}\n`;
  for (const b of r.breaks) body += `- ${b}\n`;
  body += '\nLa réconciliation applicative et le coût économique M15 ne sont pas validés. Les preuves du portail ne reconstruisent pas des messages FIX.\n';
  if (r.simulated_tca) {
    body += '\n## TCA SIMULÉE — VALEURS FICTIVES\n\nScénario de test autonome, sans valeur de performance. Les résultats ci-dessous ne modifient ni le PnL constaté ni le registre des trades.\n\n| Ordre fictif | EUR | Exécution fictive | Benchmark fictif | Shortfall USD | Frais fictifs USD | All-in USD/M |\n|---|---:|---:|---:|---:|---:|---:|\n';
    for (const t of r.simulated_tca.orders) body += `| ${t.id} | ${t.units} | ${t.execution_price} | ${t.benchmark_price} | ${money(t.shortfall_usd)} | ${t.fee_usd} | ${money(t.all_in_usd_per_million)} |\n`;
    body += `\nTotal simulé : shortfall **${money(r.simulated_tca.shortfall_usd)} USD**, frais **${money(r.simulated_tca.fees_usd)} USD**, coût all-in **${money(r.simulated_tca.all_in_usd)} USD**, soit **${money(r.simulated_tca.all_in_usd_per_million)} USD/M**. Dénominateur : somme des notionals USD aux benchmarks fictifs.\n`;
  }
  body += '\n## Récapitulatif email\n\nBrouillon généré uniquement. Aucun destinataire, compte email, envoi ou planification configuré.\n';
  return body;
}

export function writeBundle(config, destination) {
  const recap = buildRecap(config), body = renderRecap(recap);
  fs.mkdirSync(destination, { recursive: false }); // New immutable run directory; never overwrite a previous recap.
  const subject = `[LMAX DEMO][PROVISOIRE${recap.simulated_tca ? '][TCA SIMULEE' : ''}] ${config.date} — récapitulatif quotidien`;
  for (const [name, data] of Object.entries({ 'recap.json': JSON.stringify(recap, null, 2),
    'recap.md': body, 'email-draft.txt': `Objet : ${subject}\n\n${body}` })) fs.writeFileSync(path.join(destination, name), data, { flag: 'wx' });
  fs.writeFileSync(path.join(destination, 'manifest.json'), JSON.stringify({ schema: 'lmax_demo_recap_manifest_v1',
    source_sha256: hash(fs.readFileSync(fileURLToPath(import.meta.url))), config_sha256: hash(JSON.stringify(config)),
    files: ['recap.json', 'recap.md', 'email-draft.txt'].map(name => ({ name, sha256: hash(fs.readFileSync(path.join(destination, name))) })),
    network_requests: 0, database_writes: 0, emails_sent: 0, orders_sent: 0 }, null, 2), { flag: 'wx' });
  return recap;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    required(process.argv.length === 4, 'Usage: node Build-LmaxDemoDailyRecap.mjs config.json NEW_OUTPUT_DIRECTORY');
    const recap = writeBundle(JSON.parse(fs.readFileSync(process.argv[2], 'utf8').replace(/^\uFEFF/, '')), process.argv[3]);
    console.log(JSON.stringify({ status: recap.status, output: process.argv[3], breaks: recap.breaks, email_sent: false }));
  } catch (e) { console.error(e.message); process.exitCode = 1; }
}
