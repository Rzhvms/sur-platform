import https from 'node:https';
import { readFileSync } from 'node:fs';

// Read-only checks for an explicitly supplied pair of disposable HTTPS clients.
// The output includes only boolean results and durations, never headers or response bodies.
const origins = process.argv.slice(2);
if (origins.length !== 2 || origins[0] === origins[1]) { throw new Error('Pass two distinct HTTPS test origins'); }
for (const origin of origins) {
  const url = new URL(origin);
  if (url.protocol !== 'https:' || url.username || url.password || url.pathname !== '/' || url.search || url.hash) {
    throw new Error('Expected a canonical HTTPS test origin');
  }
}
const caFile = process.env['SUR_TEST_CA'];
const ca = caFile ? readFileSync(caFile) : undefined;
const tokenA = process.env['SUR_TEST_TOKEN_A'];
const tokenB = process.env['SUR_TEST_TOKEN_B'];
const riskId = process.env['SUR_TEST_RISK_ID'];
function get(origin, path, token) {
  return new Promise((resolve, reject) => {
    const start = performance.now();
    const request = https.get(new URL(path, origin), {
      ca, timeout: 8000, headers: token ? { Authorization: 'Bearer ' + token } : {},
    }, response => {
      const pieces = [];
      let length = 0;
      response.on('data', piece => {
        length += piece.length;
        if (length > 2 * 1024 * 1024) { response.destroy(new Error('Response limit')); return; }
        pieces.push(piece);
      });
      response.on('error', reject);
      response.on('end', () => resolve({
        status: response.statusCode, headers: response.headers, body: Buffer.concat(pieces).toString('utf8'),
        durationMs: Math.round(performance.now() - start),
      }));
    });
    request.on('timeout', () => request.destroy(new Error('Request timeout')));
    request.on('error', reject);
  });
}
const results = [];
async function check(name, action) {
  try { results.push({ name, passed: !!(await action()) }); }
  catch { results.push({ name, passed: false }); }
}
for (let tenant = 0; tenant < origins.length; tenant++) {
  const origin = origins[tenant];
  await check('tenant-' + tenant + ':deep-link-and-headers', async () => {
    const response = await get(origin, '/reports');
    return response.status === 200 && response.body.includes('<sur-root') &&
      response.headers['content-security-policy']?.includes("script-src 'self'") &&
      response.headers['x-content-type-options'] === 'nosniff' && response.headers['referrer-policy'] === 'no-referrer';
  });
  await check('tenant-' + tenant + ':api-does-not-fallback', async () => {
    const response = await get(origin, '/api/v1/public/acceptance-route-that-does-not-exist');
    return response.status >= 400 && !response.body.includes('<sur-root');
  });
  await check('tenant-' + tenant + ':internal-closed', async () => {
    const response = await get(origin, '/api/v1/internal/sessions');
    return response.status === 404 && !response.body.includes('<sur-root');
  });

}
if (tokenA && tokenB && riskId && /^[a-f0-9-]{36}$/i.test(riskId)) {
  await check('matching-ids-own-data', async () => {
    const [a, b] = await Promise.all([
      get(origins[0], '/api/v1/public/risks/' + riskId, tokenA),
      get(origins[1], '/api/v1/public/risks/' + riskId, tokenB),
    ]);
    return a.status === 200 && b.status === 200 &&
      JSON.parse(a.body).data.risk.name !== JSON.parse(b.body).data.risk.name;
  });
  await check('cross-tenant-jwt-rejected', async () => {
    const [a, b] = await Promise.all([
      get(origins[0], '/api/v1/public/risks/' + riskId, tokenB),
      get(origins[1], '/api/v1/public/risks/' + riskId, tokenA),
    ]);
    return a.status === 401 && b.status === 401;
  });
} else {
  results.push({ name: 'authenticated-isolation', passed: false, reason: 'missing-test-tokens-or-shared-risk-id' });
}
process.stdout.write(JSON.stringify({ checks: results, passed: results.every(row => row.passed) }, null, 2) + '\n');
process.exitCode = results.every(row => row.passed) ? 0 : 1;
