#!/usr/bin/env node
import crypto from "node:crypto";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { parseArgs } from "node:util";

// Report-only candidate, pending Core PR #61 review. Never starts the Worker.
export const SOURCE_COMMIT = "6dce3375aaaa7e8042c61a36467e81fe5e97cb49";
export const DOWNLOADER_ROOT = "C:\\deploy\\IntradayPlatform\\operator\\lmax-portal-reports-6dce3375-pr61";
const DOWNLOADER_SHA256 = "495093a578b3ac8f2ab4b202fcf39c713a97512f3b01c9216ed866798770257d";
const REPORT_ROOT = "D:\\data\\lmax-eod";
const PROFILE = "D:\\qq-secure\\lmax-portal\\profiles\\test-1754288005";

export function buildReportInvocation({ date, execute = false, interactive = false, now = new Date(), nonce = crypto.randomUUID() }) {
  const reportDate = date ?? now.toISOString().slice(0, 10);
  if (!/^\d{4}-\d{2}-\d{2}$/.test(reportDate)
      || !Number.isFinite(Date.parse(`${reportDate}T00:00:00Z`))
      || new Date(`${reportDate}T00:00:00Z`).toISOString().slice(0, 10) !== reportDate) {
    throw new Error("LMAX_REPORT_DATE_INVALID");
  }
  if (reportDate > now.toISOString().slice(0, 10)) throw new Error("LMAX_REPORT_DATE_IN_FUTURE");
  if (!/^[a-zA-Z0-9-]+$/.test(nonce)) throw new Error("LMAX_REPORT_RUN_ID_INVALID");
  const runId = `demo-reports-${now.toISOString().replace(/[-:.]/g, "")}-${nonce}`;
  const runRoot = path.win32.join(REPORT_ROOT, "logs", runId);
  const manifestPath = path.win32.join(runRoot, "acquisition-manifest.json");
  const args = [path.win32.join(DOWNLOADER_ROOT, "src", "downloader.mjs"),
    "--portal-url", "https://account.london-demo.lmax.com/", "--account-id", "1754288005",
    "--from", reportDate, "--to", reportDate,
    "--download-root", path.win32.join(REPORT_ROOT, "portal-downloads", runId),
    "--staging-root", path.win32.join(REPORT_ROOT, "inbox", runId),
    "--auth-mode", interactive ? "interactive-bootstrap" : "manual-session",
    "--user-data-dir", PROFILE, "--browser-channel", "chrome",
    "--auth-origin", "https://web-order.london-demo.lmax.com",
    "--output-manifest", manifestPath];
  if (interactive) args.push("--headed", "--manual-mfa-timeout-ms", "600000");
  if (execute) args.push("--execute-portal-download");
  return { runId, runRoot, manifestPath, reportDate, execute, interactive, args };
}

export function verifyDownloader() {
  const manifest = JSON.parse(fs.readFileSync(path.win32.join(DOWNLOADER_ROOT, "source-manifest.json"), "utf8"));
  if (manifest.repository !== "phu-qqb/QQ.Production.Core" || manifest.source_commit !== SOURCE_COMMIT) {
    throw new Error("LMAX_REPORT_SOURCE_COMMIT_MISMATCH");
  }
  const sha = crypto.createHash("sha256").update(fs.readFileSync(path.win32.join(DOWNLOADER_ROOT, "src", "downloader.mjs"))).digest("hex");
  if (sha !== DOWNLOADER_SHA256) throw new Error("LMAX_REPORT_DOWNLOADER_HASH_MISMATCH");
  for (const file of manifest.files) {
    if (!/^(src|test)\/[a-zA-Z0-9.-]+$|^(README\.md|package(-lock)?\.json)$/.test(file.path)) {
      throw new Error("LMAX_REPORT_SOURCE_PATH_INVALID");
    }
    const actual = crypto.createHash("sha256").update(fs.readFileSync(path.win32.join(DOWNLOADER_ROOT, file.path))).digest("hex");
    if (actual !== file.sha256) throw new Error(`LMAX_REPORT_SOURCE_HASH_MISMATCH:${file.path}`);
  }
}

function main() {
  const { values } = parseArgs({ options: { date: { type: "string" }, execute: { type: "boolean" }, interactive: { type: "boolean" } }, allowPositionals: false, strict: true });
  if (process.platform !== "win32" || os.hostname().toUpperCase() !== "EC2AMAZ-1QPHTD8"
      || os.userInfo().username.toLowerCase() !== "administrator"
      || process.env.USERDOMAIN?.toUpperCase() !== "EC2AMAZ-1QPHTD8") {
    throw new Error("LMAX_REPORT_DEMO_OWNER_CONTEXT_REQUIRED");
  }
  verifyDownloader();
  const invocation = buildReportInvocation(values);
  fs.mkdirSync(invocation.runRoot, { recursive: false });
  const logPath = path.win32.join(invocation.runRoot, "downloader.log");
  const log = fs.openSync(logPath, "wx");
  const startedUtc = new Date().toISOString();
  console.log(JSON.stringify({ run_id: invocation.runId, report_date: invocation.reportDate, execute: invocation.execute, interactive: invocation.interactive, run_root: invocation.runRoot }));
  let result;
  try {
    result = spawnSync(process.execPath, invocation.args, { stdio: ["ignore", log, log], shell: false });
  } finally {
    fs.closeSync(log);
  }
  const receipt = {
    schema: "lmax_demo_report_launcher_receipt_v1", run_id: invocation.runId,
    started_utc: startedUtc, completed_utc: new Date().toISOString(), source_commit: SOURCE_COMMIT,
    account_id: "1754288005", report_date: invocation.reportDate,
    mode: invocation.execute ? "REPORT_DOWNLOAD" : "PLAN_ONLY",
    process_exit_code: result.status, process_signal: result.signal, launch_error: result.error?.code ?? null,
    manifest_exists: fs.existsSync(invocation.manifestPath), log_path: logPath,
    worker_started: false, database_import_performed: false
  };
  fs.writeFileSync(path.win32.join(invocation.runRoot, "command-result.json"), JSON.stringify(receipt, null, 2), { flag: "wx" });
  console.log(JSON.stringify(receipt));
  process.exitCode = result.status ?? 1;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try { main(); } catch (error) { console.error(error.message); process.exitCode = 1; }
}
