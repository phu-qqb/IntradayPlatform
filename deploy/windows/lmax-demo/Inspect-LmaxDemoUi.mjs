import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { createRequire } from 'node:module';

// Bounded, read-only inspection of the existing official UI session. This does
// not create an attestation, enter credentials, submit orders, or call Account REST.
if (os.hostname().toUpperCase() !== 'EC2AMAZ-1QPHTD8' || os.userInfo().username.toLowerCase() !== 'administrator')
  throw new Error('DEMO_UI_OWNER_CONTEXT_REQUIRED');
const require = createRequire('C:/deploy/IntradayPlatform/operator/lmax-portal-reports-6dce3375-pr61/package.json');
const { chromium } = require('playwright');
const root = path.win32.join('D:\\data\\lmax-demo-ui', new Date().toISOString().replace(/[:.]/g, '-'));
fs.mkdirSync(root, { recursive: true });
let context;
try {
  context = await chromium.launchPersistentContext('D:\\qq-secure\\lmax-portal\\profiles\\test-1754288005',
    { channel: 'chrome', headless: true, acceptDownloads: false });
  const page = await context.newPage();
  await page.goto('https://web-order.london-demo.lmax.com/', { waitUntil: 'domcontentloaded', timeout: 30000 });
  await page.waitForTimeout(3000);
  const result = await page.evaluate(() => {
    const text = document.body?.innerText ?? '';
    const names = ['Positions', 'Open positions', 'Open Positions', 'Working orders', 'Working Orders', 'Orders', 'Order book', 'Order Book'];
    return {
      origin: location.origin,
      account1754288005Visible: text.includes('1754288005'),
      signInRequired: !!document.querySelector('input[type=password]'),
      exactLabelsPresent: names.filter(name => [...document.querySelectorAll('button,[role=tab],a,h1,h2,h3')].some(e => e.textContent.trim() === name)),
      explicitNoOpenPositionsVisible: /\bno open positions\b/i.test(text),
      explicitNoWorkingOrdersVisible: /\bno working orders\b/i.test(text)
    };
  });
  const receipt = { schema: 'lmax-demo-ui-inspection-v1', inspectedAtUtc: new Date().toISOString(), ...result,
    observationEstablished: false, ordersSubmitted: 0, credentialsEntered: false, accountRestCalled: false };
  fs.writeFileSync(path.win32.join(root, 'inspection.json'), JSON.stringify(receipt, null, 2), { flag: 'wx' });
  console.log(JSON.stringify(receipt));
} catch (error) {
  console.log(JSON.stringify({ schema: 'lmax-demo-ui-inspection-v1', observationEstablished: false,
    errorType: error.name, ordersSubmitted: 0, credentialsEntered: false }));
  process.exitCode = 1;
} finally {
  await context?.close();
}
