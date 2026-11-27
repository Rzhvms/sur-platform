import assert from 'node:assert/strict';
import { test } from 'vitest';
import { compatibleRelease, sameRelease } from './release-policy';

test('release comparison requires an actual API response and a matching marked API revision', () => {
  const api = { version: '0.1.0', revision: 'revision-one', contractVersion: 1 };
  const spa = { spaVersion: '0.1.0', apiVersion: '0.1.0', apiRevision: 'revision-one', contractVersion: 1, openApiSha256: 'a'.repeat(64) };
  assert.equal(compatibleRelease('<html>SPA fallback</html>'), false);
  assert.equal(compatibleRelease({ ...api, contractVersion: 2 }), false);
  assert.equal(sameRelease(api, spa), true);
  assert.equal(sameRelease({ ...api, revision: 'revision-two' }, spa), false);
  assert.equal(sameRelease(api, { ...spa, openApiSha256: '' }), false);
});
