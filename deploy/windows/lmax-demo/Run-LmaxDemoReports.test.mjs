import test from "node:test";
import assert from "node:assert/strict";
import { buildReportInvocation } from "./Run-LmaxDemoReports.mjs";

const now = new Date("2026-09-16T13:30:00Z");

test("default invocation plans reports without opening a portal session", () => {
  const plan = buildReportInvocation({ now });
  assert.equal(plan.reportDate, "2026-09-16");
  assert.equal(plan.execute, false);
  assert.ok(!plan.args.includes("--execute-portal-download"));
  assert.ok(!plan.args.includes("--headed"));
  assert.ok(plan.args.includes("https://account.london-demo.lmax.com/"));
  assert.ok(plan.args.includes("1754288005"));
});

test("each same-day capture has separate reports, staging and evidence directories", () => {
  const a = buildReportInvocation({ now, execute: true });
  const b = buildReportInvocation({ now, execute: true });
  for (const flag of ["--download-root", "--staging-root", "--output-manifest"]) {
    assert.notEqual(a.args[a.args.indexOf(flag) + 1], b.args[b.args.indexOf(flag) + 1]);
  }
  assert.notEqual(a.runRoot, b.runRoot);
});

test("interactive execution keeps authentication at the explicit Demo origin", () => {
  const plan = buildReportInvocation({ now, execute: true, interactive: true });
  assert.ok(plan.args.includes("--execute-portal-download"));
  assert.ok(plan.args.includes("interactive-bootstrap"));
  assert.ok(plan.args.includes("--headed"));
  assert.equal(plan.args[plan.args.indexOf("--auth-origin") + 1], "https://web-order.london-demo.lmax.com");
  assert.ok(!plan.args.includes("--session-recovery"));
  assert.ok(!plan.args.includes("--credential-secret-id"));
});

test("invalid, future and injected date/path values fail before any process launch", () => {
  for (const date of ["2026-02-30", "2026-09-17", "2026-9-16", "2026-09-16 --execute", "../reports"]) {
    assert.throws(() => buildReportInvocation({ now, date }), /LMAX_REPORT_DATE_/);
  }
  assert.throws(() => buildReportInvocation({ now, nonce: "../reuse" }), /LMAX_REPORT_RUN_ID_INVALID/);
});
