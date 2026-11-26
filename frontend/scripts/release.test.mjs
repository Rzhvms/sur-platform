import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';
import { compatibleRelease, sameRelease } from '../src/app/core/release/release-policy.ts';

test('release comparison requires an actual API response and a matching marked API revision', () => {
  const api = { version: '0.1.0', revision: 'revision-one', contractVersion: 1 };
  const spa = { spaVersion: '0.1.0', apiVersion: '0.1.0', apiRevision: 'revision-one', contractVersion: 1, openApiSha256: 'a'.repeat(64) };
  assert.equal(compatibleRelease('<html>SPA fallback</html>'), false);
  assert.equal(compatibleRelease({ ...api, contractVersion: 2 }), false);
  assert.equal(sameRelease(api, spa), true);
  assert.equal(sameRelease({ ...api, revision: 'revision-two' }, spa), false);
  assert.equal(sameRelease(api, { ...spa, openApiSha256: '' }), false);
});

test('nginx preserves security headers for SPA fallback and API error responses', () => {
  const nginx = readFileSync(new URL('../../backend/deploy/nginx/default.conf', import.meta.url), 'utf8');
  const locations = [...nginx.matchAll(/location[^\n]*\{([^}]+)\}/g)].map(match => match[1]);
  assert.ok(locations.every(body => !body.includes('add_header')));
  assert.match(nginx, /proxy_intercept_errors off;/);
  assert.match(nginx, /proxy_cache off;/);
  assert.match(nginx, /script-src 'self';/);
  assert.ok(!nginx.includes("'unsafe-eval'"));
  const logging = nginx.match(/log_format sur_access ([^;]+);/)[1];
  assert.ok(!/\$(?:request|args|http_authorization|http_cookie)(?:\W|$)/.test(logging));
});
