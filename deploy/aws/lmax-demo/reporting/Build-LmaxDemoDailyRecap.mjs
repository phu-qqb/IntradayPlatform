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
  const trades = csv.rows.map(r => {
    required(r['Account Id'] === config.account_id, 'ACCOUNT_MISMATCH');
    required(r['Trade Date'] === date, 'DATE_MISMATCH');
    required(r['Execution ID'] && !ids.has(r['Execution ID']), 'DUPLICATE_OR_EMPTY_EXECUTION_ID');
    required(r['Order ID'] && r.Symbol === 'EUR/USD', 'UNSUPPORTED_OR_EMPTY_INSTRUMENT_ORDER');
    ids.add(r['Execution ID']);
    const units = numeric(r['Units Bought/Sold']), price = numeric(r['Trade Price']);
    required(units !== 0 && price > 0, 'INVALID_TRADE');
    return { execution_id: r['Execution ID'], order_id: r['Order ID'], symbol: r.Symbol,
      timestamp: r.Timestamp, units, price, commission_usd: numeric(r['Total Commission']), source: 'OFFICIAL_CSV' };
  });
  for (const order of config.expected_order_ids ?? [])
    required(trades.some(t => t.order_id === order), 'EXPECTED_ORDER_MISSING');
  return { source: candidate.path, sha256: hash(bytes), trades };
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
  const csvNetUnits = trades.length ? round(trades.reduce((a,t) => a + t.units, 0)) : null;
  const commissions = trades.length ? round(trades.reduce((a,t) => a + t.commission_usd, 0)) : null;
  // Flat trade sum is not proof of flat account: initial inventory/other activity may exist.
  const recap = { schema: 'lmax_demo_daily_recap_v1', date: config.date, account_id: config.account_id,
    generated_at_utc: new Date().toISOString(), environment: config.environment,
    status: 'PROVISIONAL', acquisition_attempts: attempts, selected_source: selected && { path: selected.source, sha256: selected.sha256 },
    official_trades: trades, official_trade_units_sum: csvNetUnits, official_commissions_usd: commissions,
    portal_observation: observation,
    observed_round_trip_net_usd: observation ? round(observation.gross_pnl_usd + observation.entry_commission_usd + observation.exit_commission_usd) : null,
    observed_net_precision: observation ? 'APPROXIMATE_EXIT_FEE_ROUNDED' : 'UNAVAILABLE',
    reconciliation: { status: 'NOT_RECONCILED', internal_evidence: config.internal_evidence ?? null,
      official_report_import_performed: false, ledger_mutation_performed: false },
    economic_tca: { status: 'MISSING_M15_BENCHMARK', all_in_usd_per_million: null },
    breaks: [...(selected ? [] : ['NO_COMPLETE_OFFICIAL_TRADES_EXPORT']), 'REPORT_SET_RECONCILIATION_PENDING', 'M15_BENCHMARK_MISSING'],
    email: { status: 'DRAFT_ONLY', sent: false } };
  if (config.internal_evidence?.status === 'FIX_CONTINUITY_LOST') recap.breaks.push('FIX_CONTINUITY_LOST');
  if (config.simulated_tca) {
    const s = config.simulated_tca;
    required(s.label === 'SIMULATED_NOT_FOR_ECONOMIC_VALIDATION' && s.owner_authorized === true, 'SIMULATION_OPT_IN_REQUIRED');
    required(Array.isArray(s.orders) && s.orders.length > 0, 'SIMULATED_ORDERS_REQUIRED');
    const orders = s.orders.map(o => {
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
  let body = `# LMAX Demo — récapitulatif du ${r.date}\n\n**PROVISOIRE — réconciliation ouverte.**\n\n`;
  body += `## Résultat constaté\n\n`;
  body += o ? `Position observée : **${o.position_units} EUR**, ordres actifs : **${o.working_orders}** (${o.observed_at_utc}).\n\nPnL brut : **${money(o.gross_pnl_usd)} USD**. Net après commissions : **≈ ${money(r.observed_round_trip_net_usd)} USD** (sortie arrondie au centime).\n\nSource : ${o.source_reference}.\n\n` : 'État du compte et PnL : indisponibles. Absence de preuve ≠ activité nulle.\n\n';
  body += `CSV complet retenu : ${r.selected_source ? r.selected_source.path : 'aucun'}. ${r.official_trades.length} exécution(s) dans la source retenue. Les exports incomplets restent conservés mais ne sont pas présentés comme une journée complète.\n\n`;
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
